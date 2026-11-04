// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/TabGroup")]
public class TabGroup : ToggleGroup
{
    public GameObject CurrentActivityPage { get; set; }
    public Tab CurrentSelectTab { get; set; }
}
}
