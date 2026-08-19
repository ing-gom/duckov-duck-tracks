using System;
using Ducky.Sdk.Localizations;
using DuckTracks.Settings;
using DuckTracks.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuckTracks.UI
{
    /// <summary>
    /// 발자국 설정 창.
    ///
    /// 지금은 <b>걸을 때</b> 하나만 다룹니다. 탈것·차량 프로필은 코드에 남아 있지만
    /// 기본으로 꺼져 있고 창에도 나오지 않습니다 — 오리 발자국을 먼저 제대로
    /// 만들고 나서 열 자리입니다.
    ///
    /// 모양 고르기와 도형 그리기는 본문 위에 덮이는 별도 화면입니다
    /// (<c>ShapePicker</c> · <c>ShapeEditor</c> 파일).
    /// </summary>
    public partial class TrackWindowCanvas : MonoBehaviour
    {
        private const int SortingOrder = 30000;
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        /// <summary>
        /// 창 너비.
        ///
        /// 넉넉하게 잡습니다 — 한국어·중국어·영어가 같은 자리에 들어가야 하는데
        /// 길이가 제각각이라, 좁으면 어느 한 언어에서 글자가 잘립니다.
        /// </summary>
        private const float PanelWidth = 1040f;

        private const float PanelHeight = 880f;

        private static TrackWindowCanvas? _instance;

        public static TrackWindowCanvas Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var go = new GameObject("DuckTracksWindow");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TrackWindowCanvas>();
                return _instance;
            }
        }

        /// <summary>
        /// 창이 떠 있는지. <see cref="Patches.PlayerInputBlockPatch"/>가 이걸 보고
        /// 게임 입력을 막습니다.
        /// </summary>
        public static bool IsShown =>
            _instance != null && _instance._root != null && _instance._root.activeSelf;

        private GameObject? _canvasRoot;
        private GameObject? _root;
        private RectTransform? _body;

        private ColorPickerControl? _picker;

        /// <summary>색 피커가 지금 무엇을 편집 중인지.</summary>
        private enum ColourTarget
        {
            Fresh,
            Fade,
            Burst,
        }

        private ColourTarget _colourTarget = ColourTarget.Fresh;

        private RawImage? _previewImage;
        private Button? _masterButton;

        /// <summary>편집 대상. 지금은 걸을 때 하나뿐입니다.</summary>
        private static TrackProfile Current => TrackSettings.Foot;

        // ── 열고 닫기 ───────────────────────────────────────────────

        public void Show()
        {
            try
            {
                EnsureCanvas();
                Build();

                if (_root != null)
                    _root.SetActive(true);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckTracks] 설정 창 열기 실패: {ex}");
            }
        }

        public void Hide()
        {
            CloseShapePicker();
            CloseShapeEditor();

            if (_root != null)
                _root.SetActive(false);

            // 창을 닫을 때 저장합니다. 슬라이더를 움직일 때마다 파일에 쓰면
            // 한 번 끌 때 수십 번 디스크에 닿습니다.
            TrackProfileStore.Save();
        }

        private void Update()
        {
            if (!IsShown || !Input.GetKeyDown(KeyCode.Escape))
                return;

            // 덮여 있는 화면이 있으면 그것부터 닫습니다. 한 번에 다 닫히면
            // 도형을 그리다 실수로 Esc를 눌렀을 때 창까지 사라집니다.
            if (CloseShapeEditor() || CloseShapePicker())
                return;

            Hide();
        }

        // ── 캔버스 ──────────────────────────────────────────────────

        private void EnsureCanvas()
        {
            if (_canvasRoot != null)
                return;

            var go = new GameObject("DuckTracksCanvas");
            DontDestroyOnLoad(go);
            _canvasRoot = go;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // 게임에 EventSystem이 없을 일은 거의 없지만, 없으면 클릭이 안 먹습니다.
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("DuckTracksEventSystem");
                DontDestroyOnLoad(es);
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        // ── 뼈대 ────────────────────────────────────────────────────

        private void Build()
        {
            if (_root != null)
            {
                RebuildBody();
                return;
            }

            var canvasTransform = _canvasRoot!.transform;

            // 뒤를 어둡게 덮습니다. 창 밖을 눌러도 게임에 클릭이 새지 않게 하는
            // 역할도 겸합니다.
            var shade = UiKit.MakeImage("Shade", canvasTransform, new Color(0f, 0f, 0f, 0.55f));
            UiKit.Stretch(shade.rectTransform);
            _root = shade.gameObject;

            var panel = UiKit.MakeImage("Panel", shade.transform, UiKit.PanelColor).rectTransform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.anchoredPosition = Vector2.zero;

            var panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.spacing = 10f;
            panelLayout.padding = new RectOffset(20, 20, 16, 18);
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            // 덮는 화면은 <b>패널이 아니라</b> 그 바깥 그늘에 붙입니다.
            //
            // 패널에는 VerticalLayoutGroup이 걸려 있어서, 거기에 붙이면 덮는 화면이
            // 레이아웃 자식으로 취급돼 헤더·본문 <b>아래에</b> 한 칸 차지하고 깔립니다.
            // 그늘에는 레이아웃이 없으므로 우리가 정한 자리에 그대로 놓입니다.
            _overlayHost = (RectTransform)shade.transform;

            BuildHeader(panel);
            BuildScrollBody(panel);

            RebuildBody();
        }

        private void BuildHeader(Transform parent)
        {
            var header = UiKit.MakeRow(parent, 46f);

            var title = UiKit.MakeText(
                header, L.Window.Title, 26, UiKit.TextColor, TextAlignmentOptions.MidlineLeft);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _masterButton = UiKit.MakeButton(header, "", 200f, () =>
            {
                TrackSettings.SetEnabled(!TrackSettings.Enabled);
                RefreshMasterButton();
            });

            RefreshMasterButton();

            UiKit.MakeButton(header, L.Window.Close, 120f, Hide);
        }

        private void RefreshMasterButton()
        {
            if (_masterButton == null)
                return;

            var label = UiKit.LabelOf(_masterButton);
            if (label != null)
                label.text = TrackSettings.Enabled ? L.Toggle.MasterOff : L.Toggle.MasterOn;

            var image = _masterButton.targetGraphic as Image;
            if (image != null)
                image.color = TrackSettings.Enabled ? UiKit.ButtonColor : UiKit.AccentColor;
        }

        /// <summary>
        /// 스크롤되는 본문.
        ///
        /// 마스크는 <see cref="RectMask2D"/>를 씁니다 — Mask와 달리 별도 이미지가
        /// 필요 없습니다. 다만 rect를 넘어간 글자를 잘라내므로, 안쪽 요소가 폭을
        /// 넘기지 않도록 <see cref="UiKit.SetWidth"/>가 최소폭을 안 박습니다.
        /// </summary>
        private void BuildScrollBody(Transform parent)
        {
            var viewport = UiKit.MakeRect("Viewport", parent);
            var element = viewport.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;
            element.minHeight = 640f;

            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            _body = UiKit.MakeRect("Content", viewport);
            _body.anchorMin = new Vector2(0f, 1f);
            _body.anchorMax = new Vector2(1f, 1f);
            _body.pivot = new Vector2(0.5f, 1f);

            // 반드시 0으로 만들어야 합니다.
            //
            // 새로 만든 RectTransform의 sizeDelta는 (100, 100)입니다. 가로가 늘어나는
            // 앵커에서 sizeDelta.x는 <b>부모 폭에 더해지는 값</b>이라, 그대로 두면 본문이
            // 뷰포트보다 100px 넓어집니다. 피벗이 가운데(0.5)라 좌우로 50px씩 삐져나가고,
            // 그 부분을 RectMask2D가 잘라냅니다 — 글자 좌우가 잘리던 원인입니다.
            _body.sizeDelta = Vector2.zero;

            var layout = _body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;

            // 오른쪽 여백은 스크롤 막대 자리입니다. 0이면 마지막 글자가 막대에 가립니다.
            layout.padding = new RectOffset(0, 14, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = _body;
        }

        // ── 본문 ────────────────────────────────────────────────────

        private void RebuildBody()
        {
            if (_body == null)
                return;

            for (int i = _body.childCount - 1; i >= 0; i--)
                Destroy(_body.GetChild(i).gameObject);

            _picker = null;
            _previewImage = null;

            var profile = Current;

            BuildShapeSection(profile);
            BuildColourSection(profile);
            BuildSizeSection(profile);
            BuildFooter(profile);
        }

        // ── 모양 ────────────────────────────────────────────────────

        private void BuildShapeSection(TrackProfile profile)
        {
            var section = UiKit.MakeSection(_body!, L.Shape.Section);

            var row = UiKit.MakeRow(section, 40f);
            AddSourceButton(row, profile, L.Shape.SourceActual, TrackShapeSource.ActualFoot);
            AddSourceButton(row, profile, L.Shape.SourceTexture, TrackShapeSource.Texture);

            if (profile.shapeSource == TrackShapeSource.ActualFoot)
            {
                UiKit.MakeHint(section, L.Shape.ActualHint);
            }
            else
            {
                var pickRow = UiKit.MakeRow(section, 40f);

                var current = UiKit.MakeText(
                    pickRow,
                    string.IsNullOrEmpty(profile.textureName) ? L.Shape.SourceBuiltin : profile.textureName,
                    19, UiKit.TextColor, TextAlignmentOptions.MidlineLeft);
                current.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

                UiKit.MakeButton(pickRow, L.Shape.Picker, 180f, () => OpenShapePicker(PickerTarget.Track));
                UiKit.MakeButton(pickRow, L.Editor.Open, 180f, () => OpenShapeEditor(null));

                string? folder = TrackTextures.GetUserFolder();
                if (!string.IsNullOrEmpty(folder))
                    UiKit.MakeHint(section, string.Format(L.Shape.FolderHint, folder));
            }

            BuildPreview(section, profile);
        }

        private void AddSourceButton(
            Transform parent, TrackProfile profile, string label, TrackShapeSource source)
        {
            var button = UiKit.MakeButton(parent, label, 0f, () =>
            {
                profile.shapeSource = source;
                TrackSettings.NotifyChanged();
                RebuildBody();
            });

            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = profile.shapeSource == source ? UiKit.AccentColor : UiKit.ButtonColor;
        }

        /// <summary>
        /// 지금 설정으로 자국이 어떻게 보일지.
        ///
        /// 바탕을 어둡게 깔고 그 위에 그립니다. 실제 자국도 바닥 위에 얹히므로,
        /// 흰 바탕에 그리면 밝은 색 자국이 안 보여서 판단을 그르칩니다.
        /// </summary>
        private void BuildPreview(Transform parent, TrackProfile profile)
        {
            var row = UiKit.MakeRow(parent, 150f);

            var caption = UiKit.MakeText(
                row, L.Shape.Preview, 18, UiKit.MutedColor, TextAlignmentOptions.MidlineLeft);
            UiKit.SetWidth(caption.rectTransform, 260f);

            var backdrop = UiKit.MakeImage("Backdrop", row, new Color(0.13f, 0.14f, 0.12f, 1f));
            UiKit.SetWidth(backdrop.rectTransform, 150f);

            var holder = UiKit.MakeRect("Preview", backdrop.transform);
            UiKit.Stretch(holder);
            holder.offsetMin = new Vector2(12f, 12f);
            holder.offsetMax = new Vector2(-12f, -12f);

            _previewImage = holder.gameObject.AddComponent<RawImage>();
            RefreshPreview(profile);

            var spacer = UiKit.MakeRect("Spacer", row);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void RefreshPreview(TrackProfile profile)
        {
            if (_previewImage == null)
                return;

            // 실제 발 실루엣은 판에 들어가 발을 굽기 전에는 없습니다. 그때는
            // 내장 도형을 보여 줍니다 — 창에서 아무것도 안 보이는 것보다 낫습니다.
            string? name = profile.shapeSource == TrackShapeSource.Texture ? profile.textureName : null;

            _previewImage.texture = TrackTextures.Resolve(name);

            // 알갱이 색을 편집 중이어도 미리보기는 자국입니다. 자국 색을 보여 줘야
            // 무엇이 바뀌는지 헷갈리지 않습니다.
            _previewImage.color = profile.color;
        }

        // ── 색 ──────────────────────────────────────────────────────

        private void BuildColourSection(TrackProfile profile)
        {
            var section = UiKit.MakeSection(_body!, L.Colour.Section);

            var targetRow = UiKit.MakeRow(section, 40f);

            AddColourTab(targetRow, L.Colour.Fresh, ColourTarget.Fresh);
            AddColourTab(targetRow, L.Colour.Fade, ColourTarget.Fade);

            // 알갱이 색 칸은 알갱이를 켰을 때만 나옵니다. 꺼 놓고 색만 고르면
            // 아무 일도 안 일어나서 고장 난 것처럼 보입니다.
            if (profile.burst)
                AddColourTab(targetRow, L.Colour.Burst, ColourTarget.Burst);
            else if (_colourTarget == ColourTarget.Burst)
                _colourTarget = ColourTarget.Fresh;

            BuildPicker(section, profile);

            UiKit.MakeSliderRow(
                section, L.Colour.Alpha, 0f, 1f, TargetColour(profile).a, "{0:P0}",
                v =>
                {
                    var colour = TargetColour(profile);
                    colour.a = v;
                    SetTargetColour(profile, colour);

                    TrackSettings.NotifyChanged();
                    RefreshPreview(profile);
                });

            var blendRow = UiKit.MakeRow(section, 40f);

            var alphaButton = UiKit.MakeButton(blendRow, L.Colour.BlendAlpha, 0f, () =>
            {
                profile.blend = TrackBlend.AlphaBlend;
                TrackSettings.NotifyChanged();
                RebuildBody();
            });

            var addButton = UiKit.MakeButton(blendRow, L.Colour.BlendAdditive, 0f, () =>
            {
                profile.blend = TrackBlend.Additive;
                TrackSettings.NotifyChanged();
                RebuildBody();
            });

            SetSelected(alphaButton, profile.blend == TrackBlend.AlphaBlend);
            SetSelected(addButton, profile.blend == TrackBlend.Additive);

            UiKit.MakeHint(section, L.Colour.BlendHint);

            // 밝기는 야광일 때만 뜻이 있습니다. 덮어쓰기에서 올리면 색이 하얗게 뜨기만
            // 하므로 아예 보여 주지 않습니다.
            if (profile.blend == TrackBlend.Additive)
            {
                UiKit.MakeSliderRow(section, L.Colour.GlowIntensity, 0.5f, 6f, profile.glowIntensity,
                    "{0:F1}x", v => { profile.glowIntensity = v; TrackSettings.NotifyChanged(); });
            }

            BuildBurstSection(profile);
            BuildPulseSection(profile);
        }

        // ── 걸음 알갱이 ────────────────────────────────────────────

        private void BuildBurstSection(TrackProfile profile)
        {
            var section = UiKit.MakeSection(_body!, L.Burst.Section);

            UiKit.MakeCheckRow(section, L.Burst.Enable, profile.burst, v =>
            {
                profile.burst = v;
                TrackSettings.NotifyChanged();
                RebuildBody();
            });

            if (!profile.burst)
                return;

            UiKit.MakeSliderRow(section, L.Burst.Count, 1f, 24f, profile.burstCount,
                "{0:F0}", v => { profile.burstCount = Mathf.RoundToInt(v); TrackSettings.NotifyChanged(); });

            UiKit.MakeSliderRow(section, L.Burst.Size, 0.01f, 0.4f, profile.burstSize,
                "{0:F2}m", v => { profile.burstSize = v; TrackSettings.NotifyChanged(); });

            UiKit.MakeSliderRow(section, L.Burst.Speed, 0.1f, 5f, profile.burstSpeed,
                "{0:F1}", v => { profile.burstSpeed = v; TrackSettings.NotifyChanged(); });

            UiKit.MakeSliderRow(section, L.Burst.Gravity, 0f, 3f, profile.burstGravity,
                "{0:F2}x", v => { profile.burstGravity = v; TrackSettings.NotifyChanged(); });

            UiKit.MakeSliderRow(section, L.Burst.Life, 0.1f, 4f, profile.burstLife,
                "{0:F1}s", v => { profile.burstLife = v; TrackSettings.NotifyChanged(); });

            var shapeRow = UiKit.MakeRow(section, 40f);

            var shapeName = UiKit.MakeText(
                shapeRow,
                string.IsNullOrEmpty(profile.burstTextureName) ? L.Burst.DefaultShape : profile.burstTextureName,
                18, UiKit.TextColor, TextAlignmentOptions.MidlineLeft);
            shapeName.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            UiKit.MakeButton(shapeRow, L.Burst.PickShape, 180f, () => OpenShapePicker(PickerTarget.Burst));

            if (!string.IsNullOrEmpty(profile.burstTextureName))
            {
                UiKit.MakeButton(shapeRow, L.Burst.ResetShape, 140f, () =>
                {
                    profile.burstTextureName = "";
                    TrackSettings.NotifyChanged();
                    RebuildBody();
                });
            }

            // 흘리기는 튀기기 안에 둡니다. 색·모양·수명을 거기서 가져오므로
            // 따로 떼어 놓으면 무엇을 물려받는지 알 수 없습니다.
            UiKit.MakeCheckRow(section, L.Burst.Drift, profile.drift, v =>
            {
                profile.drift = v;
                TrackSettings.NotifyChanged();
                RebuildBody();
            });

            if (profile.drift)
            {
                UiKit.MakeHint(section, L.Burst.DriftHint);

                UiKit.MakeSliderRow(section, L.Burst.DriftRate, 0.05f, 4f, profile.driftRate,
                    "{0:F2}", v => { profile.driftRate = v; TrackSettings.NotifyChanged(); });

                UiKit.MakeSliderRow(section, L.Burst.DriftScale, 0.15f, 1.5f, profile.driftScale,
                    "{0:F2}x", v => { profile.driftScale = v; TrackSettings.NotifyChanged(); });

                UiKit.MakeSliderRow(section, L.Burst.DriftRise, 0.02f, 1.5f, profile.driftRise,
                    "{0:F2}", v => { profile.driftRise = v; TrackSettings.NotifyChanged(); });
            }

            UiKit.MakeHint(section, L.Burst.ColourHint);
        }

        // ── 깜박임 ──────────────────────────────────────────────────

        private void BuildPulseSection(TrackProfile profile)
        {
            var section = UiKit.MakeSection(_body!, L.Pulse.Section);

            UiKit.MakeCheckRow(section, L.Pulse.Enable, profile.pulse, v =>
            {
                profile.pulse = v;
                TrackSettings.NotifyChanged();
                RebuildBody();
            });

            if (profile.pulse)
            {
                // 0.05Hz(20초에 한 번)부터 15Hz(스트로브)까지. 위쪽을 6에서 늘린 것은
                // 빠른 깜박임이 야광과 붙으면 전혀 다른 인상이 되기 때문입니다.
                UiKit.MakeSliderRow(section, L.Pulse.Speed, 0.05f, 15f, profile.pulseSpeed,
                    "{0:F2}Hz", v => { profile.pulseSpeed = v; TrackSettings.NotifyChanged(); });

                UiKit.MakeSliderRow(section, L.Pulse.Depth, 0f, 1f, profile.pulseDepth,
                    "{0:P0}", v => { profile.pulseDepth = v; TrackSettings.NotifyChanged(); });
            }

            UiKit.MakeCheckRow(section, L.Pulse.CycleHue, profile.cycleHue, v =>
            {
                profile.cycleHue = v;
                TrackSettings.NotifyChanged();
                RebuildBody();
            });

            if (profile.cycleHue)
            {
                UiKit.MakeSliderRow(section, L.Pulse.HueSpeed, 0.01f, 1f, profile.hueSpeed,
                    "{0:F2}", v => { profile.hueSpeed = v; TrackSettings.NotifyChanged(); });

                // 무채색은 색조를 돌려도 회색입니다. 기본값이 진한 회색이라
                // 안 알려 주면 기능이 고장 난 줄 압니다.
                Color.RGBToHSV(profile.color, out _, out float saturation, out _);
                if (saturation < 0.08f)
                    UiKit.MakeHint(section, L.Pulse.GreyHint);
            }
        }

        private void AddColourTab(Transform parent, string label, ColourTarget target)
        {
            var button = UiKit.MakeButton(parent, label, 0f, () =>
            {
                _colourTarget = target;
                RebuildBody();
            });

            SetSelected(button, _colourTarget == target);
        }

        private Color TargetColour(TrackProfile profile)
        {
            return _colourTarget switch
            {
                ColourTarget.Fade => profile.fadeColor,
                ColourTarget.Burst => profile.burstColor,
                _ => profile.color,
            };
        }

        private void SetTargetColour(TrackProfile profile, Color colour)
        {
            switch (_colourTarget)
            {
                case ColourTarget.Fade:
                    profile.fadeColor = colour;
                    break;

                case ColourTarget.Burst:
                    profile.burstColor = colour;
                    break;

                default:
                    profile.color = colour;
                    break;
            }
        }

        private static void SetSelected(Button button, bool selected)
        {
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = selected ? UiKit.AccentColor : UiKit.ButtonColor;
        }

        /// <summary>
        /// 색 선택기. 채도·명도 사각형 + 색조 막대 + 숫자 입력칸.
        ///
        /// 부품 구성은 <see cref="ColorPickerControl"/>이 요구하는 그대로입니다 —
        /// 그쪽이 텍스처와 드래그 처리를 전부 들고 있어서 여기서는 자리만 잡습니다.
        /// </summary>
        private void BuildPicker(Transform parent, TrackProfile profile)
        {
            var row = UiKit.MakeRow(parent, 200f, 14f);

            var square = UiKit.MakeImage("SV", row, Color.white);
            UiKit.SetWidth(square.rectTransform, 200f);

            var cursor = UiKit.MakeImage("Cursor", square.transform, Color.white);
            cursor.rectTransform.sizeDelta = new Vector2(12f, 12f);
            cursor.raycastTarget = false;

            var hue = UiKit.MakeImage("Hue", row, Color.white);
            UiKit.SetWidth(hue.rectTransform, 36f);

            var side = UiKit.MakeRect("Side", row);
            side.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var sideLayout = side.gameObject.AddComponent<VerticalLayoutGroup>();
            sideLayout.spacing = 8f;
            sideLayout.childControlWidth = true;
            sideLayout.childControlHeight = true;
            sideLayout.childForceExpandWidth = true;
            sideLayout.childForceExpandHeight = false;

            var swatch = UiKit.MakeImage("Swatch", side, Color.white);
            UiKit.SetHeight(swatch.rectTransform, 52f);

            var hexRow = UiKit.MakeRow(side, 36f);
            var hexCaption = UiKit.MakeText(
                hexRow, L.Colour.Hex, 17, UiKit.MutedColor, TextAlignmentOptions.MidlineLeft);
            UiKit.SetWidth(hexCaption.rectTransform, 72f);
            var hexField = MakeInput(hexRow);

            var rgbRow = UiKit.MakeRow(side, 36f);
            var rField = MakeInput(rgbRow);
            var gField = MakeInput(rgbRow);
            var bField = MakeInput(rgbRow);

            _picker = new ColorPickerControl(square, cursor, hue, swatch, hexField, rField, gField, bField);
            _picker.SetColor(TargetColour(profile));

            _picker.OnChanged = colour =>
            {
                // 알파는 슬라이더가 따로 들고 있습니다. 피커가 낸 색으로 덮어쓰면
                // 방금 맞춘 진하기가 1로 튑니다.
                colour.a = TargetColour(profile).a;
                SetTargetColour(profile, colour);

                // 찍힌 직후 색을 바꾸면 사라질 때 색도 따라갑니다. 그래야 색 하나만
                // 바꿔도 자연스럽게 옅어집니다.
                if (_colourTarget == ColourTarget.Fresh)
                    profile.fadeColor = new Color(colour.r, colour.g, colour.b, profile.fadeColor.a);

                TrackSettings.NotifyChanged();
                RefreshPreview(profile);
            };
        }

        /// <summary>
        /// TMP_InputField는 자식 구조(뷰포트·텍스트)를 직접 만들어 줘야 동작합니다.
        /// </summary>
        private static TMP_InputField MakeInput(Transform parent)
        {
            var image = UiKit.MakeImage("Input", parent, new Color(0f, 0f, 0f, 0.45f));
            image.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var field = image.gameObject.AddComponent<TMP_InputField>();

            var viewport = UiKit.MakeRect("TextArea", image.transform);
            UiKit.Stretch(viewport);
            viewport.offsetMin = new Vector2(8f, 4f);
            viewport.offsetMax = new Vector2(-8f, -4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = UiKit.MakeText(viewport, "", 17, UiKit.TextColor, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(text.rectTransform);
            text.raycastTarget = false;

            field.textViewport = viewport;
            field.textComponent = text;
            field.targetGraphic = image;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;

            return field;
        }

        // ── 크기와 지속 ─────────────────────────────────────────────

        private void BuildSizeSection(TrackProfile profile)
        {
            var section = UiKit.MakeSection(_body!, L.Shape2.Section);

            // 실제 발을 쓰면 크기는 발이 정합니다. 여기서는 배수만 만집니다.
            if (profile.shapeSource == TrackShapeSource.ActualFoot)
            {
                UiKit.MakeSliderRow(section, L.Shape2.AutoScale, 0.3f, 3f, profile.autoSizeScale,
                    "{0:F2}x", v => { profile.autoSizeScale = v; TrackSettings.NotifyChanged(); });
            }
            else
            {
                UiKit.MakeSliderRow(section, L.Shape2.Size, 0.05f, 1.5f, profile.size,
                    "{0:F2}m", v => { profile.size = v; TrackSettings.NotifyChanged(); });
            }

            UiKit.MakeCheckRow(section, L.Shape2.Forever, profile.infiniteLife, v =>
            {
                profile.infiniteLife = v;
                TrackSettings.NotifyChanged();

                // 슬라이더를 보였다 감췄다 해야 하므로 본문을 다시 만듭니다.
                RebuildBody();
            });

            if (profile.infiniteLife)
            {
                // 계속 남길 때는 개수도 제한하지 않으므로 슬라이더가 할 일이 없습니다.
                UiKit.MakeHint(section, L.Shape2.ForeverHint);
            }
            else
            {
                UiKit.MakeSliderRow(section, L.Shape2.Life, 0.5f, 60f, profile.life,
                    "{0:F1}s", v => { profile.life = v; TrackSettings.NotifyChanged(); });
            }
        }

        private void BuildFooter(TrackProfile profile)
        {
            var row = UiKit.MakeRow(_body!, 44f);

            var spacer = UiKit.MakeRect("Spacer", row);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            UiKit.MakeButton(row, L.Window.Reset, 240f, () =>
            {
                CopyInto(TrackProfile.DefaultFoot(), profile);
                TrackSettings.NotifyChanged();
                RebuildBody();
            });
        }

        /// <summary>
        /// 기본값을 살아 있는 프로필에 옮깁니다.
        ///
        /// 참조를 갈아 끼우지 않는 이유는 <see cref="Settings.TrackSettings"/>의
        /// 정적 필드를 다른 곳이 이미 들고 있기 때문입니다.
        /// </summary>
        private static void CopyInto(TrackProfile from, TrackProfile to)
        {
            to.enabled = from.enabled;
            to.kind = from.kind;
            to.shapeSource = from.shapeSource;
            to.textureName = from.textureName;
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

        public void Dispose()
        {
            if (_canvasRoot != null)
                Destroy(_canvasRoot);

            _canvasRoot = null;
            _root = null;
            _body = null;
        }
    }
}
