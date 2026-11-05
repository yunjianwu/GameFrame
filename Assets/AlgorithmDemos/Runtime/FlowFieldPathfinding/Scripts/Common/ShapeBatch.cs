using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Demos.FlowFieldPathfinding
{
    /// <summary>
    /// 每帧重建的一张动态网格，用 Graphics.DrawMesh 提交，Built-in / URP 通用。
    /// 坐标单位为像素、屏幕中心为原点、Y 轴朝上（DemoCamera 会把正交相机设成 1 单位 = 1 像素）。
    /// 所有图形在同一个 Mesh 里，按添加顺序绘制，后加的盖在先加的上面。
    /// </summary>
    public sealed class ShapeBatch
    {
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Color32> colors = new List<Color32>(4096);
        private readonly List<int> indices = new List<int>(8192);

        public ShapeBatch(string name)
        {
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            // Sprites/Default：无光照、顶点色、Alpha 混合，两条管线下都可用，且默认在 Always Included Shaders 里
            material = new Material(Shader.Find("Sprites/Default")) { name = name };
        }

        public int VertexCount => vertices.Count;

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            indices.Add(i);
            indices.Add(i + 1);
            indices.Add(i + 2);
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            for (int k = 0; k < 4; k++) colors.Add(color);
            indices.Add(i);
            indices.Add(i + 1);
            indices.Add(i + 2);
            indices.Add(i);
            indices.Add(i + 2);
            indices.Add(i + 3);
        }

        /// <summary>左下角 (x, y)、宽 w、高 h 的实心矩形</summary>
        public void Rect(float x, float y, float w, float h, Color32 color)
        {
            Quad(new Vector2(x, y), new Vector2(x, y + h), new Vector2(x + w, y + h), new Vector2(x + w, y), color);
        }

        public void RectOutline(float x, float y, float w, float h, float thickness, Color32 color)
        {
            Rect(x, y, w, thickness, color);
            Rect(x, y + h - thickness, w, thickness, color);
            Rect(x, y + thickness, thickness, h - thickness * 2, color);
            Rect(x + w - thickness, y + thickness, thickness, h - thickness * 2, color);
        }

        public void Line(Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector2 n = new Vector2(-d.y, d.x) * (width * 0.5f / len);
            Quad(a - n, a + n, b + n, b - n, color);
        }

        public void Circle(Vector2 center, float radius, Color32 color, int segments = 12)
        {
            int c = vertices.Count;
            vertices.Add(center);
            colors.Add(color);
            for (int s = 0; s < segments; s++)
            {
                float t = s * Mathf.PI * 2f / segments;
                vertices.Add(center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius);
                colors.Add(color);
            }
            for (int s = 0; s < segments; s++)
            {
                indices.Add(c);
                indices.Add(c + 1 + (s + 1) % segments);
                indices.Add(c + 1 + s);
            }
        }

        public void Ring(Vector2 center, float radius, float width, Color32 color, int segments = 48)
        {
            float r0 = radius - width * 0.5f;
            float r1 = radius + width * 0.5f;
            for (int s = 0; s < segments; s++)
            {
                float t0 = s * Mathf.PI * 2f / segments;
                float t1 = (s + 1) * Mathf.PI * 2f / segments;
                Vector2 d0 = new Vector2(Mathf.Cos(t0), Mathf.Sin(t0));
                Vector2 d1 = new Vector2(Mathf.Cos(t1), Mathf.Sin(t1));
                Quad(center + d0 * r0, center + d0 * r1, center + d1 * r1, center + d1 * r0, color);
            }
        }

        /// <summary>上传并提交本帧网格，在 Update / LateUpdate 里调用</summary>
        public void Draw(Camera camera)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 10f));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, camera);
        }

        public void Destroy()
        {
            Object.Destroy(mesh);
            Object.Destroy(material);
        }
    }
}
