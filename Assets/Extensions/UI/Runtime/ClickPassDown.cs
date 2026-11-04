using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Starter.Extensions.UI
{
    // Prefer raycastTarget=false for decoration. Use this only when an overlay also handles the pointer.
    public sealed class ClickPassDown : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public bool PassOnce = true;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private static bool forwarding;
        public void OnPointerDown(PointerEventData data) { Pass(data, ExecuteEvents.pointerDownHandler); }
        public void OnPointerUp(PointerEventData data) { Pass(data, ExecuteEvents.pointerUpHandler); }
        public void OnPointerClick(PointerEventData data) { Pass(data, ExecuteEvents.pointerClickHandler); }

        private void Pass<T>(PointerEventData data, ExecuteEvents.EventFunction<T> handler) where T : IEventSystemHandler
        {
            if (forwarding || EventSystem.current == null) return;
            forwarding = true;
            try
            {
                hits.Clear();
                EventSystem.current.RaycastAll(data, hits);
                bool reachedSelf = false;
                foreach (var hit in hits)
                {
                    if (hit.gameObject == gameObject) { reachedSelf = true; continue; }
                    if (!reachedSelf || hit.gameObject.transform.IsChildOf(transform)) continue;
                    var receiver = ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, handler);
                    if (receiver != null && PassOnce) break;
                }
            }
            finally { hits.Clear(); forwarding = false; }
        }
    }
}
