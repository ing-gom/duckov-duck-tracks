using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ducky.Sdk.Localizations;

namespace DuckTracks.UI
{
    /// <summary>
    /// 일시정지 메뉴에 "무기 오라" 버튼을 끼워 넣습니다.
    ///
    /// PauseMenu가 public static 이벤트(onPauseMenuOn)를 노출하므로 Harmony 패치 없이 붙습니다.
    /// 버튼은 기존 버튼을 복제해서 만들기 때문에 게임 테마(폰트·모서리·호버)를 그대로 따라갑니다.
    /// </summary>
    public static class PauseMenuButton
    {
        private const string ButtonName = "DuckTracks_OpenButton";

        /// <summary>메뉴에 표시할 이름 (게임 언어를 따라갑니다)</summary>
        private static string ButtonLabel => L.Menu.OpenButton;

        private const float CheckInterval = 0.5f;

        private static float _nextCheckTime;

        public static void Install()
        {
            _nextCheckTime = 0f;
        }

        public static void Uninstall()
        {
            _nextCheckTime = float.MaxValue;
        }

        /// <summary>
        /// ModBehaviour.Update에서 주기적으로 호출합니다.
        ///
        /// 처음에는 PauseMenu.onPauseMenuOn 이벤트에 붙였는데 한 번도 불리지 않았습니다.
        /// 그 이벤트는 정적 헬퍼 Show()에서만 발생하고, 게임은 Instance.Open()을 직접 부르는
        /// 경로를 쓰기 때문입니다. 그래서 메뉴가 떠 있는지를 직접 확인하는 방식으로 바꿨습니다.
        /// (어떤 경로로 열리든 동작합니다)
        /// </summary>
        public static void Tick()
        {
            if (Time.time < _nextCheckTime)
                return;

            _nextCheckTime = Time.time + CheckInterval;

            try
            {
                // 메뉴가 "보일 때"를 기다리지 않습니다.
                // PauseMenu.Instance는 null이고 Shown도 false로 나와서 한 번도 진입하지 못했습니다.
                // 메뉴 오브젝트는 숨어 있어도 씬에 존재하므로, 찾는 즉시 버튼을 붙여 둡니다.
                var menu = FindPauseMenu();
                if (menu == null)
                {
                    LogOnce("menu-null", "[DuckTracks] 씬에서 PauseMenu를 찾지 못했습니다.");
                    return;
                }

                // 계층 전체에서 찾습니다. Transform.Find는 직계 자식만 보기 때문에
                // 컨테이너 안에 만들어 둔 버튼을 못 찾고 매번 새로 만들게 됩니다.
                if (FindExistingButton(menu.transform) != null)
                    return;

                var template = FindTemplateButton(menu.transform);
                if (template == null)
                {
                    LogOnce("template-null",
                        $"[DuckTracks] 버튼 템플릿을 찾지 못했습니다. " +
                        $"(PauseMenu='{menu.gameObject.name}', 자식 {menu.transform.childCount}개, " +
                        $"Button {menu.GetComponentsInChildren<Button>(true).Length}개)");
                    return;
                }

                CreateButton(template);
                UnityEngine.Debug.Log(
                    $"[DuckTracks] 일시정지 메뉴에 '{ButtonLabel}' 버튼을 추가했습니다. " +
                    $"(템플릿='{template.gameObject.name}')");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 일시정지 버튼 추가 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 씬에 있는 PauseMenu를 찾습니다 (비활성 포함).
        /// PauseMenu.Instance(=GameManager.PauseMenu)는 상황에 따라 null이라 믿을 수 없습니다.
        /// </summary>
        internal static PauseMenu? FindPauseMenu()
        {
            var instance = PauseMenu.Instance;
            if (instance != null)
                return instance;

            // Resources.FindObjectsOfTypeAll은 비활성 오브젝트도 찾습니다.
            foreach (var candidate in Resources.FindObjectsOfTypeAll<PauseMenu>())
            {
                if (candidate == null)
                    continue;

                // 프리팹 애셋이 아니라 씬에 올라온 인스턴스만
                if (candidate.gameObject.scene.IsValid())
                    return candidate;
            }

            return null;
        }

        private static readonly System.Collections.Generic.HashSet<string> _loggedKeys =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>같은 메시지를 매 0.5초마다 반복하지 않도록 한 번만 남깁니다.</summary>
        private static void LogOnce(string key, string message)
        {
            if (_loggedKeys.Add(key))
                UnityEngine.Debug.LogWarning(message);
        }

        private static Transform? FindExistingButton(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.gameObject.name == ButtonName)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// 복제할 기존 버튼을 고릅니다.
        ///
        /// 계층 전체에서 그냥 첫 버튼을 고르면 확인 대화상자처럼 파묻힌 하위 패널의 버튼이
        /// 뽑힐 수 있습니다. 그래서 <b>버튼이 가장 많이 모여 있는 부모</b>(= 메인 버튼 열)를
        /// 먼저 찾고, 그 안에서 가장 위쪽 버튼을 템플릿으로 씁니다.
        /// </summary>
        private static Button? FindTemplateButton(Transform menuRoot)
        {
            var groups = new System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.List<Button>>();

            foreach (var button in menuRoot.GetComponentsInChildren<Button>(true))
            {
                if (button == null || button.gameObject.name == ButtonName)
                    continue;

                var parent = button.transform.parent;
                if (parent == null)
                    continue;

                if (!groups.TryGetValue(parent, out var list))
                {
                    list = new System.Collections.Generic.List<Button>();
                    groups[parent] = list;
                }
                list.Add(button);
            }

            System.Collections.Generic.List<Button>? bestGroup = null;
            foreach (var pair in groups)
            {
                if (bestGroup == null || pair.Value.Count > bestGroup.Count)
                    bestGroup = pair.Value;
            }

            if (bestGroup == null || bestGroup.Count == 0)
                return null;

            Button best = bestGroup[0];
            foreach (var button in bestGroup)
            {
                if (button.transform.GetSiblingIndex() < best.transform.GetSiblingIndex())
                    best = button;
            }

            return best;
        }

        private static void CreateButton(Button template)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = ButtonName;

            // 원본 바로 아래에 배치 (메뉴 순서 유지)
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

            // 로컬라이제이션 컴포넌트를 떼어내고 글자를 고정합니다.
            // (그냥 text만 바꾸면 활성화될 때 원본 키로 되돌아갑니다)
            LabelKeeper.Attach(clone, ButtonLabel);

            // 원본 버튼의 동작이 Button이 아니라 FadeGroupButton 쪽에 있었다면
            // 위 정리 과정에서 그게 사라지고 Button이 없을 수도 있습니다.
            var button = clone.GetComponent<Button>();
            if (button == null)
                button = clone.AddComponent<Button>();

            // 클릭을 받으려면 레이캐스트 대상이 될 그래픽이 필요합니다.
            if (clone.GetComponent<Graphic>() == null)
            {
                var hitbox = clone.AddComponent<Image>();
                hitbox.color = new Color(0f, 0f, 0f, 0f);
                hitbox.raycastTarget = true;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OpenPanel);

            clone.SetActive(true);
        }

        private static void OpenPanel()
        {
            // 인게임 스타일 설정 창을 엽니다.
            // (IMGUI 패널은 Ctrl+Shift+F8 진단용으로 따로 남겨 둡니다)
            TrackWindowCanvas.Instance.Show();
        }
    }
}
