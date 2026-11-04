// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("UI/Tab")]
public class Tab : Toggle
{
    [SerializeField] GameObject targetPage;
    public GameObject TargetPage { get => targetPage; set { targetPage = value; RefreshActive(); } }

    [SerializeField] [Tooltip("切换选项时 是否使用显示隐藏方式")] bool isChangeActive;
    public bool IsChangeActive { get => isChangeActive; set => isChangeActive = value; }

    protected override void OnEnable()
    {
        base.OnEnable();
        onValueChanged.RemoveListener(OnSelectionChanged);
        onValueChanged.AddListener(OnSelectionChanged);
        RefreshActive();
    }

    protected override void OnDisable()
    {
        onValueChanged.RemoveListener(OnSelectionChanged);
        base.OnDisable();
    }

    private void OnSelectionChanged(bool value) { RefreshActive(); }

    public override void OnPointerClick(PointerEventData eventData)
    {
        base.OnPointerClick(eventData);
        RefreshActive();
    }

    void RefreshActive()
    {
        if (TargetPage)
            TargetPage.SetActive(isOn);
        if (group is TabGroup tg && isOn)
        {
            tg.CurrentSelectTab = this;
            if (tg.CurrentActivityPage && tg.CurrentActivityPage != TargetPage && isOn)
                tg.CurrentActivityPage.SetActive(false);
            tg.CurrentActivityPage = TargetPage;
        }
        if (IsChangeActive)
        {
            if (graphic != null) graphic.gameObject.SetActive(isOn);
            if (targetGraphic != null) targetGraphic.gameObject.SetActive(!isOn);
        }
    }
}
}
