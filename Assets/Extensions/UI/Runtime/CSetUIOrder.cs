// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class CSetUIOrder : MonoBehaviour
{

    public int m_order = 3;
    void Start()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = m_order;
    }
}
}
