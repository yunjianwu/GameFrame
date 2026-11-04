// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("UI/RadioButton")]
public class RadioButton : Selectable, IPointerClickHandler
{
    [SerializeField] bool m_IsTrue;

    public bool IsTrue { get => m_IsTrue; set => SetValue(value); }

    [SerializeField] GameObject trueOption;
    public GameObject TrueOption { get => trueOption; set => trueOption = value; }

    [SerializeField] GameObject falseOption;
    public GameObject FalseOption { get => falseOption; set => falseOption = value; }

    public UnityAction<bool> OnValueChanged { get; set; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsActive() || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left) return;
        SetValue(!m_IsTrue);
    }

    protected override void Start()
    {
        base.Start();
        RefreshActive();
    }

    void SetValue(bool isTrue)
    {
        if (m_IsTrue == isTrue) { RefreshActive(); return; }
        m_IsTrue = isTrue;
        RefreshActive();
        OnValueChanged?.Invoke(isTrue);
    }

    void RefreshActive()
    {
        if (TrueOption)
            TrueOption.SetActive(m_IsTrue);
        if (FalseOption)
            FalseOption.SetActive(!m_IsTrue);
    }
}
}
