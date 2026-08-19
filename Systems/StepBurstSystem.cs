using System;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 걸음 알갱이 — 두 갈래입니다.
    ///
    /// <list type="bullet">
    /// <item><b>튀기기</b> — 발을 디디는 순간 확 튀어 올랐다 떨어집니다.</item>
    /// <item><b>흘리기</b> — 바닥에 남아 있는 자국에서 계속 조금씩 떠오릅니다.</item>
    /// </list>
    ///
    /// 색과 모양은 같이 쓰고 움직임만 다릅니다. 그래야 둘 다 켰을 때 한 가족으로
    /// 보입니다.
    ///
    /// <b>파티클 시스템은 나눕니다.</b> 중력이 시스템 전체 설정이기 때문입니다 —
    /// 튀는 것은 떨어져야 하고(중력 1 근처) 흘리는 것은 떠올라야 해서(중력 0), 한 통에
    /// 담으면 둘 중 하나는 반드시 어색해집니다.
    ///
    /// 자국(<see cref="TrackStampSystem"/>)과도 나뉩니다. 그쪽은 바닥에 납작하게 눕고
    /// 움직이지 않습니다.
    /// </summary>
    internal static class StepBurstSystem
    {
        private sealed class Handle
        {
            internal GameObject Go = null!;
            internal ParticleSystem Ps = null!;
            internal ParticleSystemRenderer Renderer = null!;
            internal int ConfigHash;
        }

        private static Handle? _burst;
        private static Handle? _drift;

        // ── 튀기기 ──────────────────────────────────────────────────

        /// <summary>
        /// 한 걸음에 알갱이를 뿜습니다.
        /// </summary>
        /// <param name="position">발이 닿은 자리 (바닥에 붙은 좌표).</param>
        internal static void Burst(TrackProfile profile, Vector3 position)
        {
            if (!profile.burst || profile.burstCount <= 0)
                return;

            try
            {
                var handle = Acquire(ref _burst, "DuckTracks_StepBurst", profile, drifting: false);
                if (handle == null)
                    return;

                int count = Mathf.Clamp(profile.burstCount, 1, 48);
                float speed = Mathf.Max(0.05f, profile.burstSpeed);

                for (int i = 0; i < count; i++)
                {
                    // 옆으로 흩어지면서 위로 뜁니다. 위로만 쏘면 분수처럼 한 줄로
                    // 올라가고, 옆으로만 쏘면 바닥을 기어서 자국과 구분이 안 됩니다.
                    Vector2 side = UnityEngine.Random.insideUnitCircle;

                    var velocity = new Vector3(side.x, 0f, side.y) * speed * 0.55f;
                    velocity.y = speed * UnityEngine.Random.Range(0.55f, 1.25f);

                    handle.Ps.Emit(new ParticleSystem.EmitParams
                    {
                        // 바닥보다 살짝 위에서 시작합니다. 정확히 바닥에서 나오면
                        // 첫 프레임이 지형에 묻혀 보입니다.
                        position = position + Vector3.up * 0.03f,
                        velocity = velocity,
                        startSize = profile.burstSize * UnityEngine.Random.Range(0.7f, 1.3f),
                        startLifetime = profile.burstLife * UnityEngine.Random.Range(0.75f, 1.15f),
                        startColor = profile.burstColor,
                        applyShapeToPosition = false,
                    }, 1);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 알갱이 뿜기 실패: {ex.Message}");
            }
        }

        // ── 흘리기 ──────────────────────────────────────────────────

        /// <summary>
        /// 바닥에 남아 있는 자국 하나에서 알갱이를 하나 흘립니다.
        ///
        /// 어느 자국에서 흘릴지와 얼마나 자주 흘릴지는
        /// <see cref="TrackStampSystem"/>이 정합니다 — 살아 있는 자국의 자리를 들고
        /// 있는 쪽이 거기라서, 여기로 위치만 넘어옵니다.
        /// </summary>
        internal static void Drift(TrackProfile profile, Vector3 position)
        {
            try
            {
                var handle = Acquire(ref _drift, "DuckTracks_StepDrift", profile, drifting: true);
                if (handle == null)
                    return;

                // 자국 안쪽 아무 데서나 올라옵니다. 한 점에서만 나오면 분수가 됩니다.
                Vector2 spot = UnityEngine.Random.insideUnitCircle * profile.burstSize * 0.9f;

                // 옆으로는 거의 안 움직이고 천천히 떠오릅니다. "은은하게"의 정체가
                // 이 느린 상승입니다.
                var velocity = new Vector3(
                    UnityEngine.Random.Range(-0.06f, 0.06f),
                    Mathf.Max(0.01f, profile.driftRise) * UnityEngine.Random.Range(0.7f, 1.3f),
                    UnityEngine.Random.Range(-0.06f, 0.06f));

                handle.Ps.Emit(new ParticleSystem.EmitParams
                {
                    position = position + new Vector3(spot.x, 0.02f, spot.y),
                    velocity = velocity,
                    startSize = profile.burstSize * profile.driftScale * UnityEngine.Random.Range(0.7f, 1.3f),
                    startLifetime = profile.burstLife * UnityEngine.Random.Range(0.9f, 1.5f),
                    startColor = profile.burstColor,
                    applyShapeToPosition = false,
                }, 1);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 알갱이 흘리기 실패: {ex.Message}");
            }
        }

        // ── 공통 ────────────────────────────────────────────────────

        private static Handle? Acquire(ref Handle? slot, string name, TrackProfile profile, bool drifting)
        {
            // 씬이 바뀌면 통째로 사라집니다. Unity의 == 는 파괴된 객체도 null로
            // 쳐 주므로 이 검사로 잡힙니다.
            if (slot == null || slot.Go == null || slot.Ps == null)
                slot = Create(name, drifting);

            int hash = ConfigHashOf(profile, drifting);
            if (slot.ConfigHash != hash)
            {
                Configure(slot, profile, drifting);
                slot.ConfigHash = hash;
            }

            return slot;
        }

        private static Handle Create(string name, bool drifting)
        {
            // hideFlags를 걸지 않습니다 — DontSave가 들어 있어서 씬을 넘어가도
            // 살아남고, 지난 판의 알갱이가 다음 맵에 떠 있게 됩니다.
            var go = new GameObject(name);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;

            // 월드입니다. 로컬이면 알갱이가 캐릭터를 따라다닙니다.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = drifting ? DriftGradient() : BurstGradient();

            // 자국과 달리 화면을 향합니다. 공중에 뜬 알갱이를 눕혀 놓으면
            // 위에서 내려다보는 시점에서 납작하게 짓눌려 보입니다.
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // 알갱이가 없어도 돌고 있어야 Emit으로 넣은 것이 나이를 먹습니다.
            ps.Play();

            return new Handle { Go = go, Ps = ps, Renderer = renderer };
        }

        private static void Configure(Handle handle, TrackProfile profile, bool drifting)
        {
            var main = handle.Ps.main;

            if (drifting)
            {
                // 흘리는 쪽은 자국 수만큼 자리가 필요합니다. 초당 상한이 120개고
                // 수명이 몇 초라 이 정도면 넉넉합니다.
                main.maxParticles = 1200;
                main.startLifetime = Mathf.Max(0.05f, profile.burstLife * 1.5f);

                // 떠올라야 하므로 중력이 없습니다. 여기가 튀는 쪽과 갈리는 지점이고,
                // 시스템을 나눈 이유이기도 합니다.
                main.gravityModifier = 0f;
            }
            else
            {
                // 한 걸음에 최대 48개, 걸음은 초당 두어 번입니다. 넉넉히 잡아도
                // 수명이 짧아 실제로는 이보다 훨씬 적게 살아 있습니다.
                main.maxParticles = Mathf.Clamp(profile.burstCount * 40, 64, 2000);
                main.startLifetime = Mathf.Max(0.05f, profile.burstLife);

                // 튀어 올랐다 떨어져야 "뿅" 하고 튄 것으로 보입니다.
                main.gravityModifier = Mathf.Max(0f, profile.burstGravity);
            }

            // 모양은 둘이 같이 씁니다. 안 골랐으면 내장 점입니다.
            handle.Renderer.sharedMaterial = string.IsNullOrEmpty(profile.burstTextureName)
                ? TrackTextures.ResolveDotMaterial(profile.blend)
                : TrackTextures.ResolveMaterial(profile.burstTextureName, profile.blend);
        }

        /// <summary>튀는 알갱이: 또렷하게 나왔다가 뒤에서 옅어집니다.</summary>
        private static Gradient BurstGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                });

            return gradient;
        }

        /// <summary>
        /// 흘리는 알갱이: 스며 나오듯 <b>옅게 시작해서</b> 옅게 사라집니다.
        ///
        /// 튀는 것처럼 처음부터 또렷하면 자국에서 알갱이가 튀어나오는 것으로 보입니다.
        /// 은은하려면 나타나는 것도 보이지 않아야 합니다.
        /// </summary>
        private static Gradient DriftGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.3f),
                    new GradientAlphaKey(0f, 1f),
                });

            return gradient;
        }

        private static int ConfigHashOf(TrackProfile profile, bool drifting)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (drifting ? 1 : 0);
                hash = hash * 31 + profile.burstCount;
                hash = hash * 31 + profile.burstLife.GetHashCode();
                hash = hash * 31 + profile.burstGravity.GetHashCode();
                hash = hash * 31 + (int)profile.blend;
                hash = hash * 31 + (profile.burstTextureName?.GetHashCode() ?? 0);
                return hash;
            }
        }

        internal static void Clear()
        {
            if (_burst?.Ps != null)
                _burst.Ps.Clear();

            if (_drift?.Ps != null)
                _drift.Ps.Clear();
        }

        internal static void Dispose()
        {
            if (_burst?.Go != null)
                UnityEngine.Object.Destroy(_burst.Go);

            if (_drift?.Go != null)
                UnityEngine.Object.Destroy(_drift.Go);

            _burst = null;
            _drift = null;
        }
    }
}
