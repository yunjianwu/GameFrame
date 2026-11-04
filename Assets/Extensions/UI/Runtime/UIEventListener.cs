// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class UIEventListener : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler
{
   // 定义事件代理
    public delegate void UIEventProxy(GameObject gb);

    // 鼠标点击事件
    public Action<PointerEventData> OnClick;

    // 鼠标进入事件
    public Action<PointerEventData> OnMouseEnter;

    // 鼠标滑出事件
    public Action<PointerEventData> OnMouseExit;

    // 鼠标按下事件
    public Action<PointerEventData> OnMouseDown;

    // 鼠标抬起事件
    public Action<PointerEventData> OnMouseUp;

    // 鼠标开始拖拽事件
    public Action<PointerEventData> OnMouseBeginDrag;

    // 鼠标结束拖拽事件
    public Action<PointerEventData> OnMouseEndDrag;

    // 鼠标拖拽事件
    public Action<PointerEventData> OnMouseDrag;

    // 鼠标滚动事件

    public  Action<PointerEventData> OnMouseScroll;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (OnClick != null)
            OnClick.Invoke(eventData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (OnMouseEnter != null)
            OnMouseEnter.Invoke(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (OnMouseExit != null)
            OnMouseExit.Invoke(eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (OnMouseDown != null)
            OnMouseDown.Invoke(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (OnMouseUp != null)
            OnMouseUp.Invoke(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (OnMouseBeginDrag != null)
            OnMouseBeginDrag.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (OnMouseEndDrag != null)
            OnMouseEndDrag.Invoke(eventData);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (OnMouseScroll != null)
        {
            OnMouseScroll.Invoke(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(OnMouseDrag != null)
        {
            OnMouseDrag.Invoke(eventData);
        }
    }
}
}
