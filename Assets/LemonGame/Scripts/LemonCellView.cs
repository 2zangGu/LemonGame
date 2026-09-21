using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LemonPuzzle
{
    /// <summary>One grid cell: the lemon sprite + its number. Lives on the LemonCell prefab.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class LemonCellView : MonoBehaviour
    {
        [SerializeField] Image lemonImage;
        [SerializeField] Text valueText;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color selectedColor = new Color(0.75f, 1f, 0.75f, 1f);

        public int Row { get; private set; }
        public int Col { get; private set; }
        public int Value { get; private set; }

        RectTransform rt;

        void Awake()
        {
            rt = (RectTransform)transform;
            if (lemonImage == null) lemonImage = GetComponentInChildren<Image>();
            if (valueText == null) valueText = GetComponentInChildren<Text>();
        }

        public void Init(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public void SetValue(int v)
        {
            Value = v;
            bool empty = v == 0;
            // IMPORTANT: never SetActive(false) here. GridLayoutGroup only lays out active
            // children, so deactivating a cleared cell makes every later cell slide up to fill
            // the gap - that's why a clear near the start was visually "eating" cells at the end.
            // Instead we keep the GameObject active (so it keeps its grid slot) and just hide
            // its visuals, exactly like the original apple game leaving a hole in place.
            if (lemonImage != null) lemonImage.enabled = !empty;
            if (valueText != null) valueText.text = empty ? string.Empty : v.ToString();
        }

        public void SetSelected(bool selected)
        {
            if (lemonImage != null) lemonImage.color = selected ? selectedColor : normalColor;
        }

        public void PlayClearAnimation(Action onComplete)
        {
            StopAllCoroutines();
            StartCoroutine(ClearRoutine(onComplete));
        }

        IEnumerator ClearRoutine(Action onComplete)
        {
            const float duration = 0.22f;
            float t = 0f;
            Vector3 startScale = rt.localScale;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rt.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
                if (lemonImage != null)
                {
                    var col = lemonImage.color;
                    col.a = 1f - k;
                    lemonImage.color = col;
                }
                yield return null;
            }
            SetValue(0);
            onComplete?.Invoke();
        }

        /// <summary>
        /// Cancels any in-flight clear animation and restores the cell to a clean, reusable state.
        /// Always call this before re-showing a cell for a new board (a regenerate can happen in the
        /// same frame as a clear animation starting, so this also stops that coroutine mid-flight).
        /// </summary>
        public void ResetVisualState()
        {
            StopAllCoroutines();
            rt.localScale = Vector3.one;
            if (lemonImage != null) lemonImage.color = normalColor;
        }
    }
}
