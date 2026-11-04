using System;
using System.Collections;
using UnityEngine;

namespace Starter.Extensions.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CanvasGroupFade : MonoBehaviour
    {
        private Coroutine routine;
        public void FadeTo(float alpha, float seconds, Action completed = null)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            var group = GetComponent<CanvasGroup>();
            if (seconds <= 0 || !isActiveAndEnabled) { group.alpha = Mathf.Clamp01(alpha); completed?.Invoke(); return; }
            routine = StartCoroutine(Fade(group, Mathf.Clamp01(alpha), seconds, completed));
        }
        private IEnumerator Fade(CanvasGroup group, float alpha, float seconds, Action completed)
        {
            float start = group.alpha;
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, alpha, elapsed / seconds);
                yield return null;
            }
            group.alpha = alpha;
            routine = null;
            completed?.Invoke();
        }
        private void OnDisable() { if (routine != null) StopCoroutine(routine); routine = null; }
    }
}
