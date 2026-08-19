using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuckTracks.UI
{
    /// <summary>
    /// 기본 uGUI 부품 생성기.
    ///
    /// 게임에는 재사용할 만한 공용 UI 빌더가 없고, 게임의 프리팹은 옵션 화면이 한 번은
    /// 열려야 메모리에 올라옵니다. 그래서 직접 조립합니다 — 대신 색·글꼴은 게임 것을
    /// 따라가게 해서 겉돌지 않도록 합니다.
    /// </summary>
    internal static class UiKit
    {
        internal static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.13f, 0.97f);
        internal static readonly Color SectionColor = new Color(1f, 1f, 1f, 0.045f);
        internal static readonly Color ButtonColor = new Color(0.20f, 0.23f, 0.30f, 1f);
        internal static readonly Color AccentColor = new Color(0.28f, 0.45f, 0.68f, 1f);
        internal static readonly Color TextColor = new Color(0.92f, 0.94f, 0.97f, 1f);
        internal static readonly Color MutedColor = new Color(0.62f, 0.66f, 0.73f, 1f);

        /// <summary>게임이 쓰는 글꼴. 한글이 들어가므로 이걸 못 찾으면 네모가 납니다.</summary>
        private const string PreferredFontName = "Jua";

        private static TMP_FontAsset? _font;

        /// <summary>
        /// 게임 글꼴을 찾아 둡니다. 못 찾으면 TMP 기본 글꼴로 갑니다.
        ///
        /// <see cref="Resources.FindObjectsOfTypeAll{T}"/>를 쓰는 이유는, 어느 씬에도
        /// 안 붙어 있는 애셋까지 훑어야 하기 때문입니다.
        /// </summary>
        internal static TMP_FontAsset? Font()
        {
            if (_font != null)
                return _font;

            try
            {
                foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (asset == null)
                        continue;

                    if (asset.name.IndexOf(PreferredFontName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _font = asset;
                        return _font;
                    }
                }
            }
            catch
            {
                // 못 찾아도 기본 글꼴로 그립니다.
            }

            return _font;
        }

        // ── 부품 ────────────────────────────────────────────────────

        internal static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static Image MakeImage(string name, Transform parent, Color color)
        {
            var rect = MakeRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static TextMeshProUGUI MakeText(
            Transform parent, string content, int size, Color color, TextAlignmentOptions alignment)
        {
            var rect = MakeRect("Text", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            var font = Font();
            if (font != null)
                text.font = font;

            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;

            // Overflow가 아니라 Truncate입니다. 넘치도록 두면 rect 밖으로 그려지고,
            // 스크롤 영역의 RectMask2D가 그 부분을 잘라서 좌우가 잘려 보입니다.
            text.overflowMode = TextOverflowModes.Truncate;
            text.enableWordWrapping = false;
            return text;
        }

        internal static Button MakeButton(Transform parent, string label, float width, Action onClick)
        {
            return MakeButton(parent, label, width, onClick, ButtonColor);
        }

        internal static Button MakeButton(
            Transform parent, string label, float width, Action onClick, Color color)
        {
            var image = MakeImage("Button", parent, color);
            var rect = image.rectTransform;

            if (width > 0f)
                SetWidth(rect, width);
            else
                rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // 게임 버튼처럼 살짝 밝아지는 정도만.
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;

            button.onClick.AddListener(() => onClick());

            var text = MakeText(rect, label, 19, TextColor, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.margin = new Vector4(10f, 2f, 10f, 2f);

            // 언어마다 글자 길이가 크게 다릅니다. 안 줄이면 긴 쪽에서 잘립니다.
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 19f;

            return button;
        }

        /// <summary>버튼 글자를 나중에 바꾸려면 이걸로 꺼냅니다.</summary>
        internal static TextMeshProUGUI? LabelOf(Button button)
        {
            return button.GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>
        /// 게임 옵션 슬라이더와 같은 모양(가는 홈 + 채움 + 손잡이).
        /// </summary>
        internal static Slider MakeSlider(Transform parent, float min, float max)
        {
            var root = MakeRect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();

            var background = MakeImage("Background", root, new Color(0f, 0f, 0f, 0.55f));
            var bgRect = background.rectTransform;
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(1f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(0f, 8f);
            bgRect.anchoredPosition = Vector2.zero;

            var fillArea = MakeRect("FillArea", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.sizeDelta = new Vector2(-18f, 8f);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = MakeImage("Fill", fillArea, AccentColor * 1.4f);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.sizeDelta = new Vector2(18f, 0f);

            var handleArea = MakeRect("HandleArea", root);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.sizeDelta = new Vector2(-18f, 0f);
            handleArea.anchoredPosition = Vector2.zero;

            var handle = MakeImage("Handle", handleArea, new Color(0.92f, 0.96f, 1f, 1f));
            var handleRect = handle.rectTransform;
            handleRect.sizeDelta = new Vector2(18f, 22f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;

            return slider;
        }

        /// <summary>
        /// 라벨 + 슬라이더 + 값 표시가 한 줄에 놓인 흔한 조합.
        /// </summary>
        /// <param name="format">값 표시 서식. 예: <c>"{0:F2}m"</c></param>
        internal static Slider MakeSliderRow(
            Transform parent, string label, float min, float max, float value,
            string format, Action<float> onChanged)
        {
            var row = MakeRect("SliderRow", parent);
            SetHeight(row, 34f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var caption = MakeText(row, label, 18, MutedColor, TextAlignmentOptions.MidlineLeft);
            SetWidth(caption.rectTransform, 260f);
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 13f;
            caption.fontSizeMax = 18f;

            var slider = MakeSlider(row, min, max);
            var element = slider.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minWidth = 120f;

            var readout = MakeText(row, "", 18, TextColor, TextAlignmentOptions.MidlineRight);
            SetWidth(readout.rectTransform, 96f);

            slider.value = Mathf.Clamp(value, min, max);
            readout.text = string.Format(format, slider.value);

            slider.onValueChanged.AddListener(v =>
            {
                readout.text = string.Format(format, v);
                onChanged(v);
            });

            return slider;
        }

        /// <summary>
        /// 라벨이 붙은 체크 상자.
        ///
        /// uGUI의 <see cref="Toggle"/>을 쓰지 않고 버튼으로 만듭니다 — Toggle은 체크
        /// 표시 그래픽을 자식으로 따로 물려야 하고, 그 자식이 켜짐/꺼짐에 따라 통째로
        /// 켜졌다 꺼졌다 하는 구조라 레이아웃 안에서 높이가 흔들립니다.
        /// </summary>
        internal static Button MakeCheckRow(
            Transform parent, string label, bool value, Action<bool> onChanged)
        {
            var row = MakeRow(parent, 36f);

            var box = MakeImage("Box", row, new Color(0f, 0f, 0f, 0.45f));
            SetWidth(box.rectTransform, 30f);

            // 체크 표시를 <b>글자로 찍지 않습니다.</b> 게임 글꼴(Jua)에 ✔가 없으면
            // 빈 네모가 그려집니다 — 글꼴에 없는 글자를 그릴 때 나오는 그 네모입니다.
            // 안쪽에 작은 판을 하나 두고 켰다 껐다 하면 글꼴과 무관합니다.
            var mark = MakeImage("Mark", box.transform, AccentColor);
            Stretch(mark.rectTransform);
            mark.rectTransform.offsetMin = new Vector2(6f, 6f);
            mark.rectTransform.offsetMax = new Vector2(-6f, -6f);
            mark.raycastTarget = false;
            mark.gameObject.SetActive(value);

            var caption = MakeText(row, label, 18, MutedColor, TextAlignmentOptions.MidlineLeft);
            caption.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box;

            bool state = value;
            button.onClick.AddListener(() =>
            {
                state = !state;
                mark.gameObject.SetActive(state);
                onChanged(state);
            });

            // 글자를 눌러도 켜지게 합니다. 30px 상자만 눌러야 하면 답답합니다.
            var capButton = caption.gameObject.AddComponent<Button>();
            caption.raycastTarget = true;
            capButton.targetGraphic = caption;
            capButton.transition = Selectable.Transition.None;
            capButton.onClick.AddListener(() => button.onClick.Invoke());

            return button;
        }

        /// <summary>
        /// 한 덩어리로 묶이는 영역. 제목이 붙고 안쪽에 세로로 쌓입니다.
        /// </summary>
        internal static RectTransform MakeSection(Transform parent, string title)
        {
            var holder = MakeImage("Section", parent, SectionColor).rectTransform;

            var layout = holder.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(14, 14, 12, 14);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // ContentSizeFitter를 달지 않습니다.
            //
            // 이 섹션은 부모 VerticalLayoutGroup이 높이를 정해 주는 자식입니다. 거기에
            // 자기 높이를 스스로 정하는 부품을 또 달면 둘이 서로를 밀어내며 흔들립니다.
            // VerticalLayoutGroup 자체가 preferredHeight를 보고하므로 부모가 알아서 맞춥니다.

            if (!string.IsNullOrEmpty(title))
            {
                var caption = MakeText(holder, title, 21, TextColor, TextAlignmentOptions.MidlineLeft);
                SetHeight(caption.rectTransform, 28f);
            }

            return holder;
        }

        /// <summary>가로로 늘어놓는 줄.</summary>
        internal static RectTransform MakeRow(Transform parent, float height, float spacing = 8f)
        {
            var row = MakeRect("Row", parent);
            SetHeight(row, height);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // <b>반드시 false여야 합니다.</b>
            //
            // true면 남는 폭을 자식 <b>전부에게</b> 똑같이 나눠 줍니다. flexibleWidth를
            // 0으로 박아 둬도 소용이 없어서, 30px짜리 체크 상자가 줄 절반을 차지하고
            // 240px 버튼이 640px로 늘어납니다.
            //
            // 대신 늘어나야 하는 것은 스스로 flexibleWidth를 1로 신청합니다
            // (폭을 안 준 버튼, 늘어나는 라벨, 빈칸).
            layout.childForceExpandWidth = false;

            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            return row;
        }

        internal static TextMeshProUGUI MakeHint(Transform parent, string text)
        {
            var hint = MakeText(parent, text, 16, MutedColor, TextAlignmentOptions.TopLeft);

            // 힌트는 길어서 접혀야 합니다. 다만 <b>넘치게</b> 두면 rect 밖으로 그려지고
            // 스크롤 영역의 마스크가 그 부분을 잘라냅니다 — 접되 넘치지는 않게 합니다.
            hint.enableWordWrapping = true;
            hint.overflowMode = TextOverflowModes.Truncate;

            // 여기도 ContentSizeFitter를 쓰지 않습니다(섹션과 같은 이유). 대신 두 줄
            // 높이를 잡아 둡니다 — 접힌 글의 높이를 알려면 폭이 먼저 정해져야 하는데,
            // 폭은 부모가 정하고 부모는 높이를 알아야 해서 서로를 기다립니다.
            var element = hint.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 24f;
            element.preferredHeight = 46f;
            element.flexibleHeight = 0f;

            return hint;
        }

        // ── 레이아웃 보조 ───────────────────────────────────────────

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static void SetHeight(RectTransform rect, float height)
        {
            var element = rect.gameObject.GetComponent<LayoutElement>();
            if (element == null)
                element = rect.gameObject.AddComponent<LayoutElement>();

            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;
        }

        /// <summary>
        /// 폭을 고정합니다.
        ///
        /// <b>minWidth는 건드리지 않습니다.</b> 최소폭까지 박아 두면 자식들의 최소폭 합이
        /// 줄 너비를 넘을 때 레이아웃이 줄이지 못하고 그대로 넘칩니다. 넘친 부분은
        /// 스크롤 영역의 <see cref="RectMask2D"/>가 잘라내서, 글자 좌우가 잘려 보입니다.
        /// </summary>
        internal static void SetWidth(RectTransform rect, float width)
        {
            var element = rect.gameObject.GetComponent<LayoutElement>();
            if (element == null)
                element = rect.gameObject.AddComponent<LayoutElement>();

            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }
    }
}
