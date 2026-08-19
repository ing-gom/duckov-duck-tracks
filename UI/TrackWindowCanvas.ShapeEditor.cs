using System;
using Ducky.Sdk.Localizations;
using DuckTracks.Settings;
using DuckTracks.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuckTracks.UI
{
    /// <summary>
    /// 모양 만들기 — <see cref="CustomShapes.GridSize"/>칸 격자를 칠해서 도형을 만듭니다.
    ///
    /// 칸 그대로 쓰면 계단이 그대로 보이므로, 구울 때 이중선형으로 보간한 뒤 문턱값을
    /// 부드럽게 넘깁니다(<see cref="CustomShapes"/>). 그래서 24칸으로 그려도 하트나
    /// 발바닥 정도는 매끄럽게 나옵니다.
    /// </summary>
    public partial class TrackWindowCanvas
    {
        private GameObject? _editorOverlay;

        private bool[]? _cells;
        private Image[]? _cellImages;
        private TMP_InputField? _nameField;
        private TextMeshProUGUI? _editorStatus;

        /// <summary>
        /// 드래그로 이어 칠할 때 무엇으로 칠할지.
        ///
        /// 누른 칸의 반대값으로 고정합니다. 매 칸마다 뒤집으면 지나간 자리가
        /// 켜졌다 꺼졌다 하면서 얼룩이 됩니다.
        /// </summary>
        private bool _paintValue;

        private static readonly Color CellOn = new Color(0.92f, 0.94f, 0.98f, 1f);
        private static readonly Color CellOff = new Color(0.16f, 0.17f, 0.21f, 1f);

        /// <summary>
        /// 도형 편집기를 엽니다.
        /// </summary>
        /// <param name="editName">고칠 도형 이름. <c>null</c>이면 새로 만듭니다.</param>
        private void OpenShapeEditor(string? editName)
        {
            CloseShapeEditor();
            CloseShapePicker();

            if (_overlayHost == null)
                return;

            int size = CustomShapes.GridSize;
            _cells = new bool[size * size];

            var existing = CustomShapes.Find(editName);
            if (existing != null && existing.IsValid && existing.size == size)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                        _cells[y * size + x] = existing.Get(x, y);
                }
            }

            var overlay = MakeOverlay(L.Editor.Title, out var content, out var footer);
            _editorOverlay = overlay;

            BuildGrid(content, size);
            BuildEditorFooter(footer, editName);
        }

        private void BuildGrid(RectTransform content, int size)
        {
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();

            // 칸 크기를 격자 수로 나눠 정합니다. 고정값이면 GridSize를 바꿀 때
            // 격자가 화면 밖으로 나갑니다.
            float cell = Mathf.Floor(600f / size);
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(2f, 2f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = size;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(4, 4, 4, 4);

            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            _cellImages = new Image[size * size];

            // 위에서 아래로 만들되 아래쪽 줄이 y=0이 되게 뒤집습니다. 텍스처에서
            // v가 진행 방향(위가 앞)이라는 약속과 화면에서 보이는 방향을 맞추려는 것입니다.
            for (int row = 0; row < size; row++)
            {
                int y = size - 1 - row;

                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;

                    var image = UiKit.MakeImage("Cell", content, _cells![index] ? CellOn : CellOff);
                    _cellImages[index] = image;

                    var painter = image.gameObject.AddComponent<CellPainter>();
                    painter.Index = index;
                    painter.Owner = this;
                }
            }
        }

        /// <summary>
        /// 격자 칸 하나. 누름과 끌기를 둘 다 받아야 이어 칠하기가 됩니다.
        ///
        /// <see cref="IPointerEnterHandler"/>를 쓰는 이유: 드래그는 처음 누른 칸에만
        /// 이벤트가 갑니다. 지나가는 칸이 스스로 "지금 칠하는 중인가"를 물어야 합니다.
        /// </summary>
        private class CellPainter : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
        {
            internal int Index;
            internal TrackWindowCanvas Owner = null!;

            public void OnPointerDown(PointerEventData eventData)
            {
                Owner._paintValue = !(Owner._cells != null && Owner._cells[Index]);
                Owner.Paint(Index);
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (eventData.pointerId != -1 && !eventData.dragging)
                    return;

                if (!Input.GetMouseButton(0))
                    return;

                Owner.Paint(Index);
            }
        }

        private void Paint(int index)
        {
            if (_cells == null || _cellImages == null || index < 0 || index >= _cells.Length)
                return;

            if (_cells[index] == _paintValue)
                return;

            _cells[index] = _paintValue;

            var image = _cellImages[index];
            if (image != null)
                image.color = _paintValue ? CellOn : CellOff;
        }

        private void BuildEditorFooter(RectTransform footer, string? editName)
        {
            var caption = UiKit.MakeText(
                footer, L.Editor.Name, 17, UiKit.MutedColor, TextAlignmentOptions.MidlineLeft);
            UiKit.SetWidth(caption.rectTransform, 90f);

            _nameField = MakeInput(footer);
            _nameField.text = editName ?? "";

            UiKit.MakeButton(footer, L.Editor.Random, 120f, Randomize);
            UiKit.MakeButton(footer, L.Editor.Clear, 120f, () => FillAll(false));
            UiKit.MakeButton(footer, L.Editor.Invert, 120f, InvertAll);

            if (!string.IsNullOrEmpty(editName))
            {
                UiKit.MakeButton(footer, L.Editor.Delete, 120f, () =>
                {
                    CustomShapes.Delete(editName);

                    // 지운 도형을 그대로 쓰고 있으면 빈 이름으로 되돌립니다.
                    if ((Current.textureName ?? "") == editName)
                        Current.textureName = "";

                    TrackTextures.GetNames(refresh: true);
                    TrackSettings.NotifyChanged();

                    CloseShapeEditor();
                    RebuildBody();
                }, new Color(0.42f, 0.20f, 0.22f, 1f));
            }

            UiKit.MakeButton(footer, L.Editor.Save, 160f, SaveShape, UiKit.AccentColor);

            _editorStatus = UiKit.MakeText(footer, "", 16, UiKit.MutedColor, TextAlignmentOptions.MidlineLeft);
            _editorStatus.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        /// <summary>
        /// 무작위 발자국.
        ///
        /// <see cref="FootShapeGenerator"/>가 패드·발가락·발톱·물갈퀴·갈라짐의 조합에서
        /// 뽑습니다. <see cref="CustomShapes.Randomize"/>를 쓰지 않는 이유는 그쪽이
        /// 좌우대칭 얼룩을 만들기 때문입니다 — 아무 도형이나 만들 때는 쓸모가 있지만
        /// 발자국을 만들려고 누르면 아메바만 나옵니다.
        ///
        /// 눌러 가며 마음에 드는 게 나올 때까지 돌리다가 손으로 다듬는 흐름입니다.
        /// </summary>
        private void Randomize()
        {
            if (_cells == null || _cellImages == null)
                return;

            FootShapeGenerator.Fill(UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                                    CustomShapes.GridSize, _cells);

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cellImages[i] != null)
                    _cellImages[i].color = _cells[i] ? CellOn : CellOff;
            }
        }

        private void FillAll(bool value)
        {
            if (_cells == null || _cellImages == null)
                return;

            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = value;

                if (_cellImages[i] != null)
                    _cellImages[i].color = value ? CellOn : CellOff;
            }
        }

        private void InvertAll()
        {
            if (_cells == null || _cellImages == null)
                return;

            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = !_cells[i];

                if (_cellImages[i] != null)
                    _cellImages[i].color = _cells[i] ? CellOn : CellOff;
            }
        }

        private void SaveShape()
        {
            if (_cells == null || _nameField == null)
                return;

            string name = (_nameField.text ?? "").Trim();

            if (!CustomShapes.Save(name, _cells, CustomShapes.GridSize, out string reason))
            {
                // 실패를 조용히 삼키면 저장 버튼이 고장 난 것처럼 보입니다.
                if (_editorStatus != null)
                    _editorStatus.text = reason;

                return;
            }

            // 방금 만든 것을 바로 쓰게 합니다. 저장하고 다시 골라야 하면 번거롭습니다.
            Current.textureName = name;
            Current.shapeSource = TrackShapeSource.Texture;

            TrackTextures.GetNames(refresh: true);
            TrackSettings.NotifyChanged();

            CloseShapeEditor();
            RebuildBody();
        }

        private bool CloseShapeEditor()
        {
            if (_editorOverlay == null)
                return false;

            Destroy(_editorOverlay);
            _editorOverlay = null;
            _cells = null;
            _cellImages = null;
            _nameField = null;
            _editorStatus = null;
            return true;
        }
    }
}
