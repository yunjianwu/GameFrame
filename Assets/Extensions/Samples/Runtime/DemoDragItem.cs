using Starter.Extensions.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Starter.Extensions.Samples
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class DemoDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public TutorialInputGate gate;
        public string targetId = "drag";
        private Vector2 start;
        private bool dragging;
        public void OnBeginDrag(PointerEventData data)
        {
            dragging = gate == null || gate.Allows(targetId);
            start = ((RectTransform)transform).anchoredPosition;
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging) return;
            var rect = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            rect.anchoredPosition += data.delta / (canvas == null ? 1 : canvas.scaleFactor);
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (dragging) ((RectTransform)transform).anchoredPosition = start;
            dragging = false;
        }
    }
}
