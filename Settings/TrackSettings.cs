using System;
using Duckov.Options;
using DuckTracks.Systems;

namespace DuckTracks.Settings
{
    /// <summary>
    /// 전역 설정과 종류별 프로필.
    ///
    /// 켜고 끄는 값만 <see cref="OptionsManager"/>에 넣습니다. 색·크기 같은 프로필은
    /// 값이 많아서 JSON 파일로 따로 저장할 예정입니다 — WeaponAura가 같은 이유로
    /// 그렇게 나눠 두었습니다.
    /// </summary>
    public static class TrackSettings
    {
        private const string KeyEnabled = "DuckTracks_Enabled";

        /// <summary>발자국을 남길지 (기본: 켜짐)</summary>
        public static bool Enabled = true;

        /// <summary>도보 프로필.</summary>
        public static TrackProfile Foot = TrackProfile.DefaultFoot();

        /// <summary>바퀴 달린 탈것 프로필.</summary>
        public static TrackProfile Vehicle = TrackProfile.DefaultVehicle();

        /// <summary>바퀴가 아닌 탈것(말 등) 프로필.</summary>
        public static TrackProfile Mount = TrackProfile.DefaultMount();

        public static event Action? OnChanged;

        /// <summary>
        /// 이번 세션에서 사용자가 직접 바꿨는지.
        ///
        /// 씬이 바뀔 때마다 <see cref="Load"/>가 다시 불리는데, 그때 저장값으로
        /// 덮어쓰면 방금 창에서 바꾼 값이 조용히 되돌아갑니다.
        /// </summary>
        private static bool _setByUser;

        public static void Load()
        {
            if (_setByUser)
                return;

            try
            {
                Enabled = OptionsManager.Load(KeyEnabled, 1) != 0;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 설정 로드 실패: {ex.Message}");
            }
        }

        public static void SetEnabled(bool value)
        {
            _setByUser = true;

            if (Enabled == value)
                return;

            Enabled = value;

            try
            {
                OptionsManager.Save(KeyEnabled, value ? 1 : 0);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 설정 저장 실패: {ex.Message}");
            }

            OnChanged?.Invoke();
        }

        /// <summary>프로필 값을 창에서 바꾼 뒤 부릅니다.</summary>
        public static void NotifyChanged()
        {
            _setByUser = true;
            OnChanged?.Invoke();
        }
    }
}
