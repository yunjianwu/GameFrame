using UnityEngine;

namespace Starter.Extensions.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class FingerPointer : MonoBehaviour
    {
        [SerializeField] private RectTransform finger;
        [SerializeField] private Vector2 screenOffset;
        [SerializeField] private float floatAmplitude = 8;
        [SerializeField] private float pulseAmplitude = 0.06f;
        private RectTransform first;
        private RectTransform second;
        private Vector3 baseScale;
        private float started;
        private float travel = 0.7f;
        private float pauseA;
        private float pauseB = 0.25f;
        private bool pingPong;

        private void Awake()
        {
            if (finger == null) finger = (RectTransform)transform;
            baseScale = finger.localScale;
        }

        public void PointTo(RectTransform target)
        {
            first = target;
            second = null;
            Show();
        }

        public void SetSlideBetween(RectTransform a, RectTransform b, float duration)
        {
            first = a; second = b; travel = Mathf.Max(0.05f, duration); pingPong = true; Show();
        }

        public void SetRepeatBetween(RectTransform a, RectTransform b, float duration, float pauseAtB, float pauseAtA = 0)
        {
            first = a; second = b; travel = Mathf.Max(0.05f, duration);
            pauseA = Mathf.Max(0, pauseAtA); pauseB = Mathf.Max(0, pauseAtB); pingPong = false; Show();
        }

        private void Show()
        {
            if (finger == null) { finger = (RectTransform)transform; baseScale = finger.localScale; }
            started = Time.unscaledTime;
            finger.gameObject.SetActive(first != null);
            if (first == null) return;
            finger.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            LateUpdate();
        }

        public void Hide()
        {
            first = second = null;
            if (finger == null) return;
            finger.localScale = baseScale;
            finger.gameObject.SetActive(false);
        }

        private void OnDisable() { first = second = null; if (finger != null) finger.localScale = baseScale; }

        private void LateUpdate()
        {
            if (first == null || !first.gameObject.activeInHierarchy || finger == null) return;
            float elapsed = Time.unscaledTime - started;
            Vector2 screen = ScreenCenter(first);
            if (second != null)
            {
                float phase = Mathf.Repeat(elapsed, pauseA + travel + pauseB);
                float t = pingPong ? Mathf.PingPong(elapsed / travel, 1) : Mathf.Clamp01((phase - pauseA) / travel);
                screen = Vector2.Lerp(screen, ScreenCenter(second), t);
            }
            else screen.y += Mathf.Sin(elapsed * Mathf.PI * 2 / 0.8f) * floatAmplitude;
            var parent = finger.parent as RectTransform;
            if (parent == null) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen + screenOffset, CameraFor(finger), out var position))
                finger.position = position;
            finger.localScale = baseScale * (second != null ? 1 : 1 + (Mathf.Sin(elapsed * 7.85f) * 0.5f + 0.5f) * pulseAmplitude);
        }

        private static Vector2 ScreenCenter(RectTransform target)
        {
            return RectTransformUtility.WorldToScreenPoint(CameraFor(target), target.TransformPoint(target.rect.center));
        }

        private static Camera CameraFor(RectTransform target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }
}
