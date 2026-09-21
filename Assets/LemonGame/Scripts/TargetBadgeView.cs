using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LemonPuzzle
{
    /// <summary>The lemon-shaped target badge in the HUD - same sprite as the grid cells, just bigger.</summary>
    public class TargetBadgeView : MonoBehaviour
    {
        [SerializeField] Text targetText;
        [SerializeField] RectTransform badgeRect;
        [SerializeField] float popScale = 1.25f;
        [SerializeField] float shakeDistance = 8f;
        [SerializeField] float animDuration = 0.3f;

        Vector2 basePos;
        Coroutine anim;

        void Awake()
        {
            if (badgeRect == null) badgeRect = (RectTransform)transform;
            basePos = badgeRect.anchoredPosition;
        }

        public void SetTarget(int value)
        {
            if (targetText != null) targetText.text = value.ToString();
        }

        /// <summary>Play on a successful clear.</summary>
        public void PlayHit()
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(PopRoutine());
        }

        /// <summary>Play on a miss (wrong sum, or a blocked single-cell attempt).</summary>
        public void PlayMiss()
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(ShakeRoutine());
        }

        IEnumerator PopRoutine()
        {
            float t = 0f;
            while (t < animDuration)
            {
                t += Time.deltaTime;
                float k = t / animDuration;
                float scale = k < 0.4f
                    ? Mathf.Lerp(1f, popScale, k / 0.4f)
                    : Mathf.Lerp(popScale, 1f, (k - 0.4f) / 0.6f);
                badgeRect.localScale = Vector3.one * scale;
                yield return null;
            }
            badgeRect.localScale = Vector3.one;
        }

        IEnumerator ShakeRoutine()
        {
            float t = 0f;
            while (t < animDuration)
            {
                t += Time.deltaTime;
                float k = t / animDuration;
                float offset = Mathf.Sin(k * Mathf.PI * 4f) * shakeDistance * (1f - k);
                badgeRect.anchoredPosition = basePos + new Vector2(offset, 0f);
                yield return null;
            }
            badgeRect.anchoredPosition = basePos;
        }
    }
}
