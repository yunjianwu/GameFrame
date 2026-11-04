using System;
using System.Collections;
using Spine.Unity;
using UnityEngine;

namespace Starter.Extensions.Spine
{
    [RequireComponent(typeof(SkeletonGraphic))]
    public sealed class SpineTransition : MonoBehaviour
    {
        private SkeletonGraphic graphic;
        private Coroutine routine;
        private float originalAlpha = 1;
        private void Awake() { graphic = GetComponent<SkeletonGraphic>(); originalAlpha = graphic.color.a; }

        public void FadeTo(float alpha, float seconds, Action completed = null)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (graphic == null) graphic = GetComponent<SkeletonGraphic>();
            if (seconds <= 0 || !isActiveAndEnabled) { SetAlpha(alpha); completed?.Invoke(); return; }
            routine = StartCoroutine(Fade(Mathf.Clamp01(alpha), seconds, completed));
        }

        private IEnumerator Fade(float alpha, float seconds, Action completed)
        {
            float start = graphic.color.a;
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(start, alpha, elapsed / seconds));
                yield return null;
            }
            SetAlpha(alpha);
            routine = null;
            completed?.Invoke();
        }
        private void SetAlpha(float alpha) { Color color = graphic.color; color.a = Mathf.Clamp01(alpha); graphic.color = color; }
        public void VnishTransition(float seconds, Action completed = null) { FadeTo(0, seconds, completed); }
        public void ShowTransition(float seconds, Action completed = null) { FadeTo(originalAlpha, seconds, completed); }
        private void OnDisable() { if (routine != null) StopCoroutine(routine); routine = null; }
    }
}
