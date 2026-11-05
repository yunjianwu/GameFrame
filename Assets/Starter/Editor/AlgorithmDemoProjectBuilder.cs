using System;
using System.IO;
using System.Linq;
using AlgorithmDemos;
using Demos.BoidsFlocking;
using Demos.FlowFieldPathfinding;
using Demos.QuadTreeBoids;
using Starter.Game;
using Starter.Game.Procedures;
using Starter.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityGameFramework.Editor.ResourceTools;
using UnityGameFramework.Runtime;
using Object = UnityEngine.Object;

namespace Starter.Editor
{
    public static class AlgorithmDemoProjectBuilder
    {
        private static readonly Color Ink = new Color32(24, 49, 42, 255);
        private static readonly Color Paper = new Color32(243, 240, 226, 255);
        private static readonly Color Accent = new Color32(205, 229, 133, 255);
        private static readonly Color Card = new Color32(34, 67, 56, 255);

        [MenuItem("Demos/1. Configure Algorithm Demos")]
        public static void ConfigureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Configure();
        }

        public static void Configure()
        {
            EditorSceneManager.OpenScene(StarterProjectBuilder.BootScene, OpenSceneMode.Single);
            Directory.CreateDirectory("Assets/AlgorithmDemos/Scenes");
            AssetDatabase.Refresh();
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>(AssetNames.Menu);
            if (menu == null || new SerializedObject(menu.GetComponent<MenuForm>()).FindProperty("_boids").objectReferenceValue == null)
                CreateMenu();
            if (!File.Exists(AssetNames.DemoNavigation)) CreateNavigation();
            foreach (DemoDefinition demo in DemoCatalog.All) CreateScene(demo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RegisterProcedure();
            RegisterResources();
            var buildScenes = EditorBuildSettings.scenes.ToList();
            foreach (DemoDefinition demo in DemoCatalog.All)
                if (buildScenes.All(item => item.path != demo.SceneAsset))
                    buildScenes.Add(new EditorBuildSettingsScene(demo.SceneAsset, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AdaptFrameworkSample();
            IncludeSpriteShader();
            PlayerSettings.productName = "UnityGFDemos";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.yourstudio.gf.demos");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.yourstudio.gf.demos");
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("ALGORITHM_DEMOS_CONFIGURED: GF launcher, three owned scenes, navigation and resource packages.");
        }

        [MenuItem("Demos/2. Validate Algorithm Demos")]
        public static void Validate()
        {
            var collection = new ResourceCollection();
            if (!collection.Load()) throw new InvalidOperationException("Cannot load resource collection.");
            foreach (DemoDefinition demo in DemoCatalog.All)
            {
                if (!File.Exists(demo.SceneAsset) || !collection.HasAsset(AssetDatabase.AssetPathToGUID(demo.SceneAsset)))
                    throw new InvalidOperationException("Demo scene is missing from GF resources: " + demo.SceneAsset);
                Scene scene = EditorSceneManager.OpenScene(demo.SceneAsset, OpenSceneMode.Additive);
                try
                {
                    var registrations = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AlgorithmDemoScene>(true)).ToArray();
                    if (registrations.Length != 1 || registrations[0].Kind != demo.Kind || registrations[0].Simulation == null)
                        throw new InvalidOperationException("Invalid demo scene registration: " + demo.SceneAsset);
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                                throw new InvalidOperationException("Missing script in " + demo.SceneAsset);
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            var navigation = AssetDatabase.LoadAssetAtPath<GameObject>(AssetNames.DemoNavigation);
            if (navigation == null || navigation.GetComponent<DemoNavigationForm>() == null ||
                !collection.HasAsset(AssetDatabase.AssetPathToGUID(AssetNames.DemoNavigation)))
                throw new InvalidOperationException("Missing navigation UI or resource mapping.");
            Scene boot = SceneManager.GetSceneByPath(StarterProjectBuilder.BootScene);
            bool openedHere = !boot.IsValid() || !boot.isLoaded;
            if (openedHere) boot = EditorSceneManager.OpenScene(StarterProjectBuilder.BootScene, OpenSceneMode.Additive);
            try
            {
                var component = boot.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ProcedureComponent>(true)).Single();
                var names = new SerializedObject(component).FindProperty("m_AvailableProcedureTypeNames");
                bool found = false;
                for (int i = 0; i < names.arraySize; i++)
                    found |= names.GetArrayElementAtIndex(i).stringValue == typeof(ProcedureAlgorithmDemo).FullName;
                if (!found) throw new InvalidOperationException("ProcedureAlgorithmDemo is not registered.");
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(boot, true); }
            Debug.Log("ALGORITHM_DEMOS_VALIDATED: Three GF scenes, simulations, navigation, resources and procedure registration.");
        }

        private static void CreateScene(DemoDefinition demo)
        {
            if (File.Exists(demo.SceneAsset)) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject(demo.Title);
                SceneManager.MoveGameObjectToScene(root, scene);
                var registration = root.AddComponent<AlgorithmDemoScene>();
                MonoBehaviour simulation;
                switch (demo.Kind)
                {
                    case DemoKind.Boids:
                        simulation = root.AddComponent<BoidsDemo>();
                        SetInt(simulation, "boidCount", 200);
                        break;
                    case DemoKind.FlowField:
                        simulation = root.AddComponent<FlowFieldDemo>();
                        SetInt(simulation, "step", 4);
                        break;
                    case DemoKind.QuadTree:
                        simulation = root.AddComponent<QuadTreeDemo>();
                        SetInt(simulation, "boidCount", 1000);
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }
                SetInt(registration, "_kind", (int)demo.Kind);
                SetReference(registration, "_simulation", simulation);
                EditorSceneManager.SaveScene(scene, demo.SceneAsset);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void RegisterProcedure()
        {
            Scene boot = SceneManager.GetSceneByPath(StarterProjectBuilder.BootScene);
            var component = boot.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ProcedureComponent>(true)).Single();
            var serialized = new SerializedObject(component);
            var names = serialized.FindProperty("m_AvailableProcedureTypeNames");
            string name = typeof(ProcedureAlgorithmDemo).FullName;
            bool found = false;
            for (int i = 0; i < names.arraySize; i++) found |= names.GetArrayElementAtIndex(i).stringValue == name;
            if (!found)
            {
                int index = names.arraySize++;
                names.GetArrayElementAtIndex(index).stringValue = name;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(boot);
                EditorSceneManager.SaveScene(boot);
            }
        }

        private static void RegisterResources()
        {
            var collection = new ResourceCollection();
            if (!collection.Load()) throw new InvalidOperationException("Cannot load GF resource collection.");
            foreach (DemoDefinition demo in DemoCatalog.All)
            {
                if (!collection.HasResource(demo.ResourceName, null) &&
                    !collection.AddResource(demo.ResourceName, null, null, LoadType.LoadFromFile, true))
                    throw new InvalidOperationException("Cannot add resource: " + demo.ResourceName);
                string guid = AssetDatabase.AssetPathToGUID(demo.SceneAsset);
                if (!collection.HasAsset(guid) && !collection.AssignAsset(guid, demo.ResourceName, null))
                    throw new InvalidOperationException("Cannot assign scene: " + demo.SceneAsset);
            }
            string navigation = AssetDatabase.AssetPathToGUID(AssetNames.DemoNavigation);
            if (!collection.HasAsset(navigation) && !collection.AssignAsset(navigation, "demo/ui", null))
                throw new InvalidOperationException("Cannot assign navigation UI.");
            if (!collection.Save()) throw new InvalidOperationException("Cannot save demo resources.");
        }

        private static void CreateMenu()
        {
            GameObject root = Form("MenuForm", true);
            try
            {
                var logic = root.AddComponent<MenuForm>();
                var content = Rect(root.transform, "Card", Vector2.zero, new Vector2(1280, 720));
                Text(content, "Eyebrow", "GAME FRAMEWORK  /  INTERACTIVE ALGORITHMS", new Vector2(-290, 296), new Vector2(600, 30), 16, Accent, TextAnchor.MiddleLeft);
                var title = Text(content, "Title", "ALGORITHM LAB", new Vector2(-225, 230), new Vector2(730, 90), 64, Paper, TextAnchor.MiddleLeft);
                Text(content, "Description", "Three ideas. One framework. Explore how a crowd finds its way.", new Vector2(-70, 164), new Vector2(1040, 36), 21, Paper, TextAnchor.MiddleLeft);
                var play = Button(content, "PlayButton", "FRAMEWORK SAMPLE", new Vector2(463, 294), new Vector2(250, 40), 15);
                Button[] demoButtons = new Button[3];
                for (int i = 0; i < DemoCatalog.All.Count; i++)
                {
                    DemoDefinition demo = DemoCatalog.All[i];
                    var card = Rect(content, "Demo" + demo.Kind, new Vector2((i - 1) * 388, -55), new Vector2(356, 348));
                    var image = card.gameObject.AddComponent<Image>();
                    image.color = Card;
                    image.raycastTarget = false;
                    Text(card, "Index", "0" + (i + 1) + "  /  " + (i == 0 ? "EMERGENCE" : i == 1 ? "NAVIGATION" : "SPATIAL INDEX"), new Vector2(0, 133), new Vector2(306, 32), 15, Accent);
                    Text(card, "Name", demo.Title, new Vector2(0, 72), new Vector2(320, 65), i == 1 ? 39 : 44, Paper);
                    Text(card, "Topic", i == 0 ? "O(N x N) NEIGHBOR SEARCH" : i == 1 ? "ONE FIELD / MANY AGENTS" : "QUERY NEARBY / SKIP THE REST", new Vector2(0, 18), new Vector2(320, 35), 13, Accent);
                    Text(card, "Description", demo.Description + "\n\n" + (i == 0 ? "Steer a flock with your mouse." : i == 1 ? "Paint walls. Watch the crowd reroute." : "Split space and compare the cost."), new Vector2(0, -48), new Vector2(306, 90), 17, Paper);
                    string buttonName = i == 0 ? "BoidsButton" : i == 1 ? "FlowFieldButton" : "QuadTreeButton";
                    demoButtons[i] = Button(card, buttonName, "OPEN DEMO  >", new Vector2(0, -127), new Vector2(300, 50), 18);
                }
                var balance = Text(content, "CoinsValue", "Saved coins: 100", new Vector2(-415, -292), new Vector2(340, 30), 15, Accent, TextAnchor.MiddleLeft);
                Text(content, "Footer", "GF PROCEDURE  /  OWNED SCENES  /  POOLED UI  /  C# SOURCE", new Vector2(180, -292), new Vector2(810, 30), 14, Paper, TextAnchor.MiddleRight);
                SetReference(logic, "_title", title);
                SetReference(logic, "_balance", balance);
                SetReference(logic, "_play", play);
                SetReference(logic, "_boids", demoButtons[0]);
                SetReference(logic, "_flowField", demoButtons[1]);
                SetReference(logic, "_quadTree", demoButtons[2]);
                PrefabUtility.SaveAsPrefabAsset(root, AssetNames.Menu);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateNavigation()
        {
            GameObject root = Form("DemoNavigationForm", false);
            try
            {
                var logic = root.AddComponent<DemoNavigationForm>();
                var panel = Rect(root.transform, "Navigation", Vector2.zero, new Vector2(526, 50));
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1);
                panel.pivot = new Vector2(0.5f, 1);
                panel.anchoredPosition = new Vector2(0, -12);
                panel.gameObject.AddComponent<Image>().color = new Color32(24, 49, 42, 240);
                var back = Button(panel, "BackButton", "< HUB", new Vector2(-200, 0), new Vector2(104, 34), 16);
                var title = Text(panel, "Title", "ALGORITHM DEMO", new Vector2(0, 0), new Vector2(275, 38), 20, Paper);
                var restart = Button(panel, "RestartButton", "RESET", new Vector2(200, 0), new Vector2(104, 34), 16);
                SetReference(logic, "_panel", panel);
                SetReference(logic, "_title", title);
                SetReference(logic, "_back", back);
                SetReference(logic, "_restart", restart);
                PrefabUtility.SaveAsPrefabAsset(root, AssetNames.DemoNavigation);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AdaptFrameworkSample()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AssetNames.Play);
            try
            {
                root.transform.Find("Card").localScale = Vector3.one * 0.5f;
                PrefabUtility.SaveAsPrefabAsset(root, AssetNames.Play);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void IncludeSpriteShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Missing Sprites/Default shader.");
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < shaders.arraySize; i++)
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int index = shaders.arraySize++;
            shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Form(string name, bool background)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            if (background) root.AddComponent<Image>().color = Ink;
            return root;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = Rect(parent, name, position, size).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = Rect(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Accent;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text(rect, "Label", value, Vector2.zero, size - new Vector2(8, 4), fontSize, Ink);
            return button;
        }

        private static void SetReference(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string property, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
