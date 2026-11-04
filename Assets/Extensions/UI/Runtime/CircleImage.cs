// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

[AddComponentMenu("UI/CircleImage")]
public class CircleImage : Image
{
    [SerializeField] [Tooltip("组成圆的三角面个数")] int segements = 40;
    public int Segements { get => segements; set { segements = Mathf.Clamp(value, 3, 512); SetVerticesDirty(); } }

    [SerializeField] [Tooltip("填充系数")] float fillPercent = 1;
    public float FillPercent { get => fillPercent; set { fillPercent = Mathf.Clamp01(value); SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float tw = rectTransform.rect.width;
        float th = rectTransform.rect.height;
        float outerRadius = 0.5f * Mathf.Min(tw, th);
        float degreeDelta = 2 * Mathf.PI / Segements;
        if (FillPercent <= 0 || tw <= 0 || th <= 0) return;
        int curSegements = Mathf.Max(1, Mathf.CeilToInt(Mathf.Clamp(Segements, 3, 512) * Mathf.Clamp01(FillPercent)));

        Vector2 pivotVector = new Vector2(tw * (0.5f - rectTransform.pivot.x), th * (0.5f - rectTransform.pivot.y));
        Vector4 uv = overrideSprite != null ? DataUtility.GetOuterUV(overrideSprite) : new Vector4(0, 0, 1, 1);
        Vector2 center = new Vector2(uv.x + uv.z, uv.y + uv.w) * 0.5f;
        float uvScaleX = (uv.z - uv.x) / tw;
        float uvScaleY = (uv.w - uv.y) / th;

        float curDegree = 0;
        UIVertex uiVertex;
        int verticeCount;
        int triangleCount;
        Vector2 curVertice;

        curVertice = Vector2.zero;
        verticeCount = curSegements + 2;
        uiVertex = new UIVertex
        {
            color = color,
            position = curVertice + pivotVector,
            uv0 = new Vector2(curVertice.x * uvScaleX, curVertice.y * uvScaleY) + center
        };
        vh.AddVert(uiVertex);

        for (int i = 1; i < verticeCount; i++)
        {
            curVertice = new Vector2(Mathf.Cos(curDegree) * outerRadius, Mathf.Sin(curDegree) * outerRadius);
            curDegree = Mathf.Min(curDegree + degreeDelta, Mathf.Clamp01(FillPercent) * 2 * Mathf.PI);

            uiVertex = new UIVertex
            {
                color = color,
                position = curVertice + pivotVector,
                uv0 = new Vector2(curVertice.x * uvScaleX, curVertice.y * uvScaleY) + center
            };
            vh.AddVert(uiVertex);
        }

        triangleCount = curSegements * 3;
        for (int i = 0, vIdx = 1; i < triangleCount; i += 3, vIdx++)
        {
            vh.AddTriangle(vIdx, 0, vIdx + 1);
        }
    }
}
}
