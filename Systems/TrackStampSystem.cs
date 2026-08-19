using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 바닥에 자국을 찍는 파티클 시스템.
    ///
    /// 종류마다 시스템 하나를 두고, 걸음이 생길 때마다 <see cref="Stamp"/>로 알갱이를
    /// 하나씩 직접 넣습니다. 방출을 게임 엔진에 맡기지 않는 이유는 좌·우 위치와
    /// 진행 방향 회전을 자국 하나하나에 정확히 지정해야 하기 때문입니다.
    /// </summary>
    internal static class TrackStampSystem
    {
        private sealed class Handle
        {
            internal GameObject Go = null!;
            internal ParticleSystem Ps = null!;
            internal ParticleSystemRenderer Renderer = null!;

            /// <summary>마지막으로 반영한 프로필 값. 바뀐 게 없으면 다시 설정하지 않습니다.</summary>
            internal int ConfigHash;

            /// <summary>이 통을 쓰는 프로필. 매 프레임 깜박임을 먹이려면 알아야 합니다.</summary>
            internal TrackProfile? Profile;

            /// <summary>지금 걸려 있는 재질. 밝기를 여기에 곱합니다.</summary>
            internal Material? Material;

            /// <summary>
            /// 알갱이를 읽어 오는 그릇.
            ///
            /// 매 프레임 새로 만들면 그대로 쓰레기가 됩니다. 한 번 잡아 두고
            /// 상한이 커질 때만 다시 잡습니다.
            /// </summary>
            internal ParticleSystem.Particle[]? Buffer;

            /// <summary>
            /// 흘릴 알갱이의 소수점 나머지.
            ///
            /// 초당 0.5개 같은 느린 속도를 프레임마다 정수로 자르면 영영 0이 되어
            /// 하나도 안 나옵니다. 남는 만큼을 들고 있다가 1이 넘으면 내보냅니다.
            /// </summary>
            internal float DriftCarry;
        }

        // 키가 TrackKind가 아니라 문자열인 이유: 왼발과 오른발이 <b>서로 다른 그림</b>을
        // 쓸 수 있습니다. 실제 발 실루엣을 구우면 좌우가 거울상이라 텍스처가 둘입니다.
        // 종류 하나에 머티리얼 하나로 묶어 두면 한쪽이 다른 쪽을 덮어씁니다.
        private static readonly Dictionary<string, Handle> Handles = new();

        /// <summary>
        /// "무한"으로 쓸 수명(초). 10시간입니다.
        ///
        /// <b>더 크게 잡으면 안 됩니다.</b> <see cref="float"/>는 유효자릿수가 7자리라
        /// 값이 커질수록 표현 가능한 최소 간격이 벌어집니다. 100만에서는 그 간격이
        /// 0.0625초여서, 파티클이 매 프레임 하는 <c>남은수명 -= 0.016</c>이 아예
        /// 반영되지 않습니다. 그러면 <c>나이 = 전체 - 남은</c>이 영원히 0이 되고,
        /// 나이에 기대는 깜박임과 색 순환이 통째로 얼어붙습니다.
        ///
        /// 36000에서는 그 간격이 0.004초라 한 프레임 분량이 네 배 여유로 들어갑니다.
        /// 한 판이 10시간을 넘길 일도 없고, 씬이 바뀌면 어차피 함께 사라집니다.
        ///
        /// (<see cref="float.PositiveInfinity"/>는 쓸 수 없습니다 — 나이 계산이
        /// NaN으로 무너집니다.)
        /// </summary>
        private const float InfiniteLife = 36_000f;

        /// <summary>
        /// "계속 남기기"일 때의 자국 수 상한.
        ///
        /// 파티클 시스템에는 <b>반드시</b> 상한이 있어야 합니다 — 그 수만큼 미리
        /// 자리를 잡아 두는 구조라 진짜 무한은 불가능합니다. 대신 실질적으로 닿지
        /// 않는 값으로 둡니다. 걸음이 초당 두어 번이니 1만 개면 쉬지 않고 걸어도
        /// 두어 시간치입니다.
        ///
        /// 더 키우지 않는 이유는 깜박임 때문입니다. 깜박임을 켜면 매 프레임 살아 있는
        /// 자국을 전부 읽고 다시 쓰므로 개수에 정비례합니다(화면 밖이면 건너뜁니다).
        /// </summary>
        private const int InfiniteStamps = 10_000;

        /// <summary>
        /// 자국 하나를 찍습니다.
        /// </summary>
        /// <param name="key">자국을 모아 둘 통. 그림이 다르면 키도 달라야 합니다.</param>
        /// <param name="material">이 자국을 그릴 재질.</param>
        /// <param name="position">바닥에 붙은 위치. <see cref="GroundProbe"/>가 낸 값입니다.</param>
        /// <param name="yawDegrees">진행 방향 (월드 Y축 기준 각도).</param>
        /// <param name="size">자국 한 변의 크기(미터).</param>
        internal static void Stamp(
            string key, TrackProfile profile, Material material,
            Vector3 position, float yawDegrees, float size)
        {
            var handle = Acquire(key, profile, material);
            if (handle == null)
                return;

            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startSize = size,
                startLifetime = LifeOf(profile),

                // 색 변화는 colorOverLifetime 그라디언트가 통째로 들고 있습니다.
                // 여기를 흰색으로 두어야 그라디언트 색이 그대로 나옵니다.
                startColor = Color.white,

                // HorizontalBillboard에서는 이 회전이 월드 Y축 회전이 됩니다.
                rotation = yawDegrees,

                applyShapeToPosition = false,
            };

            handle.Ps.Emit(emit, 1);
        }

        /// <summary>
        /// 종류에 맞는 파티클 시스템을 꺼냅니다. 없거나 씬이 바뀌어 파괴됐으면 새로 만듭니다.
        /// </summary>
        private static Handle? Acquire(string key, TrackProfile profile, Material material)
        {
            try
            {
                Handles.TryGetValue(key, out var handle);

                // 씬이 바뀌면 GameObject가 통째로 사라집니다. Unity의 == 는 파괴된
                // 객체도 null로 쳐 주므로 이 검사로 잡힙니다.
                if (handle == null || handle.Go == null || handle.Ps == null)
                {
                    handle = Create(key);
                    Handles[key] = handle;
                    handle.ConfigHash = 0;
                }

                handle.Profile = profile;
                handle.Material = material;

                int hash = ConfigHashOf(profile) * 31 + material.GetInstanceID();
                if (handle.ConfigHash != hash)
                {
                    Configure(handle, profile, material);
                    handle.ConfigHash = hash;
                }

                return handle;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 자국 시스템 준비 실패({key}): {ex.Message}");
                return null;
            }
        }

        private static Handle Create(string key)
        {
            // hideFlags를 걸지 않습니다.
            //
            // HideAndDontSave에는 DontSave가 들어 있어서 씬을 넘어가도 오브젝트가
            // 살아남습니다. 그러면 지난 판에 찍은 발자국이 다음 맵의 같은 월드 좌표에
            // 그대로 떠 있습니다. 씬과 함께 사라지게 두고, Acquire의 null 검사가
            // 다음 판에서 새로 만들게 합니다.
            var go = new GameObject("DuckTracks_" + key);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;

            // 반드시 월드입니다. 로컬이면 자국이 캐릭터를 따라다녀서
            // "지나간 자리에 남는다"가 성립하지 않습니다.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            main.startSpeed = 0f;
            main.gravityModifier = 0f;

            // 알갱이는 Emit으로만 넣습니다. 자동 방출은 전부 끕니다.
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var overLifetime = ps.colorOverLifetime;
            overLifetime.enabled = true;

            // 바닥에 눕는 판. 기본 Billboard는 화면을 향하기 때문에 탑다운에서
            // 발자국이 세워진 카드처럼 보입니다 — 발자국에서 가장 중요한 한 줄입니다.
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = 0;

            // 알갱이가 없어도 계속 돌고 있어야 Emit으로 넣은 것이 나이를 먹습니다.
            ps.Play();

            return new Handle { Go = go, Ps = ps, Renderer = renderer };
        }

        private static void Configure(Handle handle, TrackProfile profile, Material material)
        {
            var main = handle.Ps.main;
            // 지금 살아 있는 것보다 낮추지 않습니다. 낮추는 일은 Drain이
            // 하나씩 사라진 뒤에 맡습니다.
            main.maxParticles = Mathf.Max(CapacityOf(profile), handle.Ps.particleCount);
            main.startLifetime = LifeOf(profile);

            var overLifetime = handle.Ps.colorOverLifetime;
            overLifetime.color = BuildGradient(profile);

            handle.Renderer.sharedMaterial = material;

            // 야광 밝기는 시간에 따라 변하지 않습니다. 매 프레임 넣을 이유가 없습니다.
            material.SetColor(BaseColorId, TintOf(profile));
        }

        /// <summary>
        /// 깜박임과 야광을 매 프레임 먹입니다.
        ///
        /// <b>파도</b> — 깜박임은 시계가 아니라 <b>알갱이의 나이</b>로 계산합니다.
        /// 자국마다 찍힌 시각이 다르므로 위상이 저절로 어긋나고, 그 결과 걸어온 길을
        /// 따라 밝기의 물결이 흘러갑니다. 전부 같은 박자로 뛰면 그냥 화면이 껌뻑이는
        /// 것으로 보입니다.
        ///
        /// 위상이 나이에 비례하므로 시간이 흐르면 물결은 <b>새 자국 쪽</b>, 즉
        /// 플레이어를 쫓아 흘러갑니다.
        ///
        /// 알갱이 색을 직접 쓰기 때문에 수명과 무관합니다 — 그라디언트로 물결을 넣으면
        /// 키가 여덟 개뿐이라 수명당 서너 번이 한계고, 무한 지속에서는 주기가 며칠이
        /// 되어 아무 일도 안 일어납니다.
        ///
        /// <b>야광</b>은 알갱이마다 다를 이유가 없어서 재질 색에 한 번만 곱합니다.
        /// </summary>
        internal static void Tick()
        {
            foreach (var handle in Handles.Values)
            {
                var profile = handle.Profile;
                if (profile == null || handle.Ps == null)
                    continue;

                Drain(handle, profile);

                // 깜박임·색 순환은 알갱이 색을 다시 쓰는 일이고, 흘리기는 자국 자리를
                // 읽는 일입니다. 둘 다 안 쓰면 할 일이 없습니다. 야광 밝기는 시간에
                // 따라 변하지 않으므로 Configure에서 한 번만 넣습니다.
                bool wave = profile.pulse || profile.cycleHue;

                // 튀기기를 꺼 두면 흘릴 것도 없습니다 — 색도 모양도 거기서 옵니다.
                bool shed = profile.burst && profile.drift;

                if (!wave && !shed)
                {
                    handle.DriftCarry = 0f;
                    continue;
                }

                // 화면 밖이면 건너뜁니다.
                //
                // 알갱이를 전부 읽고 다시 쓰는 일이라 개수에 정비례합니다. 무한 지속으로
                // 맵 전체에 자국을 깔아 두면 지나온 자리까지 매 프레임 계산하게 되는데,
                // 안 보이는 것을 깜박이게 해 봐야 아무도 못 봅니다.
                //
                // isVisible은 <b>지난 프레임에 어느 카메라든 그렸는지</b>입니다. 이 통에
                // 담긴 자국 중 하나라도 화면에 있으면 참이므로, 보이는 것이 안 깜박이는
                // 일은 생기지 않습니다. 다시 보이기 시작한 첫 프레임만 한 박자 늦습니다.
                if (handle.Renderer != null && !handle.Renderer.isVisible)
                    continue;

                Animate(handle, profile, wave, shed);
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// 자국 수 상한을 <b>천천히</b> 낮춥니다.
        ///
        /// "계속 남기기"를 끄면 상한이 1만에서 몇백으로 떨어집니다. 그냥 낮추면
        /// 파티클 시스템이 넘치는 만큼을 <b>그 프레임에</b> 잘라 내서, 화면 가득하던
        /// 발자국이 순식간에 사라집니다. 자연스럽게 옅어지라고
        /// <see cref="RetimeToFinite"/>로 수명을 먹여 놓고는 그걸 무의미하게 만드는
        /// 셈입니다.
        ///
        /// 그래서 살아 있는 수가 목표 아래로 내려온 뒤에 낮춥니다. 그때까지는
        /// 오래된 것부터 제 수명대로 하나씩 사라집니다.
        /// </summary>
        private static void Drain(Handle handle, TrackProfile profile)
        {
            var main = handle.Ps.main;

            int target = CapacityOf(profile);
            if (main.maxParticles <= target)
                return;

            if (handle.Ps.particleCount > target)
                return;

            main.maxParticles = target;
        }

        /// <summary>
        /// 자국 수 상한.
        ///
        /// <b>설정 항목이 아닙니다.</b> 살아 있는 자국 수는 결국 <c>걸음 빈도 x 수명</c>으로
        /// 정해집니다. 걸음은 아무리 빨라야 초당 서너 번이라, 수명만 알면 필요한 자리가
        /// 저절로 나옵니다. 사용자에게 물어봐야 어차피 닿지 않는 숫자를 고르게 될 뿐입니다.
        ///
        /// 초당 8개는 넉넉한 여유입니다 — 달려도 그 절반이 안 나옵니다.
        /// </summary>
        private static int CapacityOf(TrackProfile profile)
        {
            if (profile.infiniteLife)
                return InfiniteStamps;

            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0.1f, profile.life) * 8f), 64, 2000);
        }

        /// <summary>
        /// 재질에 곱할 색. 흰색이 "그대로"입니다.
        ///
        /// 야광만 여기 들어갑니다. 1을 넘기면 색이 1.0 위로 올라가고, 화면 후처리에
        /// 블룸이 있으면 그 지점이 번져서 실제로 빛나 보입니다.
        /// </summary>
        private static Color TintOf(TrackProfile profile)
        {
            // 덮어쓰기에서 밝기를 올리면 색이 하얗게 뜨기만 하므로 가산에서만 씁니다.
            float level = profile.blend == TrackBlend.Additive
                ? Mathf.Max(0.05f, profile.glowIntensity)
                : 1f;

            return new Color(level, level, level, 1f);
        }

        /// <summary>흘리기의 초당 총량 상한. 자국이 많아도 여기서 막힙니다.</summary>
        private const float MaxDriftPerSecond = 120f;

        /// <summary>한 프레임에 흘릴 수 있는 최대 개수. 프레임이 크게 튈 때의 안전장치입니다.</summary>
        private const int MaxDriftPerFrame = 24;

        /// <summary>
        /// 살아 있는 자국을 한 번 읽어서, 색을 다시 쓰고(<paramref name="wave"/>)
        /// 알갱이를 흘립니다(<paramref name="shed"/>).
        ///
        /// 두 일을 <b>한 번의 읽기</b>로 처리합니다. 따로 돌면 자국 배열을 프레임마다
        /// 두 번 읽게 되는데, 무한 지속에서는 그게 수천 개짜리 복사입니다.
        /// 색을 안 바꿀 때는 되쓰기도 건너뜁니다.
        /// </summary>
        private static void Animate(Handle handle, TrackProfile profile, bool wave, bool shed)
        {
            var ps = handle.Ps;

            int capacity = ps.main.maxParticles;
            if (handle.Buffer == null || handle.Buffer.Length < capacity)
                handle.Buffer = new ParticleSystem.Particle[Mathf.Max(capacity, 16)];

            int count = ps.GetParticles(handle.Buffer);
            if (count <= 0)
                return;

            if (shed)
                Shed(handle, profile, count);

            if (!wave)
                return;

            float depth = Mathf.Clamp01(profile.pulseDepth);

            for (int i = 0; i < count; i++)
            {
                // 찍힌 뒤 흐른 시간(초). 무한 지속이어도 그대로 늘어납니다.
                float age = handle.Buffer[i].startLifetime - handle.Buffer[i].remainingLifetime;

                Color colour = profile.color;

                if (profile.cycleHue)
                    colour = Rotate(colour, Mathf.Repeat(age * profile.hueSpeed, 1f));

                if (profile.pulse)
                {
                    // 이름이 swing인 이유: 바깥에 깜박임을 쓸지 말지를 담은 bool wave가
                    // 있습니다. 같은 이름을 쓰면 가려집니다.
                    float swing = 0.5f + 0.5f * Mathf.Sin(age * profile.pulseSpeed * Mathf.PI * 2f);
                    float level = 1f - depth * (1f - swing);

                    colour.r *= level;
                    colour.g *= level;
                    colour.b *= level;
                }

                // 투명도는 건드리지 않습니다. 사라지는 것은 그라디언트가 맡습니다 —
                // 여기서 알파까지 쓰면 자국이 영영 안 사라집니다.
                colour.a = 1f;

                handle.Buffer[i].startColor = colour;
            }

            ps.SetParticles(handle.Buffer, count);
        }

        /// <summary>
        /// 살아 있는 자국 중 몇 군데를 골라 알갱이를 흘립니다.
        ///
        /// 자국마다 따로 세지 않고 <b>전체 예산</b>으로 굴립니다. 자국 하나당 초당
        /// <c>driftRate</c>개라고 하면 무한 지속에서 자국이 수천 개일 때 초당 수천 개가
        /// 나옵니다 — 은은하기는커녕 화면이 하얘집니다. 총량에 상한을 두고 그만큼을
        /// 무작위로 고른 자국에 나눠 줍니다.
        /// </summary>
        private static void Shed(Handle handle, TrackProfile profile, int count)
        {
            float perSecond = Mathf.Min(profile.driftRate * count, MaxDriftPerSecond);
            handle.DriftCarry += perSecond * Time.deltaTime;

            int emit = Mathf.Min(Mathf.FloorToInt(handle.DriftCarry), MaxDriftPerFrame);
            if (emit <= 0)
                return;

            handle.DriftCarry -= emit;

            for (int i = 0; i < emit; i++)
            {
                int pick = UnityEngine.Random.Range(0, count);
                StepBurstSystem.Drift(profile, handle.Buffer![pick].position);
            }
        }

        private static float LifeOf(TrackProfile profile)
        {
            return profile.infiniteLife ? InfiniteLife : Mathf.Max(0.1f, profile.life);
        }

        /// <summary>
        /// 시간에 따른 색.
        ///
        /// 찍히자마자 옅어지기 시작하면 자국이 약해 보입니다. 수명의 앞 60%는
        /// 그대로 두고 뒤에서만 지웁니다. 색조는 처음부터 끝까지 천천히 넘어갑니다.
        /// </summary>
        private static Gradient BuildGradient(TrackProfile profile)
        {
            var gradient = new Gradient();

            // 깜박임이나 색 순환이 켜져 있으면 색은 알갱이가 들고 있습니다.
            // 여기서도 색을 넣으면 두 번 곱해져서 어두워집니다. 이때 그라디언트는
            // 사라지는 모양(투명도)만 맡습니다.
            bool perParticle = profile.pulse || profile.cycleHue;

            Color fresh = perParticle ? Color.white : profile.color;
            Color faded = perParticle ? Color.white : profile.fadeColor;

            float freshAlpha = profile.color.a;
            float fadedAlpha = profile.fadeColor.a;

            // 무한 지속이면 색이 변하면 안 됩니다. 수명이 아주 길다고 해서 그라디언트를
            // 그대로 두면 아주 느리게 옅어지는데, "계속 남는다"고 해 놓고 조금씩
            // 사라지면 설정이 안 먹은 것으로 보입니다.
            if (profile.infiniteLife)
            {
                gradient.SetKeys(
                    new[] { new GradientColorKey(fresh, 0f), new GradientColorKey(fresh, 1f) },
                    new[] { new GradientAlphaKey(freshAlpha, 0f), new GradientAlphaKey(freshAlpha, 1f) });

                return gradient;
            }

            const float holdUntil = 0.6f;
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(fresh, 0f),
                    new GradientColorKey(faded, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(freshAlpha, 0f),
                    new GradientAlphaKey(freshAlpha, holdUntil),
                    new GradientAlphaKey(fadedAlpha, 1f),
                });

            return gradient;
        }

        /// <summary>
        /// 색조만 돌립니다. 채도와 명도, 투명도는 그대로 둡니다.
        ///
        /// 무채색(회색)은 돌려도 회색입니다. 색 순환을 켰는데 아무 일도 안 일어나면
        /// 대개 그 이유입니다 — 기본값인 진한 회색이 그렇습니다.
        /// </summary>
        private static Color Rotate(Color color, float shift)
        {
            if (shift <= 0f)
                return color;

            Color.RGBToHSV(color, out float h, out float sat, out float val);

            var rotated = Color.HSVToRGB(Mathf.Repeat(h + shift, 1f), sat, val);
            rotated.a = color.a;
            return rotated;
        }

        /// <summary>
        /// 매 프레임 파티클 설정을 다시 밀어 넣지 않으려고 씁니다.
        ///
        /// 크기·걸음 폭처럼 Emit 때마다 넘기는 값은 뺍니다 — 여기 들어가야 하는 것은
        /// 시스템 전체 설정(재질·수명·상한·그라디언트)뿐입니다.
        /// </summary>
        private static int ConfigHashOf(TrackProfile profile)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + profile.color.GetHashCode();
                hash = hash * 31 + profile.fadeColor.GetHashCode();
                hash = hash * 31 + profile.life.GetHashCode();
                hash = hash * 31 + (profile.infiniteLife ? 1 : 0);
                hash = hash * 31 + (profile.cycleHue ? 1 : 0);
                hash = hash * 31 + (profile.pulse ? 1 : 0);
                hash = hash * 31 + profile.glowIntensity.GetHashCode();
                return hash;
            }
        }

        /// <summary>지금 남아 있는 자국을 즉시 지웁니다.</summary>
        internal static void Clear()
        {
            foreach (var handle in Handles.Values)
            {
                if (handle.Ps != null)
                    handle.Ps.Clear();
            }
        }

        /// <summary>
        /// 남아 있는 자국을 <paramref name="seconds"/>에 걸쳐 사라지게 합니다.
        ///
        /// <see cref="Clear"/>처럼 한 프레임에 지우면 발자국이 <b>뚝</b> 끊깁니다.
        /// 남은 수명을 줄이되 <b>지금 색을 유지한 채</b> 줄여야 하는데, 남은 시간만
        /// 깎으면 알갱이가 그라디언트의 뒷부분으로 순간이동해서 밝기가 튑니다.
        ///
        /// 그래서 지금의 정규화된 나이 <c>t = 1 - 남은/전체</c>를 그대로 두고 남은
        /// 시간만 <paramref name="seconds"/>가 되도록 전체 수명을 다시 잡습니다
        /// (<c>전체 = seconds / (1 - t)</c>). 어느 자국이든 정확히 그 시간에 걸쳐
        /// 자기 색에서 투명까지 갑니다.
        /// </summary>
        internal static void FadeOut(float seconds)
        {
            seconds = Mathf.Max(0.05f, seconds);

            foreach (var handle in Handles.Values)
            {
                var ps = handle.Ps;
                if (ps == null || ps.particleCount <= 0)
                    continue;

                int capacity = ps.main.maxParticles;
                if (handle.Buffer == null || handle.Buffer.Length < capacity)
                    handle.Buffer = new ParticleSystem.Particle[Mathf.Max(capacity, 16)];

                int count = ps.GetParticles(handle.Buffer);

                for (int i = 0; i < count; i++)
                {
                    float start = handle.Buffer[i].startLifetime;
                    float remaining = handle.Buffer[i].remainingLifetime;

                    if (remaining <= seconds)
                        continue;

                    float aged = start <= 0f ? 0f : Mathf.Clamp01(1f - remaining / start);

                    handle.Buffer[i].startLifetime = seconds / Mathf.Max(1f - aged, 0.001f);
                    handle.Buffer[i].remainingLifetime = seconds;
                }

                ps.SetParticles(handle.Buffer, count);
            }
        }

        /// <summary>
        /// "계속 남기기"를 껐을 때, 이미 찍힌 자국에도 새 수명을 먹입니다.
        ///
        /// 안 하면 무한으로 찍힌 것들은 수명이 열 시간이라 사실상 영영 남습니다.
        ///
        /// <b>지나온 길 끝에서부터 지워지게</b> 합니다. 나이를 그대로 빼서 남은 시간을
        /// 정하면 새 수명보다 오래된 것이 전부 하한값에 뭉쳐서 한꺼번에 사라집니다 —
        /// 몇 분 걸어 놓은 뒤라면 거의 전부가 그렇습니다.
        ///
        /// 그래서 먼저 나이의 범위를 재고, 가장 오래된 것이 곧 사라지고 가장 새것이
        /// 새 수명을 다 쓰도록 그 사이에 고르게 폅니다. 결과적으로 발자국이 멀리서부터
        /// 플레이어 쪽으로 차례로 지워집니다.
        /// </summary>
        internal static void RetimeToFinite(TrackProfile profile)
        {
            float life = Mathf.Max(0.1f, profile.life);

            foreach (var handle in Handles.Values)
            {
                var ps = handle.Ps;
                if (ps == null || ps.particleCount <= 0 || handle.Profile != profile)
                    continue;

                int capacity = ps.main.maxParticles;
                if (handle.Buffer == null || handle.Buffer.Length < capacity)
                    handle.Buffer = new ParticleSystem.Particle[Mathf.Max(capacity, 16)];

                int count = ps.GetParticles(handle.Buffer);
                if (count <= 0)
                    continue;

                // 1) 나이의 범위. 이걸 알아야 어디에 펼지 정할 수 있습니다.
                float oldest = float.MinValue;
                float newest = float.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    float aged = handle.Buffer[i].startLifetime - handle.Buffer[i].remainingLifetime;

                    if (aged > oldest) oldest = aged;
                    if (aged < newest) newest = aged;
                }

                float span = Mathf.Max(oldest - newest, 0.0001f);

                // 2) 나이를 남은 시간으로 뒤집어 폅니다.
                for (int i = 0; i < count; i++)
                {
                    float aged = handle.Buffer[i].startLifetime - handle.Buffer[i].remainingLifetime;

                    // 가장 새것이 0, 가장 오래된 것이 1.
                    float oldness = Mathf.Clamp01((aged - newest) / span);

                    // 오래될수록 짧게 남깁니다.
                    handle.Buffer[i].startLifetime = life;
                    handle.Buffer[i].remainingLifetime = Mathf.Lerp(life, MinDrainTime, oldness);
                }

                ps.SetParticles(handle.Buffer, count);
            }
        }

        /// <summary>가장 오래된 자국에도 이만큼은 줍니다. 뚝 끊기지 않게 하려는 것입니다.</summary>
        private const float MinDrainTime = 0.4f;

        internal static void Dispose()
        {
            foreach (var handle in Handles.Values)
            {
                if (handle.Go != null)
                    UnityEngine.Object.Destroy(handle.Go);
            }

            Handles.Clear();
        }

        /// <summary>진단용 — 지금 살아 있는 자국 수.</summary>
        internal static int CountAlive()
        {
            int total = 0;

            foreach (var handle in Handles.Values)
            {
                if (handle.Ps != null)
                    total += handle.Ps.particleCount;
            }

            return total;
        }
    }
}
