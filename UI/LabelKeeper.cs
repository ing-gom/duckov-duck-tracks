using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuckTracks.UI
{
    /// <summary>
    /// 복제한 게임 버튼의 글자를 우리 것으로 유지시킵니다.
    ///
    /// 게임 UI 텍스트에는 로컬라이제이션 컴포넌트가 붙어 있어서, 오브젝트가 활성화될 때
    /// 원본 키("게임으로 돌아가기")로 텍스트를 다시 써 버립니다. Instantiate 직후 한 번만
    /// 바꿔서는 소용이 없습니다.
    ///
    /// 그래서 (1) 복제본에 붙은 로컬라이제이션 계열 컴포넌트를 제거하고,
    /// (2) 그래도 누가 덮어쓰면 되돌리도록 이 감시자를 붙입니다.
    /// </summary>
    public class LabelKeeper : MonoBehaviour
    {
        private string _label = string.Empty;

        /// <summary>초반 몇 초만 감시하고 이후에는 스스로 멈춥니다 (매 프레임 비용 제거).</summary>
        private float _watchUntil;

        public static void Attach(GameObject root, string label)
        {
            StripLocalizers(root);

            var keeper = root.GetComponent<LabelKeeper>();
            if (keeper == null)
                keeper = root.AddComponent<LabelKeeper>();

            keeper._label = label;
            keeper._watchUntil = Time.unscaledTime + 5f;
            keeper.Apply();
        }

        /// <summary>
        /// 복제본에서 <b>텍스트를 덮어쓰는 컴포넌트</b>와 <b>원래 클릭 동작</b>을 떼어냅니다.
        ///
        /// - 게임의 텍스트 로컬라이저는 <c>TextLocalizor</c>입니다. 클래스 이름이 버전마다
        ///   달라질 수 있어서 타입 참조 대신 이름에 "Local"이 들어가는지로 거릅니다.
        /// - 일시정지 메뉴 버튼은 Button.onClick을 쓰지 않고 <c>FadeGroupButton</c>,
        ///   <c>UIPanelButton_OpenChildPanel</c>처럼 IPointerClickHandler를 직접 구현합니다.
        ///   그래서 onClick만 비우면 복제 버튼이 여전히 "게임으로 돌아가기"를 실행합니다.
        ///   Selectable(=Button)이 아닌 포인터 핸들러는 모두 제거합니다.
        /// </summary>
        private static void StripLocalizers(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component is LabelKeeper)
                    continue;

                string typeName = component.GetType().Name;
                string? reason = null;

                if (typeName.IndexOf("Local", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    reason = "텍스트 덮어쓰기";
                }
                else if (component is UnityEngine.EventSystems.IPointerClickHandler &&
                         !(component is Selectable))
                {
                    // Button/Toggle 같은 Selectable은 남겨 둡니다 (호버·눌림 연출이 여기 있습니다)
                    reason = "원래 클릭 동작";
                }

                if (reason != null)
                {
                    UnityEngine.Debug.Log($"[DuckTracks] 버튼 복제본에서 '{typeName}' 제거 ({reason} 방지)");
                    Destroy(component);
                }
            }
        }

        private void OnEnable()
        {
            // 활성화될 때가 덮어쓰기가 가장 잘 일어나는 시점입니다.
            _watchUntil = Time.unscaledTime + 2f;
            Apply();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime > _watchUntil)
            {
                enabled = false;
                return;
            }

            Apply();
        }

        private void Apply()
        {
            if (string.IsNullOrEmpty(_label))
                return;

            foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text != null && text.text != _label)
                    text.text = _label;
            }

            foreach (var text in GetComponentsInChildren<Text>(true))
            {
                if (text != null && text.text != _label)
                    text.text = _label;
            }
        }
    }
}
