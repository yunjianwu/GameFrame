// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Events;

//[RequireComponent(typeof(GridLayoutGroup))]
//[RequireComponent(typeof(ContentSizeFitter))]
public class CInfinityLoopComponent : MonoBehaviour 
{
    public class DragMoveEvent : UnityEvent<int, int> { }
    public DragMoveEvent onDragMove = new DragMoveEvent();

    public class AutoMoveToEvent : UnityEvent<List<uint>> { }
    public AutoMoveToEvent onAutoMoveTo = new AutoMoveToEvent();

    int m_nMinAmount = 0;//滚动组件数量
    int m_nAmount = 100;//总共需要显示的组件数
    int m_nRealIndex = -1;
    bool m_bHasInit = false;       //是否已初始化

    RectTransform mRectTransform;
    GridLayoutGroup mGridLayoutGroup;
    ContentSizeFitter mContentSizeFitter;
    ScrollRect mScrollRect;
    Vector2 mScrollContentSize; //滚动区域容器的初始大小
    Vector2 mScrollContentPos; //滚动区域容器的初始位置
    Vector2 mStartPosition;   //开始拖动时滚动区域容器的位置
    List<RectTransform> mChildren = new List<RectTransform>();
    Dictionary<Transform, Vector2> mChildsAnchoredPosition = new Dictionary<Transform, Vector2>();//需要滚动的组件的位置
    Dictionary<Transform, int> mChildsSiblingIndex = new Dictionary<Transform, int>();
    List<uint> m_arrRealIndex = new List<uint>();


	// Use this for initialization
	void Start ()
    {
        InitChildren();
	}

    void InitChildren()
    {
        if (m_bHasInit)
        {
            return;
        }
        //注册ScrollRect滚动回调;
        mScrollRect = transform.parent.parent.GetComponent<ScrollRect>();
        mScrollRect.onValueChanged.AddListener((data) => { ScrollCallback(data); });
        m_nMinAmount = transform.childCount;
        RectTransform scrollRectTransform = mScrollRect.GetComponent<RectTransform>();
        //获取Grid的宽度;
        mRectTransform = GetComponent<RectTransform>();

        mGridLayoutGroup = GetComponent<GridLayoutGroup>();
        mGridLayoutGroup.enabled = false;
        mContentSizeFitter = GetComponent<ContentSizeFitter>();
        mContentSizeFitter.enabled = false;

        mScrollContentPos = mRectTransform.anchoredPosition;
        mRectTransform.sizeDelta = new Vector2(mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x + 0.1f, scrollRectTransform.rect.height + mGridLayoutGroup.spacing.y + 0.1f);
        mScrollContentSize = mRectTransform.sizeDelta;

            
        //获取所有child anchoredPosition 以及 SiblingIndex;
        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);
            RectTransform childRectTrans = child.GetComponent<RectTransform>();
            mChildsAnchoredPosition.Add(child, childRectTrans.anchoredPosition);

            mChildsSiblingIndex.Add(child, child.GetSiblingIndex());
        }


        //获取所有child;
        m_arrRealIndex = new List<uint>();
        for (uint index = 0; index < transform.childCount; index++)
        {
            m_arrRealIndex.Add(index);
            Transform trans = transform.GetChild((int)index);
            trans.gameObject.SetActive(true);
            mChildren.Add(transform.GetChild((int)index).GetComponent<RectTransform>());
        }
        
        mStartPosition = mRectTransform.anchoredPosition;
        m_nRealIndex = mChildren.Count - 1;
        m_bHasInit = true;

        //如果需要显示的个数小于设定的个数;
        for (int index = 0; index < m_nMinAmount; index++)
        {
            mChildren[index].gameObject.SetActive(index < m_nAmount);
        }

        if (mGridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            //如果小了一行，则需要把GridLayout的高度减去一行的高度;
            int row = (m_nMinAmount - m_nAmount) / mGridLayoutGroup.constraintCount;
            if (row > 0)
            {
                mRectTransform.sizeDelta -= new Vector2(0, (mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y) * row);
            }
        }
        else
        {
            //如果小了一列，则需要把GridLayout的宽度减去一列的宽度;
            int column = (m_nMinAmount - m_nAmount) / mGridLayoutGroup.constraintCount;
            if (column > 0)
            {
                mRectTransform.sizeDelta -= new Vector2((mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x) * column, 0);
            }
        }
    }
	
	// Update is called once per frame
	void Update () 
    {
	
	}


    void ScrollCallback(Vector2 data)
    {
        UpdateChildren();
    }

    void UpdateChildren()
    {
        if (transform.childCount < m_nMinAmount)
        {
            return;
        }

        Vector2 currentPos = mRectTransform.anchoredPosition;

        if (mGridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            float offsetY = currentPos.y - mStartPosition.y;

            if (offsetY > 0)
            {
                //向上拉，向下扩展;
                {
                    if (m_nRealIndex >= m_nAmount - 1)
                    {
                        mStartPosition = currentPos;
                        return;
                    }

                    float scrollRectUp = mScrollRect.transform.TransformPoint(Vector3.zero).y;
                    RectTransform mostUpTrans = GetMostUpTransForm();
                    RectTransform mostDownTrans = GetMostDownTransForm();
                    if (mostUpTrans == null || mostDownTrans == null)
                    {
                        return;
                    }
                    Vector3 childBottomLeft = new Vector3(mostUpTrans.anchoredPosition.x, mostUpTrans.anchoredPosition.y - mGridLayoutGroup.cellSize.y, 0f);
                    float childBottom = transform.TransformPoint(childBottomLeft).y;

                    if (float.Parse(childBottom.ToString("0.00")) >= float.Parse(scrollRectUp.ToString("0.00")))
                    {
                        //Debug.Log("childBottom >= scrollRectUp");

                        //移动到底部;
                        mostUpTrans.anchoredPosition = new Vector2(mostUpTrans.anchoredPosition.x, mostDownTrans.anchoredPosition.y - mGridLayoutGroup.cellSize.y - mGridLayoutGroup.spacing.y);

                        m_nRealIndex++;

                        if (m_nRealIndex > m_nAmount - 1)
                        {
                            mostUpTrans.gameObject.SetActive(false);
                        }
                        else
                        {
                            if (mChildsSiblingIndex.ContainsKey(mostUpTrans))
                            {
                                int childIndex = mChildsSiblingIndex[mostUpTrans];
                                m_arrRealIndex[childIndex] = (uint)m_nRealIndex;
                                UpdateChildrenCallback(childIndex, m_nRealIndex);
                            }
                        }

                        //GridLayoutGroup 底部加长;
                       // mRectTransform.sizeDelta += new Vector2(0, mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y);
                    }
                }
            }
            else
            {
                //Debug.Log("Drag Down");
                //向下拉，下面收缩;
                if (m_nRealIndex + 1 <= mChildren.Count)
                {
                    mStartPosition = currentPos;
                    return;
                }
                RectTransform mostUpTrans = GetMostUpTransForm();
                RectTransform mostDownTrans = GetMostDownTransForm();
                if (mostUpTrans == null || mostDownTrans == null)
                {
                    return;
                }
                RectTransform scrollRectTransform = mScrollRect.GetComponent<RectTransform>();
                Vector3 scrollRectAnchorBottom = new Vector3(0, -scrollRectTransform.rect.height - mGridLayoutGroup.spacing.y, 0f);
                float scrollRectBottom = mScrollRect.transform.TransformPoint(scrollRectAnchorBottom).y;

                Vector3 childUpLeft = new Vector3(mostDownTrans.anchoredPosition.x, mostDownTrans.anchoredPosition.y, 0f);

                float childUp = transform.TransformPoint(childUpLeft).y;

                if (float.Parse(childUp.ToString("0.00")) < float.Parse(scrollRectBottom.ToString("0.00")))
                {
                    //Debug.Log("childUp < scrollRectBottom");

                    //把底部的一行 移动到顶部
                    mostDownTrans.anchoredPosition = new Vector2(mostDownTrans.anchoredPosition.x, mostUpTrans.anchoredPosition.y + mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y);

                    mostDownTrans.gameObject.SetActive(true);

                    if (mChildsSiblingIndex.ContainsKey(mostDownTrans))
                    {
                        int childIndex = mChildsSiblingIndex[mostDownTrans];
                        m_arrRealIndex[childIndex] = (uint)(m_nRealIndex - mChildren.Count);
                        UpdateChildrenCallback(childIndex, m_nRealIndex - mChildren.Count);
                    }

                    m_nRealIndex -= mGridLayoutGroup.constraintCount;

                    //GridLayoutGroup 底部缩短;
                    //mRectTransform.sizeDelta -= new Vector2(0, mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y);
                }
            }
        }
        else
        {
            float offsetX = currentPos.x - mStartPosition.x;
            //Debug.Log("offsetX" + offsetX);
            if (offsetX < 0)
            {
                //向左拉，向右扩展;
                {
                    if (m_nRealIndex >= m_nAmount - 1)
                    {
                        mStartPosition = currentPos;
                        return;
                    }

                    float scrollRectLeft = mScrollRect.transform.TransformPoint(Vector3.zero).x;
                    RectTransform mostLiftTrans = GetMostLeftTransForm();
                    RectTransform mostRightTrans = GetMostRightTransForm();
                    if (mostLiftTrans == null || mostRightTrans == null)
                    {
                        return;
                    }
                    Vector3 childBottomRight = new Vector3(mostLiftTrans.anchoredPosition.x + mGridLayoutGroup.cellSize.x, mostLiftTrans.anchoredPosition.y, 0f);
                    float childRight = transform.TransformPoint(childBottomRight).x;

                    //Debug.Log("childRight=" + childRight + "scrollRectLeft=" + scrollRectLeft);
                    //Debug.Log("childRight=" + float.Parse(childRight.ToString("0.00")) + "scrollRectLeft=" + float.Parse(scrollRectLeft.ToString("0.00")));
                    if (float.Parse(childRight.ToString("0.00")) <= float.Parse(scrollRectLeft.ToString("0.00")))
                    {
                        //Debug.Log("childRight <= scrollRectLeft");

                        //移动到右边;
                        mostLiftTrans.anchoredPosition = new Vector2(mostRightTrans.anchoredPosition.x + mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x, mostLiftTrans.anchoredPosition.y);
                        m_nRealIndex ++;
                        //Debug.Log("m_nRealIndex" + m_nRealIndex);

                        if (m_nRealIndex > m_nAmount - 1)
                        {
                            mostLiftTrans.gameObject.SetActive(false);
                        }
                        else
                        {
                            if (mChildsSiblingIndex.ContainsKey(mostLiftTrans))
                            {
                                int childIndex = mChildsSiblingIndex[mostLiftTrans];
                                m_arrRealIndex[childIndex] = (uint)m_nRealIndex;
                                UpdateChildrenCallback(childIndex, m_nRealIndex);
                            }
                        }

                        //GridLayoutGroup 右侧加长;
                        //Debug.Log("111rectTransform.sizeDelta.x" + mRectTransform.sizeDelta.x + "mRectTransform.anchoredPosition.x" + mRectTransform.anchoredPosition.x + "mRectTransform.anchoredPosition.y" + mRectTransform.anchoredPosition.y);
                        //mRectTransform.sizeDelta += new Vector2(mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x,0);
                        //Debug.Log("222rectTransform.sizeDelta.x" + mRectTransform.sizeDelta.x + "mRectTransform.anchoredPosition.x" + mRectTransform.anchoredPosition.x + "mRectTransform.anchoredPosition.y" + mRectTransform.anchoredPosition.y);
                    }
                }
            }
            else if (offsetX > 0)
            {
                //Debug.Log("Drag Down");
                //向右拉，右边收缩;                
                if (m_nRealIndex + 1 <= mChildren.Count)
                {
                    mStartPosition = currentPos;
                    return;
                }
                RectTransform mostLiftTrans = GetMostLeftTransForm();
                RectTransform mostRightTrans = GetMostRightTransForm();
                if (mostLiftTrans == null || mostRightTrans == null)
                {
                    return;
                }

                RectTransform scrollRectTransform = mScrollRect.GetComponent<RectTransform>();
                Vector3 scrollRectAnchorRight = new Vector3(scrollRectTransform.rect.width + mGridLayoutGroup.spacing.x, 0, 0f);
                float scrollRectRight = mScrollRect.transform.TransformPoint(scrollRectAnchorRight).x;

                //scrollRectRight = Mathf.
                Vector3 childUpLeft = new Vector3(mostRightTrans.anchoredPosition.x, mostRightTrans.anchoredPosition.y, 0f);

                float childLeft = transform.TransformPoint(childUpLeft).x;

                //Debug.Log("childLeft" + childLeft + "scrollRectRight" + scrollRectRight);
                if (float.Parse(childLeft.ToString("0.00")) >= float.Parse(scrollRectRight.ToString("0.00")))
                {
                    //Debug.Log("childLeft>scrollRectRight");
                    //把右边的一行 移动到左边;
                    mostRightTrans.anchoredPosition = new Vector2(mostLiftTrans.anchoredPosition.x - mGridLayoutGroup.cellSize.x - mGridLayoutGroup.spacing.x, mostRightTrans.anchoredPosition.y);

                    mostRightTrans.gameObject.SetActive(true);

                    if (mChildsSiblingIndex.ContainsKey(mostRightTrans))
                    {
                        int childIndex = mChildsSiblingIndex[mostRightTrans];
                        m_arrRealIndex[childIndex] = (uint)(m_nRealIndex - mChildren.Count);
                        UpdateChildrenCallback(childIndex, m_nRealIndex - mChildren.Count);
                    }

                    //GridLayoutGroup 右侧缩短;
                    //Debug.Log("333rectTransform.sizeDelta.x" + mRectTransform.sizeDelta.x + "mRectTransform.anchoredPosition.x" + mRectTransform.anchoredPosition.x + "mRectTransform.anchoredPosition.y" + mRectTransform.anchoredPosition.y);
                    //mRectTransform.sizeDelta -= new Vector2(mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x, 0);
                    //Debug.Log("444rectTransform.sizeDelta.x" + mRectTransform.sizeDelta.x + "mRectTransform.anchoredPosition.x" + mRectTransform.anchoredPosition.x + "mRectTransform.anchoredPosition.y" + mRectTransform.anchoredPosition.y);
                    m_nRealIndex -= mGridLayoutGroup.constraintCount;
                    //Debug.Log("m_nRealIndex" + m_nRealIndex);
                }
            }
        }
        mStartPosition = currentPos;
    }
    //找到最左侧的组件
    private RectTransform GetMostLeftTransForm()
    {
        RectTransform transForm = null;
        for (int index = 0; index < transform.childCount; index++)
        {
            Transform trans = transform.GetChild(index);
            if(trans != null)
            {
                RectTransform tempTrans = trans.GetComponent<RectTransform>();
                if (tempTrans != null)
                {
                    if (transForm == null)
                    {
                        transForm = tempTrans;
                    }
                    else
                    {
                        if (tempTrans.anchoredPosition.x < transForm.anchoredPosition.x)
                        {
                            transForm = tempTrans;
                        }
                    }
                }
            }
        }
        return transForm;
    }
    //找到最右侧的组件
    private RectTransform GetMostRightTransForm()
    {
        RectTransform transForm = null;
        for (int index = 0; index < transform.childCount; index++)
        {
            Transform trans = transform.GetChild(index);
            if (trans != null)
            {
                RectTransform tempTrans = trans.GetComponent<RectTransform>();
                if (tempTrans != null)
                {
                    if (transForm == null)
                    {
                        transForm = tempTrans;
                    }
                    else
                    {
                        if (tempTrans.anchoredPosition.x > transForm.anchoredPosition.x)
                        {
                            transForm = tempTrans;
                        }
                    }
                }
            }
        }
        return transForm;
    }
    //找到最顶端的组件
    private RectTransform GetMostUpTransForm()
    {
        RectTransform transForm = null;
        for (int index = 0; index < transform.childCount; index++)
        {
            Transform trans = transform.GetChild(index);
            if (trans != null)
            {
                RectTransform tempTrans = trans.GetComponent<RectTransform>();
                if (tempTrans != null)
                {
                    if (transForm == null)
                    {
                        transForm = tempTrans;
                    }
                    else
                    {
                        if (tempTrans.anchoredPosition.y > transForm.anchoredPosition.y)
                        {
                            transForm = tempTrans;
                        }
                    }
                }
            }
        }
        return transForm;
    }
    //找到最底端的组件
    private RectTransform GetMostDownTransForm()
    {
        RectTransform transForm = null;
        for (int index = 0; index < transform.childCount; index++)
        {
            Transform trans = transform.GetChild(index);
            if (trans != null)
            {
                RectTransform tempTrans = trans.GetComponent<RectTransform>();
                if (tempTrans != null)
                {
                    if (transForm == null)
                    {
                        transForm = tempTrans;
                    }
                    else
                    {
                        if (tempTrans.anchoredPosition.y < transForm.anchoredPosition.y)
                        {
                            transForm = tempTrans;
                        }
                    }
                }
            }
        }
        return transForm;
    }
    void UpdateChildrenCallback(int uiIndex, int index) 
    {
        if (onDragMove != null)
        {
            onDragMove.Invoke(uiIndex, index);
        }
    }
    /// <summary>
    /// 设置总的个数;
    /// </summary>
    /// <param name="count"></param>
    public void SetAmount(int count)
    {
        InitChildren();
        if (count < m_nAmount || m_nAmount < m_nMinAmount)
        {
            Clear();
            for (int index = 0; index < m_nMinAmount; index++)
            {
                mChildren[index].gameObject.SetActive(index < count);
            }
        }
        m_nAmount = count;
        // 判断是否是左右拖动，还是上下拖动
        if (mGridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            // 上下拖动
            //如果小了一行，则需要把GridLayout的高度减去一行的高度;
            int row = (m_nMinAmount - m_nAmount) / mGridLayoutGroup.constraintCount;
            if (row > 0)
            {
                RectTransform scrollRectTransform = mScrollRect.GetComponent<RectTransform>();
                mRectTransform.sizeDelta = new Vector2(0, scrollRectTransform.sizeDelta.y);
            }
            else
            {
                mRectTransform.sizeDelta = new Vector2(0, (mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y) * m_nAmount);
                
            }
            mScrollContentSize = mRectTransform.sizeDelta;
        }
        else
        {
            // 左右拖动
            //如果小了一列，则需要把GridLayout的宽度减去一列的宽度;
            int column = (m_nMinAmount - m_nAmount) / mGridLayoutGroup.constraintCount;
            if (column > 0)
            {
                RectTransform scrollRectTransform = mScrollRect.GetComponent<RectTransform>();               
                mRectTransform.sizeDelta = new Vector2(scrollRectTransform.sizeDelta.x, 0);
            }
            else
            {
                mRectTransform.sizeDelta = new Vector2((mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x) * m_nAmount, 0);
            }
            mScrollContentSize = mRectTransform.sizeDelta;
        }
    }
    //自动定位
    public void AutoMoveTo(int index)
    {
        Clear();
        if (index >= m_nAmount - (m_nMinAmount - 1))
        {
            index = m_nAmount - (m_nMinAmount - 1);
        }
        if (index < 0)
        {
            index = 0;
        }
        //滚动容器当前坐标
        if (mGridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            mStartPosition = mRectTransform.anchoredPosition - new Vector2(0, (mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y) * index);
        }
        else
        {
            mStartPosition = mRectTransform.anchoredPosition - new Vector2((mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x) * index, 0);
        }

        //所有滚动组件总共需要滚动的次数
        int totalTimes = index;
        if (m_nRealIndex + index == m_nAmount)
        {
            totalTimes = index - 1;
        }
        //计算当前索引
        m_nRealIndex += index;
        if (m_nRealIndex >= m_nAmount)
        {
            m_nRealIndex = m_nAmount - 1;
        }
        //Debug.Log("m_nRealIndex" + m_nRealIndex);
        int m = (int)Mathf.Floor(totalTimes / m_nMinAmount);
        int n = (int)(totalTimes % m_nMinAmount);
        //每个组件到达指定位置需要移动的次数
        int moveTimes = 0;
        for (int i = 0; i < m_nMinAmount; i++)
        {
            moveTimes = m;
            if(i < n)
            {
                moveTimes += 1;
            }
            //计算每个滚动组件实际代表的索引
            m_arrRealIndex[i] += (uint)(moveTimes * m_nMinAmount);
            //计算每个滚动组件的坐标
            Transform trans = transform.GetChild(i).GetComponent<RectTransform>();
            if (trans != null)
            {
                RectTransform rectTrans = trans.GetComponent<RectTransform>();
                if (rectTrans != null)
                {
                    if (mGridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                    {
                        rectTrans.anchoredPosition -= new Vector2(0, (mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y) * m_nMinAmount * moveTimes);
                    }
                    else
                    {
                        rectTrans.anchoredPosition += new Vector2((mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x) * m_nMinAmount * moveTimes, 0);
                    }
                }
            }
        }
        //计算滚动组件容器的坐标
        if (mGridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            mRectTransform.anchoredPosition += new Vector2(0, (mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y) * index);
        }
        else
        {
            mRectTransform.anchoredPosition -= new Vector2((mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x) * index, 0);
        }
        //Debug.Log("anchoredPosition" + mRectTransform.anchoredPosition.x);
        //计算滚动组件容器的大小
        if (mGridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            //mRectTransform.sizeDelta += new Vector2(0, (mGridLayoutGroup.cellSize.y + mGridLayoutGroup.spacing.y) * totalTimes);
        }
        else
        {
            //mRectTransform.sizeDelta += new Vector2((mGridLayoutGroup.cellSize.x + mGridLayoutGroup.spacing.x) * totalTimes, 0);
        }
        //Debug.Log("sizeDelta" + mRectTransform.sizeDelta.x);
        if (onAutoMoveTo != null)
        {
            onAutoMoveTo.Invoke(m_arrRealIndex);
        }
    }
    void Clear()
    {
        mRectTransform.anchoredPosition = mScrollContentPos;
        mStartPosition = mRectTransform.anchoredPosition;
        mRectTransform.sizeDelta = mScrollContentSize;
        m_nRealIndex = mChildren.Count - 1;
        m_arrRealIndex = new List<uint>();
        for (int index = 0; index < transform.childCount; index++)
        {
            m_arrRealIndex.Add((uint)index);
            Transform child = transform.GetChild(index);

            RectTransform childRectTrans = child.GetComponent<RectTransform>();
            if (mChildsAnchoredPosition.ContainsKey(child))
            {
                childRectTrans.anchoredPosition = mChildsAnchoredPosition[child];
            }
            else
            {
                Debug.LogError("mChildsAnchoredPosition no contain " + child.name);
            }
        }
    }
}
}
