using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Extensions.UI
{
    [AddComponentMenu("Starter/UI/Hollow Out Mask"), RequireComponent(typeof(CanvasRenderer))]
    public class HollowOutMask : MaskableGraphic, ICanvasRaycastFilter
    {
        [SerializeField, Min(0)] private float padding = 8;
        private readonly List<RectTransform> targets = new List<RectTransform>();
        private readonly Vector3[] corners = new Vector3[4];
        private Rect hole;
        private bool hasHole;

        public void SetTarget(RectTransform target) { SetTargets(target); }

        public void SetTargets(params RectTransform[] values)
        {
            targets.Clear();
            if (values != null)
                foreach (var value in values) if (value != null && !targets.Contains(value)) targets.Add(value);
            gameObject.SetActive(true);
            RefreshBounds();
            SetVerticesDirty();
        }

        public void ClearTargets() { targets.Clear(); hasHole = false; SetVerticesDirty(); }
        public void ClearTargetList() { ClearTargets(); }
        public void Hide() { ClearTargets(); gameObject.SetActive(false); }

        private void LateUpdate() { RefreshBounds(); }

        private void RefreshBounds()
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool found = false;
            foreach (var target in targets)
            {
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                target.GetWorldCorners(corners);
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector2 point = rectTransform.InverseTransformPoint(corners[i]);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                found = true;
            }
            Rect next = found ? Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding) : default;
            if (found == hasHole && next == hole) return;
            hasHole = found;
            hole = next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect outer = rectTransform.rect;
            if (!hasHole) { AddRect(vh, outer); return; }
            float left = Mathf.Clamp(hole.xMin, outer.xMin, outer.xMax);
            float right = Mathf.Clamp(hole.xMax, outer.xMin, outer.xMax);
            float bottom = Mathf.Clamp(hole.yMin, outer.yMin, outer.yMax);
            float top = Mathf.Clamp(hole.yMax, outer.yMin, outer.yMax);
            AddRect(vh, Rect.MinMaxRect(outer.xMin, outer.yMin, left, outer.yMax));
            AddRect(vh, Rect.MinMaxRect(right, outer.yMin, outer.xMax, outer.yMax));
            AddRect(vh, Rect.MinMaxRect(left, outer.yMin, right, bottom));
            AddRect(vh, Rect.MinMaxRect(left, top, right, outer.yMax));
        }

        private void AddRect(VertexHelper vh, Rect rect)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            int index = vh.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector2(rect.xMin, rect.yMin); vh.AddVert(vertex);
            vertex.position = new Vector2(rect.xMin, rect.yMax); vh.AddVert(vertex);
            vertex.position = new Vector2(rect.xMax, rect.yMax); vh.AddVert(vertex);
            vertex.position = new Vector2(rect.xMax, rect.yMin); vh.AddVert(vertex);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            foreach (var target in targets)
                if (target != null && target.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(target, screenPoint, eventCamera)) return false;
            return true;
        }
    }
}
