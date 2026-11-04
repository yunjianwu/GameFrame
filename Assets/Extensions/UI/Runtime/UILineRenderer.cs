using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Extensions.UI
{
    [AddComponentMenu("Starter/UI/Line Renderer"), RequireComponent(typeof(CanvasRenderer))]
    public sealed class UILineRenderer : MaskableGraphic
    {
        [SerializeField] private List<Vector2> points = new List<Vector2>();
        [SerializeField, Min(0.1f)] private float lineWidth = 4;

        public void DrawLine(Vector2[] values) { SetPoints(values); }
        public void SetPoints(IEnumerable<Vector2> values)
        {
            points.Clear();
            if (values != null) points.AddRange(values);
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 from = points[i - 1], to = points[i];
                Vector2 delta = to - from;
                if (delta.sqrMagnitude < 0.0001f) continue;
                Vector2 normal = new Vector2(-delta.y, delta.x).normalized * lineWidth * 0.5f;
                int index = vh.currentVertCount;
                var vertex = UIVertex.simpleVert; vertex.color = color;
                vertex.position = from - normal; vh.AddVert(vertex);
                vertex.position = from + normal; vh.AddVert(vertex);
                vertex.position = to + normal; vh.AddVert(vertex);
                vertex.position = to - normal; vh.AddVert(vertex);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index + 2, index + 3, index);
            }
        }
    }
}
