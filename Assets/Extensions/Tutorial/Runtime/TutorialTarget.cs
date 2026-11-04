using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Starter.Extensions.Tutorial
{
    [RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public sealed class TutorialTarget : MonoBehaviour, IPointerClickHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private string targetId;
        [SerializeField] private TutorialTargetRegistry registry;
        private Button button;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        public string Id => targetId;
        public RectTransform Rect => (RectTransform)transform;
        public event Action Clicked;
        public event Action<string> DraggedTo;

        public void Configure(string id, TutorialTargetRegistry owner)
        {
            Unbind();
            targetId = id;
            registry = owner;
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() { if (!string.IsNullOrWhiteSpace(targetId)) Bind(); }
        private void OnDisable() { Unbind(); }

        private void Bind()
        {
            if (registry == null) registry = GetComponentInParent<TutorialTargetRegistry>();
            if (registry == null) return;
            registry.Register(this);
            button = GetComponent<Button>();
            if (button != null) { button.onClick.RemoveListener(RaiseClick); button.onClick.AddListener(RaiseClick); }
        }

        private void Unbind()
        {
            if (button != null) button.onClick.RemoveListener(RaiseClick);
            if (registry != null) registry.Unregister(this);
        }

        private void RaiseClick() { Clicked?.Invoke(); }
        public void OnPointerClick(PointerEventData data)
        {
            if (button == null && isActiveAndEnabled && data.button == PointerEventData.InputButton.Left) RaiseClick();
        }
        public void OnDrag(PointerEventData data) { }
        public void OnEndDrag(PointerEventData data)
        {
            if (!isActiveAndEnabled || data.button != PointerEventData.InputButton.Left || EventSystem.current == null) return;
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            foreach (var hit in hits)
            {
                var target = hit.gameObject.GetComponentInParent<TutorialTarget>();
                if (target != null && target != this && target.registry == registry)
                {
                    DraggedTo?.Invoke(target.Id);
                    break;
                }
            }
            hits.Clear();
        }
    }
}
