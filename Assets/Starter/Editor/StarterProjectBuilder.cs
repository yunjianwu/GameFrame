using System;
using System.IO;
using Starter.Game;
using Starter.Game.Procedures;
using Starter.Game.UI;
using Starter.Runtime;
using Starter.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityGameFramework.Editor.ResourceTools;
using UnityGameFramework.Runtime;
using Object = UnityEngine.Object;

namespace Starter.Editor
{
    public static class StarterProjectBuilder
    {
        public const string BootScene = "Assets/Game/Scenes/Boot.unity";
        private const string FrameworkPrefab = "Assets/Framework/UnityGameFramework/GameFramework.prefab";
        private static readonly Color Ink = new Color32(24, 49, 42, 255);
        private static readonly Color Paper = new Color32(243, 240, 226, 255);
        private static readonly Color Accent = new Color32(205, 229, 133, 255);

        [MenuItem("Starter/1. Create or Open Demo")]
        public static void CreateOrOpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(BootScene)) EditorSceneManager.OpenScene(BootScene);
            else BuildDemo();
        }

        // Batch entry point. Existing assets are never silently overwritten.
        public static void BuildDemo()
        {
            if (File.Exists(BootScene)) { ValidateDemo(); return; }
            if (File.Exists(AssetNames.Menu) || File.Exists(AssetNames.Play))
                throw new InvalidOperationException("Some demo assets already exist. Review them before generating a new scene.");

            Directory.CreateDirectory("Assets/Game/Scenes");
            Directory.CreateDirectory("Assets/Game/Content/UI");
            Directory.CreateDirectory("Assets/GameFramework/Configs");
            AssetDatabase.Refresh();
            CreateMenuPrefab();
            CreatePlayPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GameRoot");
            root.AddComponent<GameBootstrap>();
            var gf = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FrameworkPrefab));
            gf.transform.SetParent(root.transform, false);

            var baseComponent = gf.GetComponentInChildren<BaseComponent>(true);
            SetBool(baseComponent, "m_EditorResourceMode", true);
            SetInt(baseComponent, "m_FrameRate", 60);
            var procedures = gf.GetComponentInChildren<ProcedureComponent>(true);
            var serializedProcedures = new SerializedObject(procedures);
            var names = serializedProcedures.FindProperty("m_AvailableProcedureTypeNames");
            var types = new[] { typeof(ProcedureBoot), typeof(ProcedurePreload), typeof(ProcedureMenu), typeof(ProcedurePlay) };
            names.arraySize = types.Length;
            for (int i = 0; i < types.Length; i++) names.GetArrayElementAtIndex(i).stringValue = types[i].FullName;
            serializedProcedures.FindProperty("m_EntranceProcedureTypeName").stringValue = typeof(ProcedureBoot).FullName;
            serializedProcedures.ApplyModifiedPropertiesWithoutUndo();

            var ui = gf.GetComponentInChildren<UIComponent>(true);
            var serializedUi = new SerializedObject(ui);
            serializedUi.FindProperty("m_UIGroupHelperTypeName").stringValue = typeof(UguiGroupHelper).FullName;
            var groups = serializedUi.FindProperty("m_UIGroups");
            groups.arraySize = 3;
            string[] groupNames = { "Default", "Popup", "Overlay" };
            int[] depths = { 0, 1000, 2000 };
            for (int i = 0; i < groupNames.Length; i++)
            {
                var group = groups.GetArrayElementAtIndex(i);
                group.FindPropertyRelative("m_Name").stringValue = groupNames[i];
                group.FindPropertyRelative("m_Depth").intValue = depths[i];
            }
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
            var debugger = gf.GetComponentInChildren<DebuggerComponent>(true);
            if (debugger != null) debugger.enabled = false;

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, BootScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
            PlayerSettings.companyName = "YourStudio";
            PlayerSettings.productName = "UnityGFStarter";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.yourstudio.gf.starter");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.yourstudio.gf.starter");
            CreateResourceCollection();
            ProcedureFlowConfigurator.Configure();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateDemo();
            Debug.Log("STARTER_GENERATED: " + BootScene);
        }

        [MenuItem("Starter/2. Validate Demo")]
        public static void ValidateDemo()
        {
            foreach (string asset in new[] { FrameworkPrefab, BootScene, AssetNames.Config, AssetNames.Menu, AssetNames.Play })
                if (AssetDatabase.LoadMainAssetAtPath(asset) == null)
                    throw new InvalidOperationException("Missing asset: " + asset);
            foreach (string path in new[] { AssetNames.Menu, AssetNames.Play })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                        throw new InvalidOperationException("Missing script in " + path);
            }
            ProcedureFlowConfigurator.Validate();
            Debug.Log("STARTER_VALIDATED: Procedure registration, startup settings, business scenes, config and UI references.");
        }

        private static void CreateMenuPrefab()
        {
            var root = CreateForm("MenuForm");
            var logic = root.AddComponent<MenuForm>();
            var card = CreateCard(root.transform);
            CreateText(card, "Eyebrow", "UNITY / GAME FRAMEWORK", 25, 440, Accent);
            var title = CreateText(card, "Title", "A FRESH START", 74, 290, Paper);
            CreateText(card, "Description", "A small, working foundation.\nBuild your game one system at a time.", 28, 95, Paper);
            var balance = CreateText(card, "CoinsValue", "Saved coins: 100", 32, -100, Accent);
            var play = CreateButton(card, "PlayButton", "ENTER DEMO", -310);
            CreateText(card, "Footer", "SOURCE INCLUDED  /  C# FIRST  /  NO SDK REQUIRED", 18, -490, Paper);
            SetReference(logic, "_title", title);
            SetReference(logic, "_balance", balance);
            SetReference(logic, "_play", play);
            PrefabUtility.SaveAsPrefabAsset(root, AssetNames.Menu);
            Object.DestroyImmediate(root);
        }

        private static void CreatePlayPrefab()
        {
            var root = CreateForm("PlayForm");
            var logic = root.AddComponent<PlayForm>();
            var card = CreateCard(root.transform);
            CreateText(card, "Eyebrow", "MODEL  /  EVENT  /  VIEW", 25, 470, Accent);
            var balance = CreateText(card, "CoinsValue", "Coins: 100", 74, 320, Paper);
            var feedback = CreateText(card, "Feedback", "Grant coins to test the event flow.", 26, 130, Paper);
            var grant = CreateButton(card, "GrantButton", "GRANT COINS", -70);
            var save = CreateButton(card, "SaveButton", "SAVE LOCALLY", -220);
            var back = CreateButton(card, "BackButton", "BACK TO MENU", -370);
            SetReference(logic, "_balance", balance);
            SetReference(logic, "_feedback", feedback);
            SetReference(logic, "_grant", grant);
            SetReference(logic, "_save", save);
            SetReference(logic, "_back", back);
            PrefabUtility.SaveAsPrefabAsset(root, AssetNames.Play);
            Object.DestroyImmediate(root);
        }

        private static GameObject CreateForm(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            root.GetComponent<Image>().color = Ink;
            return root;
        }

        private static Transform CreateCard(Transform parent)
        {
            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(parent, false);
            ((RectTransform)card.transform).sizeDelta = new Vector2(900, 1180);
            return card.transform;
        }

        private static Text CreateText(Transform parent, string name, string text, int size, float y, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.sizeDelta = new Vector2(840, 150);
            rect.anchoredPosition = new Vector2(0, y);
            var label = item.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string text, float y)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.sizeDelta = new Vector2(740, 112);
            rect.anchoredPosition = new Vector2(0, y);
            var image = item.GetComponent<Image>();
            image.color = Accent;
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            var label = CreateText(item.transform, "Label", text, 28, 0, Ink);
            ((RectTransform)label.transform).sizeDelta = new Vector2(720, 100);
            return button;
        }

        private static void CreateResourceCollection()
        {
            const string path = "Assets/GameFramework/Configs/ResourceCollection.xml";
            if (File.Exists(path)) return;
            var collection = new ResourceCollection();
            if (!collection.AddResource("demo/config", null, null, LoadType.LoadFromFile, true) ||
                !collection.AddResource("demo/ui", null, null, LoadType.LoadFromFile, true) ||
                !collection.AssignAsset(AssetDatabase.AssetPathToGUID(AssetNames.Config), "demo/config", null) ||
                !collection.AssignAsset(AssetDatabase.AssetPathToGUID(AssetNames.Menu), "demo/ui", null) ||
                !collection.AssignAsset(AssetDatabase.AssetPathToGUID(AssetNames.Play), "demo/ui", null) ||
                !collection.Save())
                throw new InvalidOperationException("Cannot create GF resource collection.");
        }

        private static void SetReference(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string name, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string name, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
