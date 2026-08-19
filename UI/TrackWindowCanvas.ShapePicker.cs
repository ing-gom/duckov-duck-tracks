using Ducky.Sdk.Localizations;
using DuckTracks.Settings;
using DuckTracks.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuckTracks.UI
{
    /// <summary>
    /// 모양 고르기 — 그림을 격자로 늘어놓고 눈으로 고릅니다.
    ///
    /// ◀ ▶로 이름만 넘기던 방식은 스무 개가 넘어가면 못 씁니다. 발자국은 이름보다
    /// 생김새로 고르는 것이라 더더욱 그렇습니다.
    /// </summary>
    public partial class TrackWindowCanvas
    {
        /// <summary>덮이는 화면들이 붙는 자리. 본문 위에 얹혀야 합니다.</summary>
        private RectTransform? _overlayHost;

        private GameObject? _pickerOverlay;

        /// <summary>모양 고르기가 지금 무엇의 모양을 고르는 중인지.</summary>
        private enum PickerTarget
        {
            Track,
            Burst,
        }

        private PickerTarget _pickerTarget = PickerTarget.Track;

        private void OpenShapePicker(PickerTarget target)
        {
            _pickerTarget = target;
            CloseShapePicker();

            if (_overlayHost == null)
                return;

            var overlay = MakeOverlay(
                target == PickerTarget.Burst ? L.Burst.PickShape : L.Shape.Picker,
                out var content, out var footer);

            _pickerOverlay = overlay;

            UiKit.MakeButton(footer, L.Shape.Refresh, 200f, () =>
            {
                TrackTextures.GetNames(refresh: true);
                OpenShapePicker(target);
            });

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(104f, 124f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(4, 4, 4, 4);

            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var profile = Current;
            var names = TrackTextures.GetNames();

            foreach (string name in names)
                AddShapeCell(content, profile, name);

            if (names.Length <= 1)
                UiKit.MakeHint(footer, L.Shape.NoTextures);
        }

        /// <summary>
        /// 칸 하나 — 그림 + 이름.
        ///
        /// 직접 그린 도형은 지울 수 있어야 하므로 이름 옆에 표시를 답니다.
        /// </summary>
        private void AddShapeCell(Transform parent, TrackProfile profile, string name)
        {
            bool selected = (_pickerTarget == PickerTarget.Burst
                ? profile.burstTextureName ?? ""
                : profile.textureName ?? "") == name;
            bool drawn = !string.IsNullOrEmpty(name) && CustomShapes.Find(name) != null;

            var cell = UiKit.MakeImage("Cell", parent, selected ? UiKit.AccentColor : UiKit.ButtonColor);

            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell;
            button.onClick.AddListener(() =>
            {
                if (_pickerTarget == PickerTarget.Burst)
                {
                    profile.burstTextureName = name;
                }
                else
                {
                    profile.textureName = name;
                    profile.shapeSource = TrackShapeSource.Texture;
                }

                TrackSettings.NotifyChanged();

                CloseShapePicker();
                RebuildBody();
            });

            // 그림. 어두운 바탕에 얹어야 밝은 도형이 보입니다.
            var backdrop = UiKit.MakeImage("Backdrop", cell.transform, new Color(0.1f, 0.11f, 0.10f, 1f));
            var backRect = backdrop.rectTransform;
            backRect.anchorMin = new Vector2(0f, 0f);
            backRect.anchorMax = new Vector2(1f, 1f);
            backRect.offsetMin = new Vector2(6f, 28f);
            backRect.offsetMax = new Vector2(-6f, -6f);
            backdrop.raycastTarget = false;

            var image = UiKit.MakeRect("Image", backdrop.transform);
            UiKit.Stretch(image);
            image.offsetMin = new Vector2(4f, 4f);
            image.offsetMax = new Vector2(-4f, -4f);

            var raw = image.gameObject.AddComponent<RawImage>();
            raw.texture = TrackTextures.Resolve(name);
            raw.raycastTarget = false;

            var caption = UiKit.MakeText(
                cell.transform,
                string.IsNullOrEmpty(name) ? L.Shape.SourceBuiltin : (drawn ? "✎ " + name : name),
                14, UiKit.TextColor, TextAlignmentOptions.Center);

            var capRect = caption.rectTransform;
            capRect.anchorMin = new Vector2(0f, 0f);
            capRect.anchorMax = new Vector2(1f, 0f);
            capRect.pivot = new Vector2(0.5f, 0f);
            capRect.sizeDelta = new Vector2(-8f, 26f);
            capRect.anchoredPosition = new Vector2(0f, 2f);
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 9f;
            caption.fontSizeMax = 14f;

            // 직접 그린 도형은 그 자리에서 고쳐 그릴 수 있게 합니다.
            if (!drawn)
                return;

            var edit = UiKit.MakeButton(cell.transform, "✎", 0f, () => OpenShapeEditor(name));
            var editRect = ((Image)edit.targetGraphic).rectTransform;
            editRect.anchorMin = new Vector2(1f, 1f);
            editRect.anchorMax = new Vector2(1f, 1f);
            editRect.pivot = new Vector2(1f, 1f);
            editRect.sizeDelta = new Vector2(28f, 28f);
            editRect.anchoredPosition = new Vector2(-4f, -4f);
        }

        private bool CloseShapePicker()
        {
            if (_pickerOverlay == null)
                return false;

            Destroy(_pickerOverlay);
            _pickerOverlay = null;
            return true;
        }

        /// <summary>
        /// 본문을 덮는 화면의 공통 뼈대 — 제목줄 · 스크롤 내용 · 아래 버튼줄.
        /// </summary>
        private GameObject MakeOverlay(string title, out RectTransform content, out RectTransform footer)
        {
            var shade = UiKit.MakeImage("Overlay", _overlayHost!, new Color(0.06f, 0.07f, 0.09f, 0.99f));

            // 설정 창과 같은 자리·같은 크기로 놓습니다.
            var rect = shade.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rect.anchoredPosition = Vector2.zero;

            // uGUI는 형제 순서대로 그립니다. 맨 뒤로 보내야 설정 창을 덮습니다.
            rect.SetAsLastSibling();

            var layout = shade.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(20, 20, 16, 18);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = UiKit.MakeRow(shade.transform, 44f);
            var caption = UiKit.MakeText(header, title, 24, UiKit.TextColor, TextAlignmentOptions.MidlineLeft);
            caption.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            UiKit.MakeButton(header, L.Window.Close, 120f, () =>
            {
                CloseShapeEditor();
                CloseShapePicker();
            });

            var viewport = UiKit.MakeRect("Viewport", shade.transform);
            var element = viewport.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;
            element.minHeight = 620f;

            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            content = UiKit.MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);

            // 본문과 같은 이유로 0입니다 — 기본 sizeDelta (100,100)를 두면 좌우로
            // 50px씩 삐져나가 마스크에 잘립니다.
            content.sizeDelta = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;

            footer = UiKit.MakeRow(shade.transform, 44f);

            return shade.gameObject;
        }
    }
}
