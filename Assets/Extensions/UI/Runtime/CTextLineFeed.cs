// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI
{
using UnityEngine;
using UnityEngine.UI;

public class CTextLineFeed : MonoBehaviour
{
    void Update()
    {
        Text txt = this.gameObject.GetComponent<Text>();
        if (txt != null)
        {
            txt.text = txt.text.Replace("\\n", "\n");
        }
    }
}
}
