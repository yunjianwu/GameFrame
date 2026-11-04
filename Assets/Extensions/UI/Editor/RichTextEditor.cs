// Migrated from MergeCookAndroid; see docs/migration-map.md.
namespace Starter.Extensions.UI.Editor
{
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

[CustomEditor(typeof(RichText))]
public class RichTextEditor : UnityEditor.UI.TextEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.HelpBox("Assign SpriteResolver from your UI presenter. Keys are the names in <#key> / <quad name=key ... />.", MessageType.Info);
    }
}
}
