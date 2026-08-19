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

        /// <summary>파일에 담기는 모양. 프로필 셋을 한 덩어리로 넣습니다.</summary>
        [Serializable]
        private sealed class Payload
        {
            /// <summary>
            /// 저장 형식 판. 나중에 항목이 바뀌었을 때 옛 파일을 알아보려고 둡니다.
            /// </summary>
            public int version = 1;

            public TrackProfile? foot;
            public TrackProfile? vehicle;
            public TrackProfile? mount;
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

                Apply(payload.foot, Settings.TrackSettings.Foot);
                Apply(payload.vehicle, Settings.TrackSettings.Vehicle);
                Apply(payload.mount, Settings.TrackSettings.Mount);
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
        private static void Apply(TrackProfile? from, TrackProfile to)
        {
            if (from == null)
                return;

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
        }

        internal static void Save()
        {
            try
            {
                var payload = new Payload
                {
                    foot = Settings.TrackSettings.Foot,
                    vehicle = Settings.TrackSettings.Vehicle,
                    mount = Settings.TrackSettings.Mount,
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
