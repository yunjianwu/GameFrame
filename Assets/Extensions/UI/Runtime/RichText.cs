// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;

#endif
public class RichText : Text, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public Func<string, Sprite> SpriteResolver { get; set; }

    /// <summary>
    /// 图片池
    /// </summary>
    private readonly List<Image> m_ImagesPool = new List<Image>();

    /// <summary>
    /// 图片的最后一个顶点的索引
    /// </summary>
    private readonly List<int> m_ImagesVertexIndex = new List<int>();

    /// <summary>
    /// 需要渲染的表情列表
    /// </summary>
    private Dictionary<int, Sprite[]> m_ImageSprites = new Dictionary<int, Sprite[]>();

    private Dictionary<int, int> m_ImagesIndex = new Dictionary<int, int>();

    /// <summary>
    /// 超链接信息列表
    /// </summary>
    private readonly List<HrefInfo> m_HrefInfos = new List<HrefInfo>();

    private UIVertex[] m_TempVerts = new UIVertex[4];

    /// <summary>
    /// 下划线信息列表
    /// </summary>
    private readonly List<UnderlineInfo> m_UnderlineInfos = new List<UnderlineInfo>();

    /// <summary>
    /// 文本构造器
    /// </summary>
    protected static readonly StringBuilder s_TextBuilder = new StringBuilder();

    /// <summary>
    /// 正则取出所需要的属性
    /// </summary>
    private static readonly Regex s_Regex =
        new Regex(@"<quad name=(.+?) size=(\d*\.?\d+%?) width=(\d*\.?\d+%?) />", RegexOptions.Singleline);

    /// <summary>
    /// 策划需要配置的格式正则<#47000:1>或<#47000> 其中:后面跟的是动画的帧数 没有:表示单张图 有表示序列帧动画
    /// </summary>
    private static readonly Regex s_SpriteRegex =
        new Regex(@"<#([_/:\w]+)\^?([\d]*)\^?([\d*\.?\d+]*)>", RegexOptions.Singleline);

    /// <summary>
    /// 超链接正则 <a=www.baidu.com>用户协议</a> 格式必须为<a=**>123</a>不能为<a>123</a>,若需要特殊的函数调用需要 
    /// label.onHrefClick:RemoveAllListeners() label.onHrefClick:AddListener(function(str) print(str) end)
    /// </summary>
    private static readonly Regex s_HrefRegex =
        new Regex(@"<a=([^>\n\s]+)>(.*?)(?:</a>)", RegexOptions.Singleline);

    /// <summary>
    /// 下划线正则 <uline>123</uline>或<uline=color=#878787>123</uline>
    /// </summary>
    private static readonly Regex s_UnderlineRegex =
        new Regex(@"<uline(?:=color=(.+?))?>(.*?)(?:</uline>)", RegexOptions.Singleline);

    ///<summary>
    /// Unity2019 之后的版本 需要剔除空格 换行 和 富文本标签占的顶点
    ///<summary>
    private static readonly Regex s_VertexFilter = new Regex(@"(<(.+?)>|[ \n\r\t]+)", RegexOptions.Singleline);

    [Serializable]
    public class HrefClickEvent : UnityEvent<string>
    {
    }

    [SerializeField] private HrefClickEvent m_OnHrefClick = new HrefClickEvent();

    /// <summary>解决空格自动换行问题</summary>
    public static string ReplaceSpace(string sourceStr)
    {
        return sourceStr.Replace(" ", "<color=#00000000>.</color>");
    }

    /// <summary>
    /// 超链接点击事件
    /// </summary>
    public HrefClickEvent onHrefClick
    {
        get { return m_OnHrefClick; }
        set { m_OnHrefClick = value; }
    }

    public override float preferredWidth
    {
        get
        {
            var settings = GetGenerationSettings(Vector2.zero);
            return cachedTextGeneratorForLayout.GetPreferredWidth(m_OutputText, settings) / pixelsPerUnit;
        }
    }

    public override float preferredHeight
    {
        get
        {
            //var settings = GetGenerationSettings(new Vector2(GetPixelAdjustedRect().size.x, 0.0f));
            //替换上面的原因是特殊情况下精度问题会导致高度增加一倍
            var settings = GetGenerationSettings(rectTransform.rect.size);
            return cachedTextGeneratorForLayout.GetPreferredHeight(m_OutputText, settings) / pixelsPerUnit;
        }
    }

    private RectTransform rect = null;

    public override void SetVerticesDirty()
    {
        if (rect == null)
        {
            rect = transform as RectTransform; //gameObject.GetComponent<RectTransform>();
        }

        base.SetVerticesDirty();
        UpdateQuadImage();
    }

    /// <summary>
    /// 解析完最终的文本
    /// </summary>
    private string m_OutputText;

    protected void UpdateQuadImage()
    {
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.GetPrefabAssetType(this) == UnityEditor.PrefabAssetType.Regular)
        {
            return;
        }
#endif
        ClearSprites();
        m_OutputText = GetOutputText(text);
        var filters = s_VertexFilter.Matches(m_OutputText);
        m_ImagesVertexIndex.Clear();
        m_ImageSprites.Clear();
        m_ImagesIndex.Clear();
        foreach (Match match in s_Regex.Matches(m_OutputText))
        {
            var picIndex = match.Index;
            var endIndex = picIndex * 4 + 3;
            //float height = (fontSize + lineSpacing) * 2;
            //if (preferredHeight > height)
#if !UNITY_2019_4_OR_NEWER || UNITY_2019_4_3
                //unity2019.4以上除了2019.4.3的版本 如：unity2019.4.34版本顶点索引数据里面不包含标签 只有要显示的文字
                //如：m_OutputText="<size=18><color=#ffffff>123<#31000></color></size>"
                //则顶点索引数据里最多有16个  123文字有12个顶点索引 外加表情图片4个顶点索引共16个

                //unity2019.4以下更低的版本和unity2019.4.3顶点索引数据里面包含所有的标签 
                //如：m_OutputText="<size=18><color=#ffffff>123<#31000></color></size>"
                //则顶点索引数据里最多有200个  "<size=18><color=#ffffff>123<#31000></color></size>"共50个字
                //每个字4个顶点索引所以共200个 其中表情图片的顶点索引开始值应该为27*4=108 结束顶点索引为108+3=111
                if (preferredWidth > rect.sizeDelta.x)
                {
                    m_ImagesVertexIndex.Add(endIndex);
                }
                else
#endif
            {
                for (int idx = 0; idx < filters.Count; idx++)
                {
                    var filter = filters[idx];
                    if (filter.Index >= match.Index)
                        break;
                    picIndex -= filter.Length;
                }

                //因为\<quad>标签对应的纹理还是会渲染的，并且仍然占4个顶点，而每个\<quad>会对应一张图片
                //所以这里加回前面的图片数量，就是加回\对应的纹理占的顶点
                picIndex += m_ImagesVertexIndex.Count;
                endIndex = picIndex * 4 + 3;
                m_ImagesVertexIndex.Add(endIndex);
            }

            m_ImagesPool.RemoveAll(image => image == null);
            if (m_ImagesPool.Count == 0)
            {
                GetComponentsInChildren<Image>(m_ImagesPool);
            }

            if (m_ImagesVertexIndex.Count > m_ImagesPool.Count)
            {
                var resources = new DefaultControls.Resources();
                var go = DefaultControls.CreateImage(resources);
                go.AddComponent<LayoutElement>().ignoreLayout = true; //忽略自动排版
                go.layer = gameObject.layer;
                var rt = go.transform as RectTransform;
                if (rt)
                {
                    rt.SetParent(rectTransform);
                    rt.localPosition = Vector3.zero;
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                }

                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                m_ImagesPool.Add(image);
            }

            var spriteName = match.Groups[1].Value;
            int frame = 1;
            var splitNames = spriteName.Split(':');
            if (splitNames.Length > 1)
            {
                spriteName = splitNames[0];
                if (!int.TryParse(splitNames[1], out frame))
                {
                    frame = 1;
                }
            }

            float size = float.Parse(match.Groups[2].Value);
            float width = float.Parse(match.Groups[3].Value);
            var img = m_ImagesPool[m_ImagesVertexIndex.Count - 1];
            if (frame > 1)
            {
                Sprite[] sprites = GetSpritesList(spriteName, frame);
                if (sprites != null && sprites.Length > 0)
                {
                    m_ImageSprites.Add(m_ImagesVertexIndex.Count - 1, sprites);
                    m_ImagesIndex.Add(m_ImagesVertexIndex.Count - 1, 0);
                    img.sprite = sprites[0];
                }
                else
                {
                    img.sprite = null;
                }
            }
            else if (img.sprite == null || img.sprite.name != spriteName)
            {
                LoadSprite(spriteName, img);
            }

            img.rectTransform.sizeDelta = new Vector2(size * width, size);
            img.transform.localScale = string.IsNullOrEmpty(spriteName) ? Vector3.zero : Vector3.one;
        }

        for (var i = m_ImagesVertexIndex.Count; i < m_ImagesPool.Count; i++)
        {
            if (m_ImagesPool[i])
            {
                m_ImagesPool[i].transform.localScale = Vector3.zero;
            }
        }
    }

    //List<BaseRequest> m_SpriteRequests = new List<BaseRequest>();

    private Sprite[] GetSpritesList(string name, int frame = 1)
    {
        int idx = 0;
        bool isDigit = int.TryParse(name, out idx);
        if (!isDigit || frame < 1)
        {
            return null;
        }

        List<Sprite> sprites = new List<Sprite>();
        Sprite s;
        int endIndex = idx + frame;
        do
        {
            string spritName = idx.ToString(); //取帧
            s = GetSprite(spritName);
            if (s != null)
            {
                sprites.Add(s);
            }
            idx++;
        } while (idx < endIndex);

        return sprites.ToArray();
    }

    private Sprite GetSprite(string spriteName)
    {
        return SpriteResolver != null ? SpriteResolver(spriteName) : null;
    }

    private void LoadSprite(string spriteName, Image img)
    {
        img.sprite = GetSprite(spriteName);
    }

    public void RefreshSprites() { SetVerticesDirty(); }

    void ClearSprites()
    {
        // for (int i = 0; i < m_SpriteRequests.Count; ++i)
        // {
        //     m_SpriteRequests[i].Dispose();
        // }
        //
        // m_SpriteRequests.Clear();
    }

    float fTime = 0.0f;

    void Update()
    {
        fTime += Time.deltaTime;
        if (fTime >= 0.1f)
        {
            for (int i = 0; i < m_ImagesVertexIndex.Count; i++)
            {
                var img = m_ImagesPool[i];
                if (img != null && m_ImageSprites.ContainsKey(i))
                {
                    m_ImagesIndex[i] = NextIndex(m_ImageSprites[i].Length, m_ImagesIndex[i]);
                    img.sprite = m_ImageSprites[i][m_ImagesIndex[i]];
                }
            }

            fTime = 0.0f;
        }
    }

    private int NextIndex(int count, int idx)
    {
        int next = idx + 1;
        if (idx >= count - 1)
        {
            next = 0;
        }

        return next;
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        var orignText = m_Text;
        m_Text = m_OutputText;
        base.OnPopulateMesh(toFill);
        m_Text = orignText;

        UIVertex vert = new UIVertex();
        for (var i = 0; i < m_ImagesVertexIndex.Count; i++)
        {
            var endIndex = m_ImagesVertexIndex[i];
            var rt = m_ImagesPool[i].rectTransform;
            var size = rt.sizeDelta;
            if (endIndex < toFill.currentVertCount)
            {
                toFill.PopulateUIVertex(ref vert, endIndex);
                // quad生成的mesh位置是固定底边，mesh大小变化时向上延伸，但是生成的mesh位置相对文字来说偏上，通过一个系数来向下偏移一点
                rt.localPosition = new Vector2(vert.position.x + size.x / 2, vert.position.y + size.y * 0.375f);

                //抹掉左下角的小黑点
                toFill.PopulateUIVertex(ref vert, endIndex - 3);
                var pos = vert.position;
                for (int j = endIndex, m = endIndex - 3; j > m; j--)
                {
                    toFill.PopulateUIVertex(ref vert, endIndex);
                    vert.position = pos;
                    toFill.SetUIVertex(vert, j);
                }
            }
        }

        // 处理超链接包围框
        foreach (var hrefInfo in m_HrefInfos)
        {
            hrefInfo.boxes.Clear();
#if UNITY_2019_1_OR_NEWER
            int startIndex = hrefInfo.newStartIndex;
            int endIndex = hrefInfo.newEndIndex;
#if UNITY_2019_4_3
                //目前发现此版本单行没有优化顶点多行优化了顶点
                if (preferredWidth > rect.sizeDelta.x)
                {
                    startIndex = hrefInfo.startIndex;
                    endIndex = hrefInfo.endIndex;
                }
#endif
#else
                int startIndex = hrefInfo.startIndex;
                int endIndex = hrefInfo.endIndex;
#endif
            if (startIndex >= toFill.currentVertCount)
            {
                continue;
            }

            // 将超链接里面的文本顶点索引坐标加入到包围框
            toFill.PopulateUIVertex(ref vert, startIndex + 3);
            var pos = vert.position;
            var bounds = new Bounds(pos, Vector3.zero);
            for (int i = startIndex + 3, m = endIndex; i <= m; i += 4)
            {
                if (i >= toFill.currentVertCount)
                {
                    break;
                }

                toFill.PopulateUIVertex(ref vert, i);
                pos = vert.position;
                if (pos.x < bounds.min.x || pos.y < bounds.min.y) // 换行重新添加包围框
                {
                    hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                    float y = bounds.size.y;
                    bounds = new Bounds(pos, Vector3.zero);
                }

                //else
                //{
                bounds.Encapsulate(pos); // 扩展包围框
                toFill.PopulateUIVertex(ref vert, i - 2);
                bounds.Encapsulate(vert.position);
                //}
            }

            hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
        }

        if (m_UnderlineInfos.Count > 0)
        {
            // 处理下划线包围框
            foreach (var underlineInfo in m_UnderlineInfos)
            {
                underlineInfo.boxes.Clear();
#if UNITY_2019_1_OR_NEWER
                int startIndex = underlineInfo.newStartIndex;
                int endIndex = underlineInfo.newEndIndex;
#if UNITY_2019_4_3
                    //目前发现此版本单行没有优化顶点多行优化了顶点
                    if (preferredWidth > rect.sizeDelta.x)
                    {
                        startIndex = underlineInfo.startIndex;
                        endIndex = underlineInfo.endIndex;
                    }
#endif
#else
                    int startIndex = underlineInfo.startIndex;
                    int endIndex = underlineInfo.endIndex;
#endif
                if (startIndex >= toFill.currentVertCount)
                {
                    continue;
                }

                // 将下划线里面的文本顶点索引坐标加入到包围框
                toFill.PopulateUIVertex(ref vert, startIndex + 3);
                var pos = vert.position;
                var bounds = new Bounds(pos, Vector3.zero);
                for (int i = startIndex + 3, m = endIndex; i <= m; i += 4)
                {
                    if (i >= toFill.currentVertCount)
                    {
                        break;
                    }

                    toFill.PopulateUIVertex(ref vert, i);
                    pos = vert.position;
                    if (pos.x < bounds.min.x || (bounds.min.y - pos.y) >= fontSize / 2f) // 换行重新添加包围框
                    {
                        underlineInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                        bounds = new Bounds(pos, Vector3.zero);
                    }

                    //else
                    //{
                    bounds.Encapsulate(pos); // 扩展包围框
                    toFill.PopulateUIVertex(ref vert, i - 2);
                    bounds.Encapsulate(vert.position);
                    //}
                }

                underlineInfo.boxes.Add(new Rect(bounds.min, bounds.size));
            }

            TextGenerator _UnderlineText = new TextGenerator();
            _UnderlineText.Populate("_", this.GetGenerationSettings(this.rectTransform.rect.size));
            IList<UIVertex> _TUT = _UnderlineText.verts;
            //增加这层判断是因为 控件的rect可能会被设置为0或很小的值导致无法放下一个字也就无法生成顶点了
            if (_TUT.Count > 0)
            {
                foreach (var underlineInfo in m_UnderlineInfos)
                {
#if UNITY_2019_1_OR_NEWER
                    int startIndex = underlineInfo.newStartIndex;
                    int endIndex = underlineInfo.newEndIndex;
#if UNITY_2019_4_3
                        //目前发现此版本单行没有优化顶点多行优化了顶点
                        if (preferredWidth > rect.sizeDelta.x)
                        {
                            startIndex = underlineInfo.startIndex;
                            endIndex = underlineInfo.endIndex;
                        }
#endif
#else
                        int startIndex = underlineInfo.startIndex;
                        int endIndex = underlineInfo.endIndex;
#endif
                    if (startIndex >= toFill.currentVertCount)
                    {
                        continue;
                    }

                    for (int i = 0; i < underlineInfo.boxes.Count; i++)
                    {
                        Vector3 _StartBoxPos = new Vector3(underlineInfo.boxes[i].x, underlineInfo.boxes[i].y, 0.0f);
                        Vector3 _EndBoxPos = _StartBoxPos + new Vector3(underlineInfo.boxes[i].width, underlineInfo.boxes[i].height, 0.0f);
                        AddUnderlineQuad(toFill, _TUT, _StartBoxPos, _EndBoxPos, underlineInfo.color);
                    }
                }
            }
        }
    }

    void AddUnderlineQuad(VertexHelper _VToFill, IList<UIVertex> _VTUT, Vector3 _VStartPos, Vector3 _VEndPos, Color32? color)
    {
        float yOffset = _VStartPos.y - _VTUT[0].position.y;
        float len = _VTUT[1].position.x - _VTUT[0].position.x;
        bool uvX = true; //true表示横着的使用uv中的X计算 false表示竖着的用uv中的Y计算
        float uvLen = _VTUT[1].uv0.x - _VTUT[0].uv0.x;
        if (uvLen == 0)
        {
            uvX = false;
            uvLen = _VTUT[1].uv0.y - _VTUT[0].uv0.y;
        }

        float step = 0;
        //此处增加判断是因为顶点有可能只有4个（因为控件的宽度不够只能放下1个字时只有4个顶点）
        if (_VTUT.Count > 4)
        {
            step = _VTUT[4].position.x - _VTUT[0].position.x;
        }

        int lineCount = (int) (step > 0 ? (_VEndPos.x - _VStartPos.x) / step : 1);
        lineCount = lineCount > 0 ? lineCount : 1;
        UIVertex[] m_TempVerts = new UIVertex[4];
        for (int i = 0; i < lineCount; i++)
        {
            float startX = _VStartPos.x + i * step;
            for (int j = 0; j < 4; j++)
            {
                Vector2 pos = Vector2.zero;
                Vector2 uv0 = _VTUT[j].uv0;
                if (j == 0 || j == 3)
                {
                    pos.x = startX;
                    if (uvX)
                    {
                        uv0.x += 0.2f * uvLen;
                    }
                    else
                    {
                        uv0.y += 0.2f * uvLen;
                    }
                }

                if (j == 1 || j == 2)
                {
                    if (i < lineCount - 1)
                    {
                        pos.x = startX + len;
                    }
                    else
                    {
                        pos.x = _VEndPos.x;
                        if (uvX)
                        {
                            uv0.x -= 0.2f * uvLen;
                        }
                        else
                        {
                            uv0.y -= 0.2f * uvLen;
                        }
                    }
                }

                m_TempVerts[j] = _VTUT[j];
                pos.y = _VTUT[j].position.y + yOffset;
                m_TempVerts[j].position = pos;
                m_TempVerts[j].uv0 = uv0;
                if (color != null)
                {
                    m_TempVerts[j].color = color.Value;
                }
            }

            _VToFill.AddUIVertexQuad(m_TempVerts);
        }
    }

    /// <summary>
    /// 获取解析后的最后输出文本
    /// </summary>
    /// <returns></returns>
    protected string GetOutputText(string outputText)
    {
        string tempText = ReplaceSpace(outputText);
        s_TextBuilder.Length = 0;
        int indexText = 0;

        foreach (Match match in s_SpriteRegex.Matches(tempText))
        {
            s_TextBuilder.Append(tempText.Substring(indexText, match.Index - indexText));
            string size = this.fontSize.ToString();
            if (match.Groups[2].Value != "")
            {
                size = match.Groups[2].Value;
            }

            string width = "1";
            if (match.Groups[3].Value != "")
            {
                width = match.Groups[3].Value;
            }

            s_TextBuilder.Append(string.Format("<quad name={0} size={1} width={2} />", match.Groups[1].Value, size, width));

            indexText = match.Index + match.Length;
        }

        s_TextBuilder.Append(tempText.Substring(indexText, tempText.Length - indexText));

        tempText = s_TextBuilder.ToString();
        s_TextBuilder.Length = 0;
        indexText = 0;
        int newIndex = 0;
        m_HrefInfos.Clear();
        foreach (Match match in s_HrefRegex.Matches(tempText))
        {
            s_TextBuilder.Append(tempText.Substring(indexText, match.Index - indexText));
            //s_TextBuilder.Append("<color=blue>");  // 超链接颜色
            string part = tempText.Substring(indexText, match.Index - indexText);
            var group = match.Groups[1];
            var hrefInfo = new HrefInfo
            {
                startIndex = s_TextBuilder.Length * 4, // 超链接里的文本起始顶点索引
                endIndex = (s_TextBuilder.Length + match.Groups[2].Length - 1) * 4 + 3,
                name = group.Value
            };

#if UNITY_2019_1_OR_NEWER
            newIndex = newIndex + ReplaceRichText(part).Length * 4;
            //newIndex = newIndex + Regex.Replace(part, @"(<(.+?)>|[ \n\r\t]+)", "").Length * 4;
            int newStartIndex = newIndex;
            newIndex = newIndex + ReplaceRichText(match.Groups[2].Value).Length * 4;
            hrefInfo.newStartIndex = newStartIndex;
            hrefInfo.newEndIndex = newIndex;
#endif
            m_HrefInfos.Add(hrefInfo);

            s_TextBuilder.Append(match.Groups[2].Value);
            //s_TextBuilder.Append("</color>");
            indexText = match.Index + match.Length;
        }

        s_TextBuilder.Append(tempText.Substring(indexText, tempText.Length - indexText));

        tempText = s_TextBuilder.ToString();
        s_TextBuilder.Length = 0;
        indexText = 0;
        newIndex = 0;
        m_UnderlineInfos.Clear();
        foreach (Match match in s_UnderlineRegex.Matches(tempText))
        {
            s_TextBuilder.Append(tempText.Substring(indexText, match.Index - indexText));
            string part = tempText.Substring(indexText, match.Index - indexText);
            var underlineInfo = new UnderlineInfo
            {
                startIndex = s_TextBuilder.Length * 4, // 下划线的文本起始顶点索引
                endIndex = (s_TextBuilder.Length + match.Groups[2].Length - 1) * 4 + 3,
            };
            if (!string.IsNullOrEmpty(match.Groups[1].Value))
            {
                underlineInfo.color = HexToColor(match.Groups[1].Value);
            }

#if UNITY_2019_1_OR_NEWER
            newIndex = newIndex + ReplaceRichText(part).Length * 4;
            int newStartIndex = newIndex;
            newIndex = newIndex + ReplaceRichText(match.Groups[2].Value).Length * 4;
            underlineInfo.newStartIndex = newStartIndex;
            underlineInfo.newEndIndex = newIndex;
#endif

            m_UnderlineInfos.Add(underlineInfo);

            s_TextBuilder.Append(match.Groups[2].Value);
            indexText = match.Index + match.Length;
        }

        s_TextBuilder.Append(tempText.Substring(indexText, tempText.Length - indexText));

        return s_TextBuilder.ToString();
    }

    /// <summary>
    /// 换掉富文本
    /// </summary>
    private string ReplaceRichText(string str)
    {
        str = Regex.Replace(str, @"<quad name=(.+?)/>", "1");
        str = Regex.Replace(str, @"<color=(.+?)>", "");
        str = str.Replace("</color>", "");
        str = Regex.Replace(str, @"<a=(.+?)>", "");
        str = str.Replace("</a>", "");
        str = Regex.Replace(str, @"<size=(.+?)>", "");
        str = str.Replace("</size>", "");
        str = Regex.Replace(str, @"<uline(.*?)>", "");
        str = str.Replace("</uline>", "");

        str = str.Replace("\r\n", "1");

        str = str.Replace("<b>", "");
        str = str.Replace("</b>", "");
        //str = str.Replace("<uline>", "");
        //str = str.Replace("</uline>", "");
        str = str.Replace("<i>", "");
        str = str.Replace("</i>", "");
        str = str.Replace("\n", "");
        str = str.Replace("\t", "");
        str = str.Replace("\r", "");
        str = str.Replace(" ", "");

        return str;
    }

    static Color32? HexToColor(string hex)
    {
        try
        {
            #region definedColor

            if (hex == "cyan")
            {
                return Color.cyan;
            }

            if (hex == "green")
            {
                return Color.green;
            }

            if (hex == "red")
            {
                return Color.red;
            }

            if (hex == "black")
            {
                return Color.black;
            }

            if (hex == "yellow")
            {
                return Color.yellow;
            }

            if (hex == "blue")
            {
                return Color.blue;
            }

            if (hex == "magenta")
            {
                return Color.magenta;
            }

            if (hex == "gray")
            {
                return Color.gray;
            }

            if (hex == "white")
            {
                return Color.white;
            }

            if (hex == "clear")
            {
                return Color.clear;
            }

            if (hex == "grey")
            {
                return Color.grey;
            }

            #endregion

            byte r = 255, g = 255, b = 255, a = 255;
            if (hex.Length >= 3)
            {
                r = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                return null;
            }

            if (hex.Length >= 5)
            {
                g = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                return new Color32(r, g, b, a);
            }

            if (hex.Length >= 7)
            {
                b = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                return new Color32(r, g, b, a);
            }

            if (hex.Length == 9)
            {
                a = byte.Parse(hex.Substring(7, 2), System.Globalization.NumberStyles.HexNumber);
            }

            return new Color32(r, g, b, a);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 点击事件检测是否点击到超链接文本
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out lp);

        foreach (var hrefInfo in m_HrefInfos)
        {
            var boxes = hrefInfo.boxes;
            for (var i = 0; i < boxes.Count; ++i)
            {
                if (boxes[i].Contains(lp))
                {
                    //if (m_OnHrefClick.GetPersistentEventCount() == 0)
                    //{
                    //    Application.OpenURL(hrefInfo.name);
                    //}
                    //else
                    //{
                    m_OnHrefClick.Invoke(hrefInfo.name);
                    //}
                    return;
                }
            }
        }

        m_OnHrefClick.Invoke("");
    }

    public virtual void OnPointerDown(PointerEventData eventData)
    {
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ClearSprites();
    }

    /// <summary>
    /// 超链接信息类
    /// </summary>
    private class HrefInfo
    {
        public int startIndex;
        public int newStartIndex;

        public int endIndex;
        public int newEndIndex;

        public string name;

        public readonly List<Rect> boxes = new List<Rect>();
    }

    private class UnderlineInfo
    {
        public Color32? color;

        public int startIndex;
        public int newStartIndex;

        public int endIndex;
        public int newEndIndex;

        public readonly List<Rect> boxes = new List<Rect>();
    }
}
}
