using System;
using HarmonyLib;
using UnityEngine;
using DuckTracks.UI;

namespace DuckTracks.Patches
{
    /// <summary>
    /// 모드 UI(오라 튜닝 패널 등)가 열려 있는 동안 플레이어의 발사 입력을 막습니다.
    ///
    /// IMGUI 창은 마우스 클릭을 GUI 쪽에서만 소비하고, 게임은 여전히 Input을 직접 읽기 때문에
    /// 슬라이더를 드래그하면 총이 나갑니다. 발사 입력이 모이는 지점인
    /// <c>CharacterMainControl.Trigger(bool, bool, bool)</c>에 Prefix를 걸어 플레이어에 한해 눌림 상태만 지웁니다.
    /// (메서드를 통째로 건너뛰지 않고 release는 통과시켜서 총기 상태가 눌린 채로 남지 않게 합니다.)
    /// </summary>
    public static class PlayerInputBlockPatch
    {
        private const string HarmonyId = "DuckTracks.PlayerInputBlock";

        private static Harmony? _harmony;
        private static bool _patchesApplied;

        /// <summary>
        /// 정지·입력 차단 패치가 걸려 있는지.
        ///
        /// 이게 false면 설정 창을 열어도 게임이 멈추지 않습니다 — 창은 떠 있고 커서도 보이는데
        /// 뒤에서 게임이 계속 돌아가는, 원인을 찾기 어려운 상태가 됩니다. 창을 열 때 확인합니다.
        /// </summary>
        public static bool IsApplied => _patchesApplied;

        /// <summary>
        /// 지금 플레이어 입력을 막아야 하는지 여부.
        /// 모드 창이 늘어나면 여기에 조건을 추가하면 됩니다.
        /// </summary>
        public static bool ShouldBlockInput
        {
            get
            {
                try
                {
                    return TrackWindowCanvas.IsShown;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 설정 창이 떠 있는 동안은 게임을 "정지 상태"로 위장합니다.
        /// 이렇게 해야 게임이 커서를 풀어 주고 조준·이동 입력을 스스로 멈춥니다.
        /// </summary>
        public static bool ShouldFakePause
        {
            get
            {
                try
                {
                    return TrackWindowCanvas.IsShown;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void ApplyPatches()
        {
            if (_patchesApplied)
                return;

            try
            {
                var harmony = new Harmony(HarmonyId);
                _harmony = harmony;

                ApplyAimAndPausePatches(harmony);

                // 아래 Trigger 패치를 못 붙이더라도 위 패치들은 이미 적용됐으므로
                // 해제 대상으로 표시해 둡니다.
                _patchesApplied = true;

                var triggerMethod = typeof(CharacterMainControl).GetMethod(
                    "Trigger",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool), typeof(bool), typeof(bool) },
                    null);

                if (triggerMethod == null)
                {
#if DEBUG
                    UnityEngine.Debug.LogWarning("[DuckTracks] CharacterMainControl.Trigger를 찾을 수 없어 입력 차단 패치를 건너뜁니다.");
#endif
                    return;
                }

                var prefix = typeof(PlayerInputBlockPatch).GetMethod(
                    nameof(TriggerPrefix),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (prefix == null)
                    return;

                harmony.Patch(triggerMethod, prefix: new HarmonyMethod(prefix));
                _patchesApplied = true;

#if DEBUG
                UnityEngine.Debug.Log("[DuckTracks] CharacterMainControl.Trigger 입력 차단 패치 적용 완료");
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckTracks] PlayerInputBlockPatch.ApplyPatches 오류: {ex.Message}");
            }
        }

        public static void RemovePatches()
        {
            if (!_patchesApplied)
                return;

            try
            {
                var harmony = _harmony ?? new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
                _harmony = null;
                _patchesApplied = false;

#if DEBUG
                UnityEngine.Debug.Log("[DuckTracks] 입력 차단 패치 제거 완료");
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] PlayerInputBlockPatch.RemovePatches 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 조준·정지 상태 관련 패치. 하나가 실패해도 나머지는 계속 적용합니다.
        /// (게임 버전에 따라 메서드가 없을 수 있어서 개별로 감쌉니다)
        /// </summary>
        private static void ApplyAimAndPausePatches(Harmony harmony)
        {
            // 마우스로 조준점이 계속 따라오는 문제 — 창이 열려 있으면 조준 갱신 자체를 막습니다.
            TryPatch(harmony, typeof(CharacterMainControl), "SetAimPoint",
                prefix: nameof(SetAimPointPrefix));

            // 게임이 스스로 커서를 풀고 입력을 멈추도록 "정지 중"이라고 알려 줍니다.
            TryPatch(harmony, typeof(PauseMenu), "get_Shown", postfix: nameof(ForceTruePostfix));
            TryPatch(harmony, typeof(GameManager), "get_Paused", postfix: nameof(ForceTruePostfix));
        }

        private static void TryPatch(Harmony harmony, Type target, string methodName,
            string? prefix = null, string? postfix = null)
        {
            try
            {
                var method = AccessTools.Method(target, methodName);
                if (method == null)
                {
                    UnityEngine.Debug.LogWarning($"[DuckTracks] {target.Name}.{methodName}을(를) 찾지 못해 건너뜁니다.");
                    return;
                }

                harmony.Patch(method,
                    prefix: prefix != null ? new HarmonyMethod(AccessTools.Method(typeof(PlayerInputBlockPatch), prefix)) : null,
                    postfix: postfix != null ? new HarmonyMethod(AccessTools.Method(typeof(PlayerInputBlockPatch), postfix)) : null);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] {target.Name}.{methodName} 패치 실패: {ex.Message}");
            }
        }

        /// <summary>창이 열려 있는 동안 플레이어의 조준점 갱신을 막습니다.</summary>
        private static bool SetAimPointPrefix(CharacterMainControl __instance)
        {
            try
            {
                if (!ShouldBlockInput)
                    return true;

                return __instance == null || __instance != CharacterMainControl.Main;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>정지 여부를 묻는 getter들이 창이 열린 동안 true를 돌려주게 합니다.</summary>
        private static void ForceTruePostfix(ref bool __result)
        {
            if (ShouldFakePause)
                __result = true;
        }

        /// <summary>
        /// 플레이어 캐릭터에 한해 트리거 눌림 입력을 지웁니다. AI 캐릭터는 건드리지 않습니다.
        /// </summary>
        private static void TriggerPrefix(CharacterMainControl __instance, ref bool trigger, ref bool triggerThisFrame)
        {
            try
            {
                if (!ShouldBlockInput)
                    return;

                if (__instance == null || __instance != CharacterMainControl.Main)
                    return;

                trigger = false;
                triggerThisFrame = false;
            }
            catch
            {
                // 입력 처리 중 예외가 게임을 멈추지 않도록 무시
            }
        }
    }
}
