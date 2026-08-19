using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuckTracks.UI
{
    /// <summary>
    /// 드래그로 위치를 알려 주는 영역. 색상 사각형과 색조 막대에 붙습니다.
    /// (uGUI에는 이런 기본 컨트롤이 없어서 최소한으로 만듭니다)
    /// </summary>
    public class PointerDragArea : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        /// <summary>드래그 중 위치를 0~1로 알려 줍니다.</summary>
        public Action<Vector2>? OnPicked;

        /// <summary>
        /// 누르는 순간의 위치. 회전처럼 "직전 위치와의 차이"가 필요한 쪽에서
        /// 기준점을 잡는 데 씁니다. 지정하지 않으면 <see cref="OnPicked"/>가 대신 불립니다.
        /// </summary>
        public Action<Vector2>? OnPressed;

        public void OnPointerDown(PointerEventData eventData) => Report(eventData, pressed: true);

        public void OnDrag(PointerEventData eventData) => Report(eventData, pressed: false);

        private void Report(PointerEventData eventData, bool pressed)
        {
            var rect = (RectTransform)transform;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            var size = rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return;

            // pivot 기준 좌표를 0~1로 바꿉니다.
            float x = (local.x - rect.rect.xMin) / size.x;
            float y = (local.y - rect.rect.yMin) / size.y;

            var normalized = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));

            if (pressed && OnPressed != null)
                OnPressed.Invoke(normalized);
            else
                OnPicked?.Invoke(normalized);
        }
    }

    /// <summary>
    /// 색 하나를 고르는 묶음: 채도·명도 사각형 + 색조 막대 + HEX/RGB 입력.
    ///
    /// 슬라이더 세 개로 색을 맞추는 건 감이 안 잡혀서, 실제로 눈으로 집을 수 있게 만듭니다.
    /// 값이 바뀌면 <see cref="OnChanged"/>로 알려 주고, 바깥에서 값이 바뀌면
    /// <see cref="SetColor"/>로 되돌려 채웁니다.
    /// </summary>
    public class ColorPickerControl
    {
        private readonly Image _svImage;
        private readonly Image _cursor;
        private readonly Image _swatch;
        private readonly TMP_InputField _hexField;
        private readonly TMP_InputField _rField;
        private readonly TMP_InputField _gField;
        private readonly TMP_InputField _bField;

        private Texture2D? _svTexture;

        private float _hue;
        private float _saturation = 1f;
        private float _value = 1f;

        /// <summary>되돌려 채우는 중에는 콜백을 막습니다 (무한 루프 방지).</summary>
        private bool _suppress;

        public Action<Color>? OnChanged;

        public ColorPickerControl(Image svImage, Image cursor, Image hueImage, Image swatch,
            TMP_InputField hexField, TMP_InputField rField, TMP_InputField gField, TMP_InputField bField)
        {
            _svImage = svImage;
            _cursor = cursor;
            _swatch = swatch;
            _hexField = hexField;
            _rField = rField;
            _gField = gField;
            _bField = bField;

            hueImage.sprite = MakeSprite(BuildHueTexture());
            hueImage.type = Image.Type.Simple;

            AddDrag(svImage, position =>
            {
                _saturation = position.x;
                _value = position.y;
                Commit();
            });

            AddDrag(hueImage, position =>
            {
                _hue = position.y;
                RefreshSquare();
                Commit();
            });

            _hexField.onEndEdit.AddListener(OnHexEdited);
            _rField.onEndEdit.AddListener(_ => OnChannelEdited());
            _gField.onEndEdit.AddListener(_ => OnChannelEdited());
            _bField.onEndEdit.AddListener(_ => OnChannelEdited());

            RefreshSquare();
        }

        public Color Current => Color.HSVToRGB(_hue, _saturation, _value);

        /// <summary>바깥에서 색이 바뀌었을 때 UI를 맞춥니다 (콜백 없음).</summary>
        public void SetColor(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);

            // 무채색이면 색조가 0으로 튀어서 막대가 제멋대로 움직입니다. 기존 색조를 유지합니다.
            if (s > 0.001f)
                _hue = h;

            _saturation = s;
            _value = v;

            RefreshSquare();
            RefreshFields(color);
            RefreshCursor();

            if (_swatch != null)
                _swatch.color = new Color(color.r, color.g, color.b, 1f);
        }

        // ── 내부 ────────────────────────────────────────────────────

        private static void AddDrag(Image image, Action<Vector2> onPicked)
        {
            image.raycastTarget = true;
            var area = image.gameObject.AddComponent<PointerDragArea>();
            area.OnPicked = onPicked;
        }

        private void Commit()
        {
            if (_suppress)
                return;

            var color = Current;
            RefreshFields(color);
            RefreshCursor();

            if (_swatch != null)
                _swatch.color = new Color(color.r, color.g, color.b, 1f);

            OnChanged?.Invoke(color);
        }

        private void OnHexEdited(string text)
        {
            if (_suppress)
                return;

            string trimmed = text.Trim().TrimStart('#');
            if (!ColorUtility.TryParseHtmlString("#" + trimmed, out Color parsed))
            {
                RefreshFields(Current);   // 잘못 쓴 값은 되돌립니다
                return;
            }

            SetColor(parsed);
            OnChanged?.Invoke(parsed);
        }

        private void OnChannelEdited()
        {
            if (_suppress)
                return;

            var color = new Color(
                ParseChannel(_rField.text),
                ParseChannel(_gField.text),
                ParseChannel(_bField.text),
                1f);

            SetColor(color);
            OnChanged?.Invoke(color);
        }

        private static float ParseChannel(string text)
        {
            if (int.TryParse(text.Trim(), out int value))
                return Mathf.Clamp01(value / 255f);

            return 0f;
        }

        private void RefreshFields(Color color)
        {
            _suppress = true;
            try
            {
                _hexField.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(color));
                _rField.SetTextWithoutNotify(Mathf.RoundToInt(color.r * 255f).ToString());
                _gField.SetTextWithoutNotify(Mathf.RoundToInt(color.g * 255f).ToString());
                _bField.SetTextWithoutNotify(Mathf.RoundToInt(color.b * 255f).ToString());
            }
            finally
            {
                _suppress = false;
            }
        }

        private void RefreshCursor()
        {
            if (_cursor == null)
                return;

            var rect = (RectTransform)_cursor.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(_saturation, _value);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>현재 색조로 채도·명도 사각형을 다시 그립니다.</summary>
        private void RefreshSquare()
        {
            const int size = 64;

            if (_svTexture == null)
            {
                _svTexture = new Texture2D(size, size, TextureFormat.RGB24, false)
                {
                    name = "DuckTracks_SVSquare",
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float s = x / (float)(size - 1);
                    pixels[y * size + x] = Color.HSVToRGB(_hue, s, v);
                }
            }

            _svTexture.SetPixels32(pixels);
            _svTexture.Apply(false);

            _svImage.sprite = MakeSprite(_svTexture);
            _svImage.type = Image.Type.Simple;
        }

        private static Texture2D BuildHueTexture()
        {
            const int height = 64;

            var texture = new Texture2D(1, height, TextureFormat.RGB24, false)
            {
                name = "DuckTracks_HueBar",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int y = 0; y < height; y++)
                texture.SetPixel(0, y, Color.HSVToRGB(y / (float)(height - 1), 1f, 1f));

            texture.Apply(false);
            return texture;
        }

        private static Sprite MakeSprite(Texture2D texture)
        {
            return Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
