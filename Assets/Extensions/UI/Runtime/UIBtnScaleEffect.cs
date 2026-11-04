using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Starter.Extensions.UI
{
    [DisallowMultipleComponent]
    public sealed class UIBtnScaleEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField, Range(0.1f, 1)] private float downScale = 0.88f;
        [SerializeField, Min(0)] private float downDuration = 0.08f;
        [SerializeField, Min(0)] private float upDuration = 0.12f;
        private Vector3 baseline;
        private Vector3 from;
        private Vector3 desired;
        private float elapsed;
        private float duration;
        private Selectable selectable;

        private void Awake()
        {
            if (target == null) target = (RectTransform)transform;
            baseline = target.localScale;
            from = desired = baseline;
            selectable = GetComponent<Selectable>();
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || (selectable != null && !selectable.IsInteractable())) return;
            MoveTo(baseline * downScale, downDuration);
        }
        public void OnPointerUp(PointerEventData data) { MoveTo(baseline, upDuration); }
        public void OnPointerExit(PointerEventData data) { MoveTo(baseline, upDuration); }
        private void MoveTo(Vector3 scale, float seconds)
        {
            if (target == null) return;
            from = target.localScale; desired = scale; elapsed = 0; duration = seconds;
            if (seconds <= 0) target.localScale = scale;
        }
        private void Update()
        {
            if (target == null || target.localScale == desired) return;
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
            target.localScale = Vector3.LerpUnclamped(from, desired, t * (2 - t));
        }
        private void OnDisable()
        {
            if (target != null) target.localScale = baseline;
            desired = baseline;
        }
    }
}