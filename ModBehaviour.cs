using System;
using Ducky.Sdk.ModBehaviours;
using DuckTracks.Helpers;
using DuckTracks.Settings;
using DuckTracks.Patches;
using DuckTracks.Systems;
using DuckTracks.UI;
using UnityEngine;

namespace DuckTracks
{
    /// <summary>
    /// DuckTracks 진입점.
    ///
    /// 지나간 자리에 발자국을 남깁니다. 게임 메서드를 가로채지 않고, 매 프레임
    /// 캐릭터 위치를 읽어서 걸은 거리만큼 자국을 찍습니다.
    /// </summary>
    public class ModBehaviour : ModBehaviourBase
    {
        private void Awake()
        {
            // 씬이 바뀌면 파티클 오브젝트가 통째로 사라지고, 지난 판의 걸음 기록은
            // 파괴된 캐릭터를 가리킵니다. 여기서 털고 갑니다.
            TrackSystem.ResetAll();

            TrackSettings.Load();
            TrackProfileStore.Load();
        }

        /// <summary>
        /// 각 단계를 따로 감쌉니다. 앞에서 예외 하나가 나도 뒤가 통째로 건너뛰어지지
        /// 않게 하려는 것입니다 — 설정 파일 하나 못 읽은 것 때문에 발자국이 아예 안
        /// 나오면 원인을 찾기 어렵습니다.
        /// </summary>
        protected override void ModEnabled()
        {
            // 1순위 — 이게 빠지면 설정 창이 떠도 게임이 멈추지 않습니다.
            Step("입력 차단 패치", PlayerInputBlockPatch.ApplyPatches);

            // 일시정지 메뉴에 "발자국 설정" 버튼 추가
            Step("일시정지 메뉴 버튼", PauseMenuButton.Install);

            Step("설정 로드", () =>
            {
                TrackSettings.Load();
                TrackProfileStore.Load();
            });

            // 씬이 바뀌면 ModEnabled가 다시 불릴 수 있습니다. 그냥 += 하면 구독이
            // 쌓여서 설정 한 번 바꿀 때마다 여러 번 반응합니다. 먼저 떼고 붙입니다.
            Step("설정 변경 구독", () =>
            {
                TrackSettings.OnChanged -= OnSettingsChanged;
                TrackSettings.OnChanged += OnSettingsChanged;
            });

#if DEBUG
            UnityEngine.Debug.Log("[DuckTracks] 모드 활성화");
#endif
        }

        protected override void ModDisabled()
        {
            Step("설정 변경 구독 해제", () => TrackSettings.OnChanged -= OnSettingsChanged);
            Step("설정 저장", TrackProfileStore.Save);
            Step("자국 정리", TrackSystem.Dispose);
            Step("입력 차단 패치 해제", PlayerInputBlockPatch.RemovePatches);
            Step("일시정지 메뉴 버튼 해제", PauseMenuButton.Uninstall);
            Step("설정 창 정리", () => TrackWindowCanvas.Instance.Dispose());
        }

        private void Update()
        {
            try
            {
                TrackSystem.Tick();

                // 깜박임과 야광은 시간에 따라 변하므로 매 프레임 먹입니다.
                TrackStampSystem.Tick();

                // 일시정지 메뉴가 떠 있으면 버튼이 붙어 있는지 확인 (0.5초 간격)
                PauseMenuButton.Tick();

#if DEBUG
                HandleProbeHotkeys();
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[DuckTracks] Update 오류(무시됨): {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// 발 실루엣 되읽기는 여기서 합니다.
        ///
        /// 카메라가 그 프레임에 그린 결과를 읽어야 하므로 Update에서는 이릅니다.
        /// </summary>
        private void LateUpdate()
        {
            try
            {
                TrackSystem.LateTick();
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[DuckTracks] LateUpdate 오류(무시됨): {ex.Message}");
#endif
            }
        }

#if DEBUG
        /// <summary>
        /// 진단 단축키. 배포 빌드에는 들어가지 않습니다.
        ///
        /// F9  — 캐릭터 모델 계층 전체
        /// F10 — 바닥에 가까운 렌더러 순
        ///
        /// 결과는 게임 로그에 있습니다:
        /// %USERPROFILE%\AppData\LocalLow\TeamSoda\Duckov\Player.log
        /// </summary>
        private static void HandleProbeHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.F9))
                CharacterModelProbe.Dump();

            if (Input.GetKeyDown(KeyCode.F10))
                CharacterModelProbe.DumpLowest();
        }
#endif

        /// <summary>
        /// 설정이 바뀌었을 때.
        ///
        /// <b>바닥에 있는 자국을 지우지 않습니다.</b> 색과 재질은 파티클 시스템 전체에
        /// 걸리기 때문에, 이미 찍힌 것들도 다음 프레임에 새 설정으로 그려집니다.
        /// 지울 이유가 없었습니다 — 슬라이더를 만질 때마다 지나온 자국이 통째로
        /// 사라지는 편이 오히려 이상합니다.
        ///
        /// 예외가 둘입니다.
        /// <list type="bullet">
        /// <item>모드를 껐을 때 — 뚝 끊지 않고 짧게 옅어지며 사라집니다.</item>
        /// <item>"계속 남기기"를 껐을 때 — 수명이 백만 초로 박힌 것들에 새 수명을
        /// 먹입니다. 안 하면 영영 남습니다.</item>
        /// </list>
        /// </summary>
        private static void OnSettingsChanged()
        {
            if (!TrackSettings.Enabled)
            {
                TrackStampSystem.FadeOut(1.2f);
                StepBurstSystem.Clear();
                return;
            }

            if (!TrackSettings.Foot.infiniteLife)
                TrackStampSystem.RetimeToFinite(TrackSettings.Foot);
        }

        /// <summary>한 단계가 실패해도 나머지는 계속 진행합니다.</summary>
        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckTracks] {name} 실패(나머지는 계속 진행): {ex}");
            }
        }
    }
}
