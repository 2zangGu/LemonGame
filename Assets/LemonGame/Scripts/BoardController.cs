using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LemonPuzzle
{
    /// <summary>
    /// Handles pointer drag-to-select over the grid and hands the result to GameManager.
    /// Lives on the board panel; that panel needs a raycastable Graphic (an Image works, alpha can
    /// be low) and the scene needs an EventSystem + GraphicRaycaster on the Canvas (Tools > Lemon
    /// Puzzle > Setup Scene wires all of this automatically).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BoardController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Refs")]
        public GameManager gameManager;
        public RectTransform boardRect;          // the panel the grid fills - drives coordinate math
        public GridLayoutGroup gridLayout;        // on boardRect, holds the cell instances
        public LemonCellView cellPrefab;
        public RectTransform selectionIndicator;  // child Image, top-left pivot, drawn over the board
        public TargetBadgeView targetBadge;

        [Header("Feedback")]
        public Text currentSumText;
        public Color sumNeutralColor = Color.gray;
        public Color sumMatchColor = new Color(0.25f, 0.6f, 0.25f);
        public Color sumOverColor = new Color(0.75f, 0.15f, 0.15f);

        LemonCellView[,] views;
        Vector2Int dragStart, dragCurrent;
        bool dragging;
        float cellPitchX, cellPitchY;

        void Awake()
        {
            if (boardRect == null) boardRect = (RectTransform)transform;
            if (gridLayout == null) gridLayout = GetComponent<GridLayoutGroup>();
            // Always build a fresh selection box in code rather than trusting whatever the editor
            // setup script wired up - guarantees a visible, correctly-styled drag box every run,
            // regardless of what state old scene references are left in.
            if (selectionIndicator != null) Destroy(selectionIndicator.gameObject);
            selectionIndicator = BuildSelectionIndicator();
        }

        RectTransform BuildSelectionIndicator()
        {
            var selGO = new GameObject("SelectionIndicator_Runtime", typeof(RectTransform));
            selGO.transform.SetParent(boardRect, false);
            var selRT = (RectTransform)selGO.transform;
            selRT.anchorMin = new Vector2(0, 1);
            selRT.anchorMax = new Vector2(0, 1);
            selRT.pivot = new Vector2(0, 1);
            selRT.sizeDelta = new Vector2(50, 50);
            // boardRect also carries a GridLayoutGroup, which otherwise fights us for control of
            // every child's RectTransform (it was silently snapping this into "the next grid slot"
            // after the last cell every layout pass - that's why it looked stuck in one spot).
            // Ignoring layout hands full manual control of position/size back to UpdateSelectionVisual.
            var layoutElement = selGO.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(selRT, false);
            var fillRT = (RectTransform)fillGO.transform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.GetComponent<Image>();
            fillImg.color = new Color(1f, 0.35f, 0.3f, 0.18f);
            fillImg.raycastTarget = false;

            const float thickness = 5f;
            var borderColor = new Color(1f, 0.25f, 0.2f, 0.95f);
            AddBorderBar(selRT, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, thickness), borderColor);
            AddBorderBar(selRT, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, thickness), borderColor);
            AddBorderBar(selRT, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(thickness, 0), borderColor);
            AddBorderBar(selRT, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(thickness, 0), borderColor);

            selRT.SetAsLastSibling();
            selGO.SetActive(false);
            return selRT;
        }

        static void AddBorderBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.OnBoardRebuilt += RebuildViews;
            gameManager.OnCellsCleared += HandleCellsCleared;
        }

        void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.OnBoardRebuilt -= RebuildViews;
            gameManager.OnCellsCleared -= HandleCellsCleared;
        }

        void RebuildViews()
        {
            var model = gameManager.Model;
            LayoutGrid(model.Rows, model.Cols);

            if (views == null || views.GetLength(0) != model.Rows || views.GetLength(1) != model.Cols)
            {
                foreach (Transform child in boardRect)
                    if (child != selectionIndicator) Destroy(child.gameObject);

                views = new LemonCellView[model.Rows, model.Cols];
                for (int r = 0; r < model.Rows; r++)
                    for (int c = 0; c < model.Cols; c++)
                    {
                        var view = Instantiate(cellPrefab, boardRect);
                        view.Init(r, c);
                        views[r, c] = view;
                    }

                if (selectionIndicator != null) selectionIndicator.SetAsLastSibling();
            }

            for (int r = 0; r < model.Rows; r++)
                for (int c = 0; c < model.Cols; c++)
                {
                    views[r, c].ResetVisualState();
                    views[r, c].SetValue(model.Grid[r, c]);
                }

            ClearSelectionVisual();
        }

        void LayoutGrid(int rows, int cols)
        {
            if (gridLayout == null) return;
            const float spacing = 2f;
            float w = boardRect.rect.width;
            float h = boardRect.rect.height;
            cellPitchX = w / cols;
            cellPitchY = h / rows;
            gridLayout.padding = new RectOffset(0, 0, 0, 0);
            gridLayout.spacing = new Vector2(spacing, spacing);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = cols;
            gridLayout.cellSize = new Vector2(Mathf.Max(2f, cellPitchX - spacing), Mathf.Max(2f, cellPitchY - spacing));
        }

        void HandleCellsCleared(List<Vector2Int> cells, int scoreGained)
        {
            foreach (var cell in cells)
                views[cell.y, cell.x].PlayClearAnimation(null);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (gameManager == null || !gameManager.IsRunning || views == null) return;
            dragStart = dragCurrent = ScreenToCell(eventData);
            dragging = true;
            UpdateSelectionVisual();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragCurrent = ScreenToCell(eventData);
            UpdateSelectionVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;

            var cells = gameManager.Model.GetNonEmptyCellsInRect(dragStart, dragCurrent);
            gameManager.SubmitSelection(cells);

            ClearSelectionVisual();
        }

        Vector2Int ScreenToCell(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, eventData.position, eventData.pressEventCamera, out var local);
            Rect rect = boardRect.rect;
            float nx = (local.x - rect.xMin) / rect.width;
            float ny = 1f - (local.y - rect.yMin) / rect.height; // row 0 at the top, matching the model
            int cols = gameManager.Model.Cols, rows = gameManager.Model.Rows;
            int col = Mathf.Clamp(Mathf.FloorToInt(nx * cols), 0, cols - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt(ny * rows), 0, rows - 1);
            return new Vector2Int(col, row);
        }

        void UpdateSelectionVisual()
        {
            var cells = gameManager.Model.GetNonEmptyCellsInRect(dragStart, dragCurrent);
            int sum = gameManager.Model.SumOf(cells);

            for (int r = 0; r < views.GetLength(0); r++)
                for (int c = 0; c < views.GetLength(1); c++)
                    views[r, c].SetSelected(false);
            foreach (var cell in cells) views[cell.y, cell.x].SetSelected(true);

            if (selectionIndicator != null)
            {
                int r0 = Mathf.Min(dragStart.y, dragCurrent.y), r1 = Mathf.Max(dragStart.y, dragCurrent.y);
                int c0 = Mathf.Min(dragStart.x, dragCurrent.x), c1 = Mathf.Max(dragStart.x, dragCurrent.x);
                selectionIndicator.gameObject.SetActive(true);
                selectionIndicator.anchoredPosition = new Vector2(c0 * cellPitchX, -(r0 * cellPitchY));
                selectionIndicator.sizeDelta = new Vector2((c1 - c0 + 1) * cellPitchX, (r1 - r0 + 1) * cellPitchY);
            }

            if (currentSumText != null)
            {
                bool hasCells = cells.Count > 0;
                currentSumText.text = hasCells ? $"{sum} / {gameManager.Target}" : string.Empty;
                bool validMatch = hasCells && sum == gameManager.Target && cells.Count >= gameManager.minClearCells;
                currentSumText.color = !hasCells
                    ? sumNeutralColor
                    : (validMatch ? sumMatchColor : (sum >= gameManager.Target ? sumOverColor : sumNeutralColor));
            }
        }

        void ClearSelectionVisual()
        {
            if (views != null)
                for (int r = 0; r < views.GetLength(0); r++)
                    for (int c = 0; c < views.GetLength(1); c++)
                        views[r, c].SetSelected(false);
            if (selectionIndicator != null) selectionIndicator.gameObject.SetActive(false);
            if (currentSumText != null) currentSumText.text = string.Empty;
        }
    }
}
