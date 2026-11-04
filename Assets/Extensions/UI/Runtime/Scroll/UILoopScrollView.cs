// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
//*****************************-》 基类 循环列表 《-****************************
//author kim
//初始化:
//      Init(callBackFunc)
//刷新整个列表（首次调用和数量变化时调用）:
//      ShowList(int = 数量)
//刷新单个项:
//      UpdateCell(int = 索引)
//刷新列表数据(无数量变化时调用):
//      UpdateList()
//回调:
//Func(GameObject = Cell, int = Index)  //刷新列表

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.EventSystems;
using System;


public enum Scroll_Direction
{
    Horizontal,
    Vertical
}

public class UILoopScrollView : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public GameObject m_PointingFirstArrow;
    public GameObject m_PointingEndArrow;

    public Scroll_Direction m_Direction = Scroll_Direction.Horizontal;
    public bool m_IsShowArrow = true;
    
    public bool m_endDragItemToTop = false;

    public int m_Row = 1;

    public float m_RowSpacing = 0f; //行间距
    public float m_ColSpacing = 0f; //列间距
    public GameObject m_CellGameObject; //指定的cell
    public List<Button> btnList = new List<Button>();
    

    protected Action<GameObject, int> m_FuncCallBackFunc;
    protected Action<GameObject, int> m_FuncOnClickCallBack;
    protected Action<int, bool, GameObject> m_FuncOnButtonClickCallBack;
    protected Action<GameObject> m_FuncInCellCallBack;
    protected Action m_FuncOnListenerCallBack;
    protected Action m_FuncOnBeginDragCallBack;

    protected RectTransform rectTrans;

    protected float m_PlaneWidth;
    protected float m_PlaneHeight;

    protected float m_ContentWidth;
    protected float m_ContentHeight;

    protected float m_CellObjectWidth;
    protected float m_CellObjectHeight;

    protected GameObject m_Content;
    protected RectTransform m_ContentRectTrans;

    private bool m_isInited = false;

    //记录 物体的坐标 和 物体 
    protected struct CellInfo
    {
        public Vector3 pos;
        public GameObject obj;
    };

    protected Vector2[] m_originalPos;//原始位子
    
    protected CellInfo[] m_CellInfos;

    protected bool m_IsInited = false;

    protected ScrollRect m_ScrollRect;

    protected int m_MaxCount = -1; //列表数量

    protected int m_MinIndex = -1;
    protected int m_MaxIndex = -1;

    protected Vector2[] itemsSize;
    protected bool isdifferentSizes;
    protected bool m_IsClearList = true; //是否清空列表
    protected bool tempMaskClickCallBack = false;


    public virtual void Init(Action<GameObject, int> callBack)
    {
        Init(callBack, null);
    }

    public void Init(Action<GameObject, int> callBack, Action<GameObject, int> onClickCallBack,
        Action<GameObject> funcInCellCallBack)
    {
        if (funcInCellCallBack != null)
        {
            m_FuncInCellCallBack = funcInCellCallBack;
        }

        Init(callBack, onClickCallBack);
    }
    
    public virtual void Init(Action<GameObject, int> callBack, Action<GameObject, int> onClickCallBack)
    {
        isdifferentSizes = false;
        DisposeAll();
        m_FuncOnListenerCallBack = null;
        m_FuncOnBeginDragCallBack = null;
        m_FuncCallBackFunc = callBack;

        if (onClickCallBack != null)
        {
            m_FuncOnClickCallBack = onClickCallBack;
        }

        if (m_isInited)
        {
            return;
        }

        m_Content = this.GetComponent<ScrollRect>().content.gameObject;

        if (m_CellGameObject == null)
        {
            m_CellGameObject = m_Content.transform.GetChild(0).gameObject;
        }

        if (m_FuncInCellCallBack != null)
        {
            m_FuncInCellCallBack(m_CellGameObject);
        }

        Button cellButtonComponent = m_CellGameObject.GetComponent<Button>();
        if (cellButtonComponent != null)
        {
            cellButtonComponent.onClick.AddListener(delegate() { OnClickCell(m_CellGameObject); });
        }

        /* Cell 处理 */
        //m_CellGameObject.transform.SetParent(m_Content.transform.parent, false);
        SetPoolsObj(m_CellGameObject);

        RectTransform cellRectTrans = m_CellGameObject.GetComponent<RectTransform>();
        cellRectTrans.pivot = new Vector2(0f, 1f);
        CheckAnchor(cellRectTrans);
        cellRectTrans.anchoredPosition = Vector2.zero;

        //记录 Cell 信息
        m_CellObjectHeight = cellRectTrans.rect.height;
        m_CellObjectWidth = cellRectTrans.rect.width;

        //记录 Plane 信息
        rectTrans = GetComponent<RectTransform>();
        Rect planeRect = rectTrans.rect;
        m_PlaneHeight = planeRect.height;
        m_PlaneWidth = planeRect.width;

        //记录 Content 信息
        m_ContentRectTrans = m_Content.GetComponent<RectTransform>();
        Rect contentRect = m_ContentRectTrans.rect;
        m_ContentHeight = contentRect.height;
        m_ContentWidth = contentRect.width;

        m_ContentRectTrans.pivot = new Vector2(0f, 1f);
        //m_ContentRectTrans.sizeDelta = new Vector2 (planeRect.width, planeRect.height);
        //m_ContentRectTrans.anchoredPosition = Vector2.zero;
        CheckAnchor(m_ContentRectTrans);

        m_ScrollRect = this.GetComponent<ScrollRect>();

        m_ScrollRect.onValueChanged.RemoveListener(HandleValueChanged);
        m_ScrollRect.onValueChanged.AddListener(HandleValueChanged);

        if (m_PointingFirstArrow != null || m_PointingEndArrow != null)
        {
            OnDragListener(Vector2.zero);
        }

        //InitScrollBarGameObject(); // 废弃

        m_isInited = true;
    }

    public void SetListenerCallBack(Action listenerCallBack)
    {
        m_FuncOnListenerCallBack = listenerCallBack;
    }
    public void SetOnBeginDragCallBack(Action onBeginDragCallBack)
    {
        m_FuncOnBeginDragCallBack = onBeginDragCallBack;
    }

    //检查 Anchor 是否正确
    private void CheckAnchor(RectTransform rectTrans)
    {
        if (m_Direction == Scroll_Direction.Vertical)
        {
            if (!((rectTrans.anchorMin == new Vector2(0, 1) && rectTrans.anchorMax == new Vector2(0, 1)) ||
                  (rectTrans.anchorMin == new Vector2(0, 1) && rectTrans.anchorMax == new Vector2(1, 1))))
            {
                rectTrans.anchorMin = new Vector2(0, 1);
                rectTrans.anchorMax = new Vector2(1, 1);
            }
        }
        else
        {
            if (!((rectTrans.anchorMin == new Vector2(0, 1) && rectTrans.anchorMax == new Vector2(0, 1)) ||
                  (rectTrans.anchorMin == new Vector2(0, 0) && rectTrans.anchorMax == new Vector2(0, 1))))
            {
                rectTrans.anchorMin = new Vector2(0, 0);
                rectTrans.anchorMax = new Vector2(0, 1);
            }
        }
    }

    //实时刷新列表时用
    public virtual void UpdateList()
    {
        if (m_CellInfos == null) return;
        for (int i = 0, length = m_CellInfos.Length; i < length; i++)
        {
            CellInfo cellInfo = m_CellInfos[i];
            if (cellInfo.obj != null)
            {
                float rangePos = m_Direction == Scroll_Direction.Vertical ? cellInfo.pos.y : cellInfo.pos.x;
                if (!IsOutRange(rangePos))
                {
                    Func(m_FuncCallBackFunc, cellInfo.obj, true);
                }
            }
        }
    }

    //刷新某一项
    public void UpdateCell(int index)
    {
        if (m_CellInfos == null || index < 1 || index > m_CellInfos.Length) return;
        CellInfo cellInfo = m_CellInfos[index - 1];
        if (cellInfo.obj != null)
        {
            float rangePos = m_Direction == Scroll_Direction.Vertical ? cellInfo.pos.y : cellInfo.pos.x;
            if (!IsOutRange(rangePos))
            {
                Func(m_FuncCallBackFunc, cellInfo.obj);
            }
        }
    }

    public virtual void ShowList(string numStr)
    {
    }

    public virtual void ShowList(int num, Vector2[] size)
    {
        if (size == null || size.Length != num) throw new ArgumentException("One size is required for each item.", nameof(size));
        if (m_Row != 1) throw new InvalidOperationException("Variable size lists require m_Row = 1.");
        itemsSize = size;
        isdifferentSizes = true;
        ShowList(num);
    }

    public void UpdateContentSize(int num)
    {
        float allSize = 0;
        if (isdifferentSizes)
        {
            for (int i = 0; i < itemsSize.Length; i++)
            {
                allSize += (m_Direction == Scroll_Direction.Vertical)
                    ? itemsSize[i].y + m_ColSpacing
                    : itemsSize[i].x + m_RowSpacing;
            }
        }

        //-> 计算 Content 尺寸
        if (m_Direction == Scroll_Direction.Vertical)
        {
            float contentSize = m_RowSpacing * Mathf.Max(0, Mathf.CeilToInt((float)num / m_Row) - 1) +
                                m_CellObjectHeight * Mathf.CeilToInt((float) num / m_Row);
            m_ContentHeight = contentSize;
            m_ContentWidth = m_ContentRectTrans.sizeDelta.x;
            contentSize = contentSize < rectTrans.rect.height ? rectTrans.rect.height : contentSize;
            m_ContentRectTrans.sizeDelta = new Vector2(m_ContentWidth, (isdifferentSizes) ? allSize : contentSize);
        }
        else
        {
            float contentSize = m_ColSpacing * Mathf.Max(0, Mathf.CeilToInt((float)num / m_Row) - 1) +
                                m_CellObjectWidth * Mathf.CeilToInt((float) num / m_Row);
            m_ContentWidth = contentSize;
            m_ContentHeight = m_ContentRectTrans.sizeDelta.y;
            contentSize = contentSize < rectTrans.rect.width ? rectTrans.rect.width : contentSize;
            m_ContentRectTrans.sizeDelta = new Vector2((isdifferentSizes) ? allSize : contentSize, m_ContentHeight);
        }
    }

    public virtual void ShowList(int num)
    {
        if (!m_isInited) throw new InvalidOperationException("Call Init before ShowList.");
        if (num < 0) throw new ArgumentOutOfRangeException(nameof(num));
        m_Row = Mathf.Max(1, m_Row);
        tempMaskClickCallBack = false;
        m_originalPos = new Vector2[num];
        m_MinIndex = -1;
        m_MaxIndex = -1;

        // m_ScrollRect.verticalNormalizedPosition = 1;
        UpdateContentSize(num);

        
        if (m_Direction == Scroll_Direction.Vertical)
        {
            if (num != m_MaxCount)
            {
             m_ContentRectTrans.anchoredPosition = new Vector2(m_ContentRectTrans.anchoredPosition.x, 0);
            } 
        }
        else
        {
            if (num != m_MaxCount)
            {
                m_ContentRectTrans.anchoredPosition = new Vector2(0, m_ContentRectTrans.anchoredPosition.y);
            }
        }

        //-> 计算 开始索引
        int lastEndIndex = 0;
        //-> 过多的物体 扔到对象池 ( 首次调 ShowList函数时 则无效 )
        if (m_IsInited)
        {
            lastEndIndex = num - m_MaxCount > 0 ? m_MaxCount : num;
            lastEndIndex = m_IsClearList ? 0 : lastEndIndex;
            int count = m_IsClearList ? m_CellInfos.Length : m_MaxCount;
            for (int i = lastEndIndex; i < count; i++)
            {
                if (m_CellInfos[i].obj != null)
                {
                    SetPoolsObj(m_CellInfos[i].obj);
                    m_CellInfos[i].obj = null;
                }
            }
        }

        //-> 以下四行代码 在for循环所用
        CellInfo[] tempCellInfos = m_CellInfos;
        m_CellInfos = new CellInfo[num];
        float curSize = 0;
        //-> 1: 计算 每个Cell坐标并存储 2: 显示范围内的 Cell
        for (int i = 0; i < num; i++)
        {
            // * -> 存储 已有的数据 ( 首次调 ShowList函数时 则无效 )
            if (m_MaxCount != -1 && i < lastEndIndex)
            {
                CellInfo tempCellInfo = tempCellInfos[i];
                //-> 计算是否超出范围
                float rPos = m_Direction == Scroll_Direction.Vertical ? tempCellInfo.pos.y : tempCellInfo.pos.x;
                if (!IsOutRange(rPos))
                {
                    //-> 记录显示范围中的 首位index 和 末尾index
                    m_MinIndex = m_MinIndex == -1 ? i : m_MinIndex; //首位index
                    m_MaxIndex = i; // 末尾index

                    if (tempCellInfo.obj == null)
                    {
                        tempCellInfo.obj = GetPoolsObj();
                    }

                    tempCellInfo.obj.transform.GetComponent<RectTransform>().anchoredPosition = tempCellInfo.pos;
                    if (isdifferentSizes)
                        tempCellInfo.obj.transform.GetComponent<RectTransform>().sizeDelta = itemsSize[i];
                    tempCellInfo.obj.name = i.ToString();
                    tempCellInfo.obj.SetActive(true);

                    Func(m_FuncCallBackFunc, tempCellInfo.obj);
                }
                else
                {
                    SetPoolsObj(tempCellInfo.obj);
                    tempCellInfo.obj = null;
                }

                m_CellInfos[i] = tempCellInfo;

                if (!isdifferentSizes)
                    continue;
            }

            CellInfo cellInfo = new CellInfo();

            float pos = 0; //坐标( isVertical ? 记录Y : 记录X )
            float rowPos = 0; //计算每排里面的cell 坐标

            // * -> 计算每个Cell坐标
            if (m_Direction == Scroll_Direction.Vertical)
            {
                pos = (isdifferentSizes)
                    ? curSize + m_RowSpacing
                    : m_CellObjectHeight * Mathf.FloorToInt(i / m_Row) + m_RowSpacing * Mathf.FloorToInt(i / m_Row);
                rowPos = m_CellObjectWidth * (i % m_Row) + m_RowSpacing * (i % m_Row);
                cellInfo.pos = new Vector3(rowPos, -pos, 0);
                if (isdifferentSizes)
                {
                    curSize += itemsSize[i].y + m_RowSpacing;
                }
            }
            else
            {
                pos = (isdifferentSizes)
                    ? curSize + m_ColSpacing
                    : m_CellObjectWidth * Mathf.FloorToInt(i / m_Row) + m_ColSpacing * Mathf.FloorToInt(i / m_Row);
                rowPos = m_CellObjectHeight * (i % m_Row) + m_ColSpacing * (i % m_Row);
                cellInfo.pos = new Vector3(pos, -rowPos, 0);
                if (isdifferentSizes)
                {
                    curSize += itemsSize[i].x + m_ColSpacing;
                }
            }

            //-> 计算是否超出范围
            float cellPos = m_Direction == Scroll_Direction.Vertical ? cellInfo.pos.y : cellInfo.pos.x;
            float size = 0;
            if (isdifferentSizes)
            {
                size = m_Direction == Scroll_Direction.Vertical ? itemsSize[i].y : itemsSize[i].x;
            }

            if ((isdifferentSizes) ? IsOutRange(cellPos, size) : IsOutRange(cellPos))
            {
                cellInfo.obj = null;
                m_CellInfos[i] = cellInfo;
                continue;
            }

            if (!(m_MaxCount != -1 && i < lastEndIndex))
            {
                //-> 记录显示范围中的 首位index 和 末尾index
                m_MinIndex = m_MinIndex == -1 ? i : m_MinIndex; //首位index
                m_MaxIndex = i; // 末尾index

                //-> 取或创建 Cell
                GameObject cell = GetPoolsObj();
                cell.transform.GetComponent<RectTransform>().anchoredPosition = cellInfo.pos;
                cell.gameObject.name = i.ToString();
                
                if (isdifferentSizes)
                {
                    cell.transform.GetComponent<RectTransform>().sizeDelta = itemsSize[i];
                }
                
                //-> 存数据
                cellInfo.obj = cell;
                m_CellInfos[i] = cellInfo;

                //-> 回调  函数
                Func(m_FuncCallBackFunc, cell);
            }
        }

        for (int i = 0; i < num; i++)
        {
            m_originalPos[i] = m_CellInfos[i].pos;
        }
        
        m_MaxCount = num;
        m_IsInited = true;
        OnDragListener(Vector2.zero);
    }

    //前往某个Item
    public void ToItem(int index)
    {
        // This legacy positioning API uses zero-based indices; cell callbacks remain one-based.
        if (m_CellInfos == null || index < 0 || index >= m_CellInfos.Length) return;
        if (m_Direction == Scroll_Direction.Vertical)
        {
            m_ContentRectTrans.anchoredPosition = new Vector2(m_ContentRectTrans.anchoredPosition.x,
                Mathf.Abs(m_CellInfos[index].pos.y));
        }
        else
        {
            m_ContentRectTrans.anchoredPosition = new Vector2(Mathf.Abs(m_CellInfos[index].pos.x),
                m_ContentRectTrans.anchoredPosition.y);
        }

        UpdateCheck();
    }

    // 更新滚动区域的大小
    public void UpdateSize()
    {
        Rect rect = GetComponent<RectTransform>().rect;
        m_PlaneHeight = rect.height;
        m_PlaneWidth = rect.width;
    }

    //滑动事件
    private void HandleValueChanged(Vector2 value)
    {
        ScrollRectListener(value);
        if (m_PointingFirstArrow != null || m_PointingEndArrow != null) OnDragListener(value);
    }

    protected virtual void ScrollRectListener(Vector2 value)
    {
        if (m_FuncOnListenerCallBack != null)
        {
            m_FuncOnListenerCallBack();
        }
        UpdateCheck();
    }

    private void UpdateCheck()
    {
        if (m_CellInfos == null)
        {
            return;
        }

        // 检查超出范围
        for (int i = 0, length = m_CellInfos.Length; i < length; i++)
        {
            CellInfo cellInfo = m_CellInfos[i];
            GameObject obj = cellInfo.obj;
            Vector3 pos = cellInfo.pos;

            float rangePos = m_Direction == Scroll_Direction.Vertical ? pos.y : pos.x;
            float size = 0;
            if (isdifferentSizes)
            {
                size = m_Direction == Scroll_Direction.Vertical ? itemsSize[i].y : itemsSize[i].x;
            }

            //判断是否超出显示范围
            if ((isdifferentSizes) ? IsOutRange(rangePos, size) : IsOutRange(rangePos))
            {
                //把超出范围的cell 扔进 poolsObj里
                if (obj != null)
                {
                    SetPoolsObj(obj);
                    m_CellInfos[i].obj = null;
                }
            }
            else
            {
                if (obj == null)
                {
                    //优先从 poolsObj中 取出 （poolsObj为空则返回 实例化的cell）
                    GameObject cell = GetPoolsObj();
                    cell.transform.localPosition = pos;
                    cell.gameObject.name = i.ToString();
                    m_CellInfos[i].obj = cell;

                    if (isdifferentSizes)
                    {
                        m_CellInfos[i].obj.transform.GetComponent<RectTransform>().sizeDelta = itemsSize[i];
                    }
                    Func(m_FuncCallBackFunc, cell);
                }
            }
        }
    }

    //判断是否超出显示范围
    protected bool IsOutRange(float pos)
    {
        Vector3 listP = m_ContentRectTrans.anchoredPosition;
        if (m_Direction == Scroll_Direction.Vertical)
        {
            if (pos + listP.y >= m_CellObjectHeight || pos + listP.y <= -rectTrans.rect.height)
            {
                return true;
            }
        }
        else
        {
            if (pos + listP.x <= -m_CellObjectWidth || pos + listP.x >= rectTrans.rect.width)
            {
                return true;
            }
        }

        return false;
    }

    //判断是否超出显示范围
    protected bool IsOutRange(float pos, float size)
    {
        Vector3 listP = m_ContentRectTrans.anchoredPosition;
        if (m_Direction == Scroll_Direction.Vertical)
        {
            if (pos + listP.y >= size || pos + listP.y <= -rectTrans.rect.height)
            {
                return true;
            }
        }
        else
        {
            if (pos + listP.x <= -size || pos + listP.x >= rectTrans.rect.width)
            {
                return true;
            }
        }

        return false;
    }

    //对象池 机制  (存入， 取出) cell
    protected Stack<GameObject> poolsObj = new Stack<GameObject>();

    private Coroutine positionRoutine;

    private void AnimateTo(Vector2 target, float duration)
    {
        if (positionRoutine != null) StopCoroutine(positionRoutine);
        if (!isActiveAndEnabled || duration <= 0)
        {
            m_ContentRectTrans.anchoredPosition = target;
            return;
        }
        positionRoutine = StartCoroutine(AnimatePosition(target, duration));
    }

    private IEnumerator AnimatePosition(Vector2 target, float duration)
    {
        Vector2 start = m_ContentRectTrans.anchoredPosition;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            m_ContentRectTrans.anchoredPosition = Vector2.Lerp(start, target, t * (2 - t));
            yield return null;
        }
        m_ContentRectTrans.anchoredPosition = target;
        positionRoutine = null;
    }

    protected virtual void OnDisable()
    {
        if (positionRoutine != null) StopCoroutine(positionRoutine);
        positionRoutine = null;
    }

    //取出 cell
    protected virtual GameObject GetPoolsObj()
    {
        GameObject cell = null;
        if (poolsObj.Count > 0)
        {
            cell = poolsObj.Pop();
        }

        if (cell == null)
        {
            cell = Instantiate(m_CellGameObject) as GameObject;
            Button cellButtonComponent = cell.GetComponent<Button>();
            if (cellButtonComponent != null) cellButtonComponent.onClick.AddListener(delegate() { OnClickCell(cell); });
            if (m_FuncInCellCallBack != null)
            {
                m_FuncInCellCallBack(cell);
            }
        }

        cell.transform.SetParent(m_Content.transform, false);
        cell.transform.localScale = Vector3.one;
        UILoopScrollView.SetActive(cell, true);

        return cell;
    }

    //存入 cell
    protected virtual void SetPoolsObj(GameObject cell)
    {
        if (cell != null)
        {
            poolsObj.Push(cell);
            UILoopScrollView.SetActive(cell, false);
        }
    }

    //回调
    protected void Func(Action<GameObject, int> func, GameObject selectObject, bool isUpdate = false)
    {
        int num = int.Parse(selectObject.name) + 1;
        if (func != null)
        {
            func(selectObject, num);
        }
    }
    protected void Func(Action<GameObject, int> func,int num, GameObject selectObject, bool isUpdate = false)
    {
        if (func != null)
        {
            func(selectObject, num);
        }
    }

    public void DisposeAll()
    {
        if (m_FuncCallBackFunc != null)
        {
            m_FuncCallBackFunc = null;
        }

        if (m_FuncOnClickCallBack != null)
        {
            m_FuncOnClickCallBack = null;
        }
    }

    protected void OnDestroy()
    {
        DisposeAll();
    }

    public virtual void OnClickCell(GameObject cell)
    {
        if (!tempMaskClickCallBack)
        {
            Func(m_FuncOnClickCallBack, cell);
        }
    }

    //-> ExpandCircularScrollView 函数
    public virtual void OnClickExpand(int index) { }

    //-> FlipCircularScrollView 函数
    public virtual void SetToPageIndex(int index) { }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (m_FuncOnBeginDragCallBack != null)
        {
            m_FuncOnBeginDragCallBack();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (!m_endDragItemToTop || m_CellInfos == null || m_CellInfos.Length == 0)
        {
            return;
        }

        if (m_Direction == Scroll_Direction.Vertical)
        {
            if (m_ScrollRect.verticalNormalizedPosition < 0 || m_ScrollRect.verticalNormalizedPosition > 1)
            {
                return;
            }
        }
        else
        {
            if (m_ScrollRect.horizontalNormalizedPosition < 0 || m_ScrollRect.horizontalNormalizedPosition > 1)
            {
                return;
            }
        }
        
        //获取最近的下标
        float curDIST;
        float curSize;
        float minDIST = -1;
        int minIndex = -1;
        float offset = m_Direction == Scroll_Direction.Vertical
            ? m_ContentRectTrans.anchoredPosition.y : -m_ContentRectTrans.anchoredPosition.x;
        
        for (int i = 0; i < m_CellInfos.Length; i++)
        {
            if (m_Direction == Scroll_Direction.Vertical)
            {
                curSize = isdifferentSizes ? itemsSize[i].y / 2 : m_CellObjectHeight / 2;
                curDIST = Mathf.Abs(offset - Mathf.Abs(m_CellInfos[i].pos.y) - curSize);
            }
            else
            {
                curSize = isdifferentSizes ? itemsSize[i].x / 2 : m_CellObjectWidth / 2;
                curDIST = Mathf.Abs(offset - Mathf.Abs(m_CellInfos[i].pos.x) - curSize);
            }

            if (minDIST == -1 || curDIST < minDIST)
            {
                minDIST = curDIST;
                minIndex = i;
            }
        }

        //以中心点作为判断
        if (minIndex >= 0 && minIndex < m_CellInfos.Length)
        {
            float centerPos;
            if (m_Direction == Scroll_Direction.Vertical)
            {
                centerPos = Mathf.Abs( m_CellInfos[minIndex].pos.y);
                centerPos += isdifferentSizes ? itemsSize[minIndex].y / 2 : m_CellObjectHeight / 2;
                if (offset > centerPos)
                {
                    minIndex += 1;
                }
            }
            else
            {
                centerPos = Mathf.Abs( m_CellInfos[minIndex].pos.x);
                centerPos += isdifferentSizes ? itemsSize[minIndex].x / 2: m_CellObjectWidth / 2;
                if (offset > centerPos)
                {
                    minIndex += 1;
                }
            }
        }

        if (minIndex < 0 || minIndex >= m_CellInfos.Length)
        {
            //说明触底了
            return;
        }

        int minCanIndex = 0;
        float allSize = 0;
        float rectSize = m_Direction == Scroll_Direction.Vertical ? rectTrans.rect.height : rectTrans.rect.width;
        while (allSize <= rectSize && minCanIndex < m_CellInfos.Length)
        {
            if (m_Direction == Scroll_Direction.Vertical)
            {
                curSize = (isdifferentSizes ? itemsSize[minCanIndex].y : m_CellObjectHeight) + m_RowSpacing;
            }
            else
            {
                curSize = (isdifferentSizes ? itemsSize[minCanIndex].x : m_CellObjectWidth) + m_ColSpacing;
            }

            allSize += curSize;
            minCanIndex += 1;
        }

        if (allSize <= rectSize)
        {
            //达不到自动吸附的大小
            return;
        }

        Vector2 target;
        float time;
        m_ScrollRect.StopMovement();
        if (m_Direction == Scroll_Direction.Vertical)
        {
            curSize = isdifferentSizes ? itemsSize[minIndex].y / 2 : m_CellObjectHeight / 2;
            target = new Vector2(m_ContentRectTrans.anchoredPosition.x,
                Mathf.Abs(m_CellInfos[minIndex].pos.y));
            time = Mathf.Abs(m_ContentRectTrans.anchoredPosition.y - target.y) / Mathf.Max(1, curSize / 2) * 0.5f;
            AnimateTo(target, time);
        }
        else
        {
            curSize = isdifferentSizes ? itemsSize[minIndex].x / 2 : m_CellObjectWidth / 2;
            target = new Vector2(-Mathf.Abs(m_CellInfos[minIndex].pos.x),
                m_ContentRectTrans.anchoredPosition.y);
            time = Mathf.Abs(m_ContentRectTrans.anchoredPosition.x - target.x) / Mathf.Max(1, curSize / 2) * 0.5f;
            AnimateTo(target, time);
        }
        
    }

    protected void OnDragListener(Vector2 value)
    {
        float normalizedPos = m_Direction == Scroll_Direction.Vertical
            ? m_ScrollRect.verticalNormalizedPosition
            : m_ScrollRect.horizontalNormalizedPosition;

        if (m_Direction == Scroll_Direction.Vertical)
        {
            if (m_ContentHeight - rectTrans.rect.height < 10)
            {
                UILoopScrollView.SetActive(m_PointingFirstArrow, false);
                UILoopScrollView.SetActive(m_PointingEndArrow, false);
                return;
            }
        }
        else
        {
            if (m_ContentWidth - rectTrans.rect.width < 10)
            {
                UILoopScrollView.SetActive(m_PointingFirstArrow, false);
                UILoopScrollView.SetActive(m_PointingEndArrow, false);
                return;
            }
        }

        if (normalizedPos >= 0.9)
        {
            UILoopScrollView.SetActive(m_PointingFirstArrow, false);
            UILoopScrollView.SetActive(m_PointingEndArrow, true);
        }
        else if (normalizedPos <= 0.1)
        {
            UILoopScrollView.SetActive(m_PointingFirstArrow, true);
            UILoopScrollView.SetActive(m_PointingEndArrow, false);
        }
        else
        {
            UILoopScrollView.SetActive(m_PointingFirstArrow, true);
            UILoopScrollView.SetActive(m_PointingEndArrow, true);
        }
    }

    public static void SetActive(GameObject obj, bool isActive)
    {
        if (obj != null)
        {
            obj.SetActive(isActive);
        }
    }
}
}
