using System;
using System.IO;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 종류별 프로필을 파일로 저장하고 읽습니다.
    ///
    /// <see cref="Duckov.Options.OptionsManager"/>에 넣지 않는 이유는 값이 많고
    /// 구조가 있기 때문입니다. 켜고 끄는 스위치 하나는 거기가 맞지만, 색 넷과
    /// 슬라이더 여덟 개를 키 하나씩 쪼개 넣으면 나중에 항목을 더할 때마다
    /// 마이그레이션을 해야 합니다.
    ///
    /// 저장 위치는 <b>사용자 데이터 폴더</b>입니다. 모드 폴더는 재설치·갱신 때
    /// 통째로 지워집니다 — 거기 두면 업데이트할 때마다 설정이 날아갑니다.
    /// </summary>
    internal static class TrackProfileStore
    {
        private const string FileName = "profiles.json";

        /// <summary>
        /// 저장 형식 판.
        ///
        /// 1은 프로필을 <b>중첩 객체</b>로 넣던 판입니다. 그 파일에는 <c>version</c>
        /// 말고 아무것도 들어 있지 않으므로(아래 참조) 옮겨 올 값이 없습니다.
        /// </summary>
        private const int CurrentVersion = 2;

        /// <summary>
        /// 파일에 담기는 모양.
        ///
        /// <b>프로필을 객체가 아니라 문자열로 담습니다.</b> Unity의 <c>JsonUtility</c>는
        /// 런타임에 올라온 모드 어셈블리의 타입을 <b>중첩 필드로는</b> 직렬화하지 못하고
        /// 조용히 버립니다. 판 1이 정확히 그 함정에 빠져 있었습니다 — 저장된 파일이
        /// <c>{ "version": 1 }</c> 스무 바이트였고, 프로필 셋이 통째로 없었습니다.
        /// 맵을 옮기거나 게임을 다시 켤 때 설정이 초기값으로 돌아가던 원인입니다.
        ///
        /// <see cref="ProfileJson"/>이 프로필 하나를 <b>맨 위 객체</b>로 직렬화해
        /// 문자열로 만들어 줍니다. 문자열은 Unity가 항상 아는 타입이라 안전하게 오갑니다.
        /// </summary>
        [Serializable]
        private sealed class Payload
        {
            public int version = CurrentVersion;

            public string foot = "";
            public string vehicle = "";
            public string mount = "";
        }

        /// <summary>
        /// 이미 읽었는지.
        ///
        /// 씬이 바뀔 때마다 <c>Awake</c>가 다시 불리는데, 그때 파일 값으로 덮어쓰면
        /// 창에서 방금 고쳐 놓고 아직 저장 안 한 값이 조용히 되돌아갑니다.
        /// </summary>
        private static bool _loaded;

        internal static void Load()
        {
            if (_loaded)
                return;

            _loaded = true;

            try
            {
                string path = FilePath();
                if (!File.Exists(path))
                    return;

                var payload = JsonUtility.FromJson<Payload>(File.ReadAllText(path));
                if (payload == null)
                    return;

                int applied = 0;
                applied += Apply(ProfileJson.OneFrom<TrackProfile>(payload.foot), Settings.TrackSettings.Foot);
                applied += Apply(ProfileJson.OneFrom<TrackProfile>(payload.vehicle), Settings.TrackSettings.Vehicle);
                applied += Apply(ProfileJson.OneFrom<TrackProfile>(payload.mount), Settings.TrackSettings.Mount);

                // 성공 로그를 남깁니다. 판 1의 저장 실패가 오래 눈에 띄지 않았던 이유가
                // 실패해도 조용했기 때문입니다 — 몇 개를 실제로 되살렸는지 찍어 둡니다.
                UnityEngine.Debug.Log(
                    $"[DuckTracks] 프로필 {applied}개를 불러왔습니다. (판 {payload.version})");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 프로필 로드 실패(기본값으로 갑니다): {ex.Message}");
            }
        }

        /// <summary>
        /// 읽은 값을 살아 있는 프로필에 옮깁니다.
        ///
        /// 객체를 통째로 갈아 끼우지 않는 이유는, 다른 곳이 이미 그 프로필을
        /// 참조하고 있기 때문입니다. 참조를 바꾸면 창에서 고친 값이 반영되지 않는
        /// 자리가 생깁니다.
        /// </summary>
        /// <returns>옮겼으면 1, 읽을 것이 없었으면 0.</returns>
        private static int Apply(TrackProfile? from, TrackProfile to)
        {
            if (from == null)
                return 0;

            to.enabled = from.enabled;
            to.kind = from.kind;
            to.shapeSource = from.shapeSource;
            to.textureName = from.textureName ?? "";
            to.autoSizeScale = from.autoSizeScale;
            to.color = from.color;
            to.fadeColor = from.fadeColor;
            to.blend = from.blend;
            to.size = from.size;
            to.stride = from.stride;
            to.spread = from.spread;
            to.life = from.life;
            to.infiniteLife = from.infiniteLife;
            to.angleJitter = from.angleJitter;
            to.runStrideScale = from.runStrideScale;
            to.pairGap = from.pairGap;
            to.glowIntensity = from.glowIntensity;
            to.pulse = from.pulse;
            to.pulseSpeed = from.pulseSpeed;
            to.pulseDepth = from.pulseDepth;
            to.cycleHue = from.cycleHue;
            to.hueSpeed = from.hueSpeed;
            to.burst = from.burst;
            to.burstCount = from.burstCount;
            to.burstSize = from.burstSize;
            to.burstSpeed = from.burstSpeed;
            to.burstGravity = from.burstGravity;
            to.burstLife = from.burstLife;
            to.drift = from.drift;
            to.driftRate = from.driftRate;
            to.driftScale = from.driftScale;
            to.driftRise = from.driftRise;
            to.burstColor = from.burstColor;
            to.burstTextureName = from.burstTextureName ?? "";

            return 1;
        }

        internal static void Save()
        {
            try
            {
                var payload = new Payload
                {
                    foot = ProfileJson.One(Settings.TrackSettings.Foot),
                    vehicle = ProfileJson.One(Settings.TrackSettings.Vehicle),
                    mount = ProfileJson.One(Settings.TrackSettings.Mount),
                };

                string path = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonUtility.ToJson(payload, true));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 프로필 저장 실패: {ex.Message}");
            }
        }

        private static string FilePath()
        {
            return Path.Combine(
                Path.Combine(Application.persistentDataPath, "DuckTracks"), FileName);
        }
    }
}
