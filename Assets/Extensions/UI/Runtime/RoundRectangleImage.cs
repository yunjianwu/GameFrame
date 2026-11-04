// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

[AddComponentMenu("UI/RoundRectangleImage")]
public class RoundRectangleImage : Image
{
    public int segements = 10;
    public int roundRectangleRadius = 1;
    public float fillPercent = 1;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float tw = rectTransform.rect.width;
        float th = rectTransform.rect.height;
        if (fillPercent <= 0 || tw <= 0 || th <= 0) return;
        int safeSegments = Mathf.Clamp(segements, 1, 128);
        float degreeDelta = 2 * Mathf.PI / (safeSegments * 4);
        int curSegements = Mathf.Max(1, Mathf.CeilToInt((safeSegments + 1) * 4 * Mathf.Clamp01(fillPercent)));
        Vector2 halfSize = rectTransform.rect.size / 2;
        float radius = Mathf.Clamp(roundRectangleRadius, 0, Mathf.Min(halfSize.x, halfSize.y));

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
        verticeCount = curSegements + 1;
        uiVertex = new UIVertex
        {
            color = color,
            position = curVertice + pivotVector,
            uv0 = new Vector2(curVertice.x * uvScaleX, curVertice.y * uvScaleY) + center
        };
        vh.AddVert(uiVertex);

        for (int j = 0; j < 4; j++)
        {
            for (int i = 0; i < safeSegments + 1 && j * (safeSegments + 1) + i < curSegements; i++)
            {
                float cosA = Mathf.Cos(curDegree);
                float sinA = Mathf.Sin(curDegree);
                switch (j)
                {
                    case 0:
                        curVertice = new Vector2((cosA - 1) * radius + halfSize.x, (sinA - 1) * radius + halfSize.y);
                        break;
                    case 1:
                        curVertice = new Vector2((cosA + 1) * radius - halfSize.x, (sinA - 1) * radius + halfSize.y);
                        break;
                    case 2:
                        curVertice = new Vector2((cosA + 1) * radius - halfSize.x, (sinA + 1) * radius - halfSize.y);
                        break;
                    case 3:
                        curVertice = new Vector2((cosA - 1) * radius + halfSize.x, (sinA + 1) * radius - halfSize.y);
                        break;
                }
                if (i < safeSegments)
                    curDegree += degreeDelta;
                uiVertex = new UIVertex
                {
                    color = color,
                    position = curVertice + pivotVector,
                    uv0 = new Vector2(curVertice.x * uvScaleX, curVertice.y * uvScaleY) + center
                };
                vh.AddVert(uiVertex);
            }
        }

        triangleCount = curSegements * 3;
        for (int i = 0, vIdx = 1; i < triangleCount - 3; i += 3, vIdx++)
        {
            vh.AddTriangle(vIdx, 0, vIdx + 1);
        }
        if (fillPercent >= 1)
        {
            //首尾顶点相连
            vh.AddTriangle(verticeCount - 1, 0, 1);
        }
    }
}
}
