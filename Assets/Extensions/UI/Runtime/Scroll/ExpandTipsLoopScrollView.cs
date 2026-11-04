// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
//*****************************-》 展开Tips 循环列表 《-****************************
//author: Kim
//初始化:
//      Init(CallBack, OnClickCallBack)
//刷新列表:
//      ShowList(int = 数量)
//回调:
// 1: Func(cell, index)  //列表刷新回调
// 2: OnClickCell(cell, index) 点击Cell 回调

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExpandTipsLoopScrollView : UILoopScrollView
{
    public GameObject m_ExpandTips;
    public GameObject m_Arrow;
    public float m_TipsSpacing;

    private float m_ExpandTipsHeight;
    private bool m_IsExpand = false;

    public override void Init(Action<GameObject, int> callBack, Action<GameObject, int> onClickCallBack)
    {
        base.Init(callBack, onClickCallBack);

        //m_ExpandTips.SetActive(false);
        UILoopScrollView.SetActive(m_ExpandTips, false);

        var rectTrans = m_ExpandTips.GetComponent<RectTransform>();
        rectTrans.pivot = new Vector2(0, 1);
        rectTrans.anchorMin = new Vector2(0, 1);
        rectTrans.anchorMax = new Vector2(0, 1);
        rectTrans.anchoredPosition = new Vector2(0, 0);
        rectTrans.sizeDelta = new Vector2(m_ContentWidth, rectTrans.sizeDelta.y);

        m_ExpandTipsHeight = m_ExpandTips.GetComponent<RectTransform>().rect.height;

        if (m_Arrow != null)
        {
            var arrowRectTrams = m_Arrow.GetComponent<RectTransform>();
            arrowRectTrams.pivot = new Vector2(0.5f, 0.5f);
            arrowRectTrams.anchorMin = new Vector2(0, 0.5f);
            arrowRectTrams.anchorMax = new Vector2(0, 0.5f);
        }
    }

    public override void ShowList(int num)
    {
        base.ShowList(num);

        m_IsExpand = false;
        m_IsClearList = true;
        lastClickCellName = null;
        UILoopScrollView.SetActive(m_ExpandTips, false);
        //m_ExpandTips.SetActive(false);
    }

    private string lastClickCellName = null;
    public override void OnClickCell(GameObject cell)
    {
        int clickedIndex = int.Parse(cell.name);
        float clickedX = cell.transform.localPosition.x;
        if (lastClickCellName == null || cell.name == lastClickCellName || !m_IsExpand)
        {
            m_IsExpand = !m_IsExpand;
        }
        lastClickCellName = cell.name;

        int curRow = clickedIndex / m_Row + 1;

        //-> Tips框 显示
        //m_ExpandTips.SetActive(m_IsExpand);
        UILoopScrollView.SetActive(m_ExpandTips, m_IsExpand);
        m_ExpandTips.transform.localPosition = new Vector3(0, -((m_RowSpacing + m_CellObjectHeight) * curRow + m_TipsSpacing), 0);

        //-> Content尺寸 计算
        float contentHeight = m_IsExpand ? m_ContentHeight + m_ExpandTipsHeight + m_TipsSpacing : m_ContentHeight;
        contentHeight = contentHeight < m_PlaneHeight ? m_PlaneHeight : contentHeight;
        m_ContentRectTrans.sizeDelta = new Vector2(m_ContentWidth, contentHeight);

        m_MinIndex = -1;
        m_MaxIndex = -1;

        for (int i = 0, length = m_CellInfos.Length; i < length; i++)
        {
            CellInfo cellInfo = m_CellInfos[i];

            float pos = 0;  // Y 坐标
            float rowPos = 0; //计算每排里面的cell 坐标

            pos = m_CellObjectHeight * Mathf.FloorToInt(i / m_Row) + m_RowSpacing * (Mathf.FloorToInt(i / m_Row) + 1);
            rowPos = m_CellObjectWidth * (i % m_Row) + m_RowSpacing * (i % m_Row);

            pos += (i / m_Row >= curRow && m_IsExpand) ? m_ExpandTipsHeight + m_TipsSpacing * 2 - m_RowSpacing : 0; //往下移 Tips框高 和 距离

            cellInfo.pos = new Vector3(rowPos, -pos, 0);

            if (IsOutRange(-pos))
            {
                if (cellInfo.obj != null)
                {
                    SetPoolsObj(cellInfo.obj);
                    cellInfo.obj = null;
                }
            }
            else
            {
                //-> 记录显示范围中的 首位index 和 末尾index
                m_MinIndex = m_MinIndex == -1 ? i : m_MinIndex;// 首位 Index
                m_MaxIndex = i; // 末尾 Index

                GameObject cellObj = cellInfo.obj == null ? GetPoolsObj() : cellInfo.obj;
                cellObj.GetComponent<RectTransform>().anchoredPosition = cellInfo.pos;
                cellObj.name = i.ToString();
                if (cellInfo.obj == null) Func(m_FuncCallBackFunc, cellObj);
                cellInfo.obj = cellObj;
            }

            m_CellInfos[i] = cellInfo;
        }

        if (m_Arrow != null)
        {
            var arrowObj = m_Arrow.transform.GetComponent<RectTransform>();
            arrowObj.anchoredPosition = new Vector2(clickedX + (m_CellObjectWidth / 2), arrowObj.anchoredPosition.y);
        }
        Func(m_FuncOnClickCallBack, clickedIndex + 1, cell);
    }

    private Dictionary<GameObject, bool> isAddedListener = new Dictionary<GameObject, bool>();
    protected override GameObject GetPoolsObj()
    {
        // Base handles the single click subscription for each pooled cell.
        return base.GetPoolsObj();
    }
}
}
