// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/Gradient Color")]
[RequireComponent(typeof(Graphic))]
#if USE_BASE_VERTEX_EFFECT
    public class GradientColor : BaseVertexEffect
#else
public class GradientColor : BaseMeshEffect
#endif
{
    [SerializeField] DIRECTION direction = DIRECTION.Both;
    public DIRECTION Direction { get => direction; set => direction = value; }

    [SerializeField] Color colorTop = Color.white;
    public Color ColorTop { get => colorTop; set => colorTop = value; }

    [SerializeField] Color colorBottom = Color.black;
    public Color ColorBottom { get => colorBottom; set => colorBottom = value; }

    [SerializeField] Color colorLeft = Color.red;
    public Color ColorLeft { get => colorLeft; set => colorLeft = value; }

    [SerializeField] Color colorRight = Color.blue;
    public Color ColorRight { get => colorRight; set => colorRight = value; }

    [SerializeField]
    [Tooltip("是否受自身Griphic组件颜色影响")]
    bool isBeAffectedBySelfColor;
    public bool IsBeAffectedBySelfColor { get => isBeAffectedBySelfColor; set => isBeAffectedBySelfColor = value; }

    [SerializeField]
    [Tooltip("是否启用透明效果")]
    bool isGradientAlpha = true;
    public bool IsGradientAlpha { get => isGradientAlpha; set => isGradientAlpha = value; }

    [SerializeField]
    [Tooltip("是否每行单独应用垂直渐变")]
    bool perLineVerticalGradient = false;
    public bool PerLineVerticalGradient { get => perLineVerticalGradient; set => perLineVerticalGradient = value; }

    [SerializeField]
    [Tooltip("垂直渐变曲线")]
    AnimationCurve verticalGradientCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve VerticalGradientCurve 
    { 
        get => verticalGradientCurve; 
        set => verticalGradientCurve = value; 
    }

    [SerializeField]
    [Tooltip("水平渐变曲线")]
    AnimationCurve horizontalGradientCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve HorizontalGradientCurve 
    { 
        get => horizontalGradientCurve; 
        set => horizontalGradientCurve = value; 
    }

#if USE_BASE_VERTEX_EFFECT
        public override void ModifyVertices (List<UIVertex> vList)
#else
    public override void ModifyMesh(VertexHelper vh)
    {
        if (IsActive() == false)
        {
            return;
        }

        var vList = new List<UIVertex>();
        vh.GetUIVertexStream(vList);

        ModifyVertices(vList);

        vh.Clear();
        vh.AddUIVertexTriangleStream(vList);
    }

    public void ModifyVertices(List<UIVertex> vList)
#endif
    {
        if (IsActive() == false || vList == null || vList.Count == 0)
        {
            return;
        }

        if (PerLineVerticalGradient && (Direction == DIRECTION.Vertical || Direction == DIRECTION.Both))
        {
            ModifyVerticesPerLine(vList);
        }
        else
        {
            ModifyVerticesGlobal(vList);
        }
    }

    private void ModifyVerticesGlobal(List<UIVertex> vList)
    {
        float topX = 0f, topY = 0f, bottomX = 0f, bottomY = 0f;

        for (int i = 0; i < vList.Count; i++)
        {
            var vertex = vList[i];
            topX = Mathf.Max(topX, vertex.position.x);
            topY = Mathf.Max(topY, vertex.position.y);
            bottomX = Mathf.Min(bottomX, vertex.position.x);
            bottomY = Mathf.Min(bottomY, vertex.position.y);
        }

        float width = topX - bottomX;
        float height = topY - bottomY;

        UIVertex tempVertex = vList[0];
        for (int i = 0; i < vList.Count; i++)
        {
            tempVertex = vList[i];
            byte orgAlpha = tempVertex.color.a;
            Color colorOrg = tempVertex.color;
            if (!IsBeAffectedBySelfColor)
                colorOrg = Color.white;
                
            Color colorV = Color.white;
            Color colorH = Color.white;
            
            if (Direction == DIRECTION.Vertical || Direction == DIRECTION.Both)
            {
                float gradientT = (tempVertex.position.y - bottomY) / height;
                float curvedT = verticalGradientCurve.Evaluate(gradientT);
                colorV = Color.Lerp(ColorBottom, ColorTop, curvedT);
            }
            
            if (Direction == DIRECTION.Horizontal || Direction == DIRECTION.Both)
            {
                float gradientT = (tempVertex.position.x - bottomX) / width;
                float curvedT = horizontalGradientCurve.Evaluate(gradientT);
                colorH = Color.Lerp(ColorLeft, ColorRight, curvedT);
            }

            switch (Direction)
            {
                case DIRECTION.Both:
                    tempVertex.color = colorOrg * colorV * colorH;
                    break;
                case DIRECTION.Vertical:
                    tempVertex.color = colorOrg * colorV;
                    break;
                case DIRECTION.Horizontal:
                    tempVertex.color = colorOrg * colorH;
                    break;
            }
            if (!IsGradientAlpha) tempVertex.color.a = orgAlpha;
            vList[i] = tempVertex;
        }
    }

    private void ModifyVerticesPerLine(List<UIVertex> vList)
    {
        // 按行分组顶点
        var lines = GroupVerticesByLines(vList);

        // 对每行单独应用垂直渐变
        foreach (var line in lines)
        {
            // 计算当前行的顶部和底部Y坐标
            float lineTopY = float.MinValue;
            float lineBottomY = float.MaxValue;

            foreach (int index in line)
            {
                var vertex = vList[index];
                lineTopY = Mathf.Max(lineTopY, vertex.position.y);
                lineBottomY = Mathf.Min(lineBottomY, vertex.position.y);
            }

            float lineHeight = lineTopY - lineBottomY;

            // 应用渐变到当前行的所有顶点
            foreach (int index in line)
            {
                UIVertex tempVertex = vList[index];
                byte orgAlpha = tempVertex.color.a;
                Color colorOrg = tempVertex.color;
                if (!IsBeAffectedBySelfColor)
                    colorOrg = Color.white;

                Color colorV = Color.white;
                if (lineHeight > 0.001f && (Direction == DIRECTION.Vertical || Direction == DIRECTION.Both)) // 避免除零错误
                {
                    float gradientT = (tempVertex.position.y - lineBottomY) / lineHeight;
                    float curvedT = verticalGradientCurve.Evaluate(gradientT);
                    colorV = Color.Lerp(ColorBottom, ColorTop, curvedT);
                }
                else
                {
                    colorV = ColorTop; // 如果行高几乎为0，使用顶部颜色
                }

                Color colorH = Color.white;
                if (Direction == DIRECTION.Both || Direction == DIRECTION.Horizontal)
                {
                    // 计算整段文本的宽度用于水平渐变
                    float globalLeftX = 0f, globalRightX = 0f;
                    for (int i = 0; i < vList.Count; i++)
                    {
                        var vertex = vList[i];
                        globalLeftX = Mathf.Min(globalLeftX, vertex.position.x);
                        globalRightX = Mathf.Max(globalRightX, vertex.position.x);
                    }
                    float globalWidth = globalRightX - globalLeftX;
                    if (globalWidth > 0.001f)
                    {
                        float gradientT = (tempVertex.position.x - globalLeftX) / globalWidth;
                        float curvedT = horizontalGradientCurve.Evaluate(gradientT);
                        colorH = Color.Lerp(ColorLeft, ColorRight, curvedT);
                    }
                }

                switch (Direction)
                {
                    case DIRECTION.Both:
                        tempVertex.color = colorOrg * colorV * colorH;
                        break;
                    case DIRECTION.Vertical:
                        tempVertex.color = colorOrg * colorV;
                        break;
                    case DIRECTION.Horizontal:
                        tempVertex.color = colorOrg * colorH;
                        break;
                }
                if (!IsGradientAlpha) tempVertex.color.a = orgAlpha;
                vList[index] = tempVertex;
            }
        }
    }

    private List<List<int>> GroupVerticesByLines(List<UIVertex> vList)
    {
        var lines = new List<List<int>>();
        var currentLine = new List<int>();
        
        // 基于三角形索引来分组顶点
        // 每3个顶点组成一个三角形
        for (int i = 0; i < vList.Count; i += 3)
        {
            // 获取三角形的平均Y坐标作为代表
            float avgY = (vList[i].position.y + vList[i + 1].position.y + vList[i + 2].position.y) / 3f;
            
            // 如果这是第一个三角形或者与前一个三角形在同一行，则添加到当前行
            if (currentLine.Count == 0 || IsSameLine(avgY, GetAverageY(currentLine, vList)))
            {
                for (int j = i; j < Mathf.Min(i + 3, vList.Count); j++)
                {
                    currentLine.Add(j);
                }
            }
            else
            {
                // 开始新行
                lines.Add(new List<int>(currentLine));
                currentLine.Clear();
                for (int j = i; j < Mathf.Min(i + 3, vList.Count); j++)
                {
                    currentLine.Add(j);
                }
            }
        }
        
        // 添加最后一行
        if (currentLine.Count > 0)
        {
            lines.Add(new List<int>(currentLine));
        }
        
        return lines;
    }

    private bool IsSameLine(float y1, float y2)
    {
        // 使用一个小的阈值来判断是否在同一行
        return Mathf.Abs(y1 - y2) < 2f;
    }

    private float GetAverageY(List<int> indices, List<UIVertex> vList)
    {
        if (indices.Count == 0) return 0f;
        
        float sum = 0f;
        foreach (int index in indices)
        {
            sum += vList[index].position.y;
        }
        return sum / indices.Count;
    }

    /// <summary>
    /// Refresh Gradient Color on playing.
    /// </summary>
    public void Refresh()
    {
        if (graphic != null)
        {
            graphic.SetVerticesDirty();
        }
    }

    public enum DIRECTION
    {
        Vertical,
        Horizontal,
        Both,
    }
}
}
