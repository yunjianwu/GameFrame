using System;
using System.IO;
using System.Linq;
using Spine.Unity;
using Starter.Extensions.Effects;
using Starter.Extensions.Spine;
using Starter.Extensions.Tutorial;
using Starter.Extensions.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Starter.Extensions.Samples.Editor
{
    public static class ExtensionsSampleBuilder
    {
        public const string GalleryScene = "Assets/Extensions/Samples/Scenes/ExtensionGallery.unity";
        private const string Content = "Assets/Extensions/Samples/Content";
        private const string Prefabs = "Assets/Extensions/Samples/Prefabs";
        private static readonly Color Ink = new Color32(24, 49, 42, 255);
        private static readonly Color Paper = new Color32(243, 240, 226, 255);
        private static readonly Color Accent = new Color32(205, 229, 133, 255);
        private static readonly Color Muted = new Color32(208, 216, 198, 255);

        [MenuItem("Starter/5. Create or Open Extension Gallery")]
        public static void OpenGallery()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(GalleryScene)) EditorSceneManager.OpenScene(GalleryScene);
            else BuildGallery();
        }

        public static void BuildGallery()
        {
            if (File.Exists(GalleryScene)) { ValidateExtensions(); return; }
            if (Directory.Exists(Content) || Directory.Exists(Prefabs))
                throw new InvalidOperationException("Partial gallery assets already exist. Review them before generating; nothing was overwritten.");
            Directory.CreateDirectory(Content);
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Path.GetDirectoryName(GalleryScene));
            AssetDatabase.Refresh();
            var skeleton = CreateSkeleton();
            var config = CreateTutorialConfig();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
            camera.cullingMask = ~(1 << 31); camera.transform.position = new Vector3(0, 0, -10);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = new GameObject("ExtensionGallery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            var demo = canvas.AddComponent<ExtensionsDemo>();
            var background = Rect("Background", canvas.transform, Vector2.zero, new Vector2(1080, 1920)); Stretch(background);
            background.gameObject.AddComponent<Image>().color = Ink;
            Label(canvas.transform, "Eyebrow", "GF STARTER / REUSABLE EXTENSIONS", 24, new Vector2(0, 865), new Vector2(980, 50), Accent);
            Label(canvas.transform, "Title", "TOOLS FOR YOUR NEXT GAME", 49, new Vector2(0, 790), new Vector2(1000, 80), Paper);
            Label(canvas.transform, "Subtitle", "Independent modules. Inspect the scene, then copy what you need.", 23, new Vector2(0, 720), new Vector2(1000, 60), Muted);
            string[] keys = { "Tutorial", "UI", "Lists", "Spine", "Effects" };
            demo.navigation = new Button[keys.Length]; demo.pages = new GameObject[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                demo.navigation[i] = Button(canvas.transform, "Nav" + keys[i], keys[i].ToUpperInvariant(), new Vector2(-400 + i * 200, 625), new Vector2(185, 72));
                var page = Rect(keys[i] + "Page", canvas.transform, new Vector2(0, -70), new Vector2(1000, 1260));
                demo.pages[i] = page.gameObject;
            }
            demo.status = Label(canvas.transform, "Status", "Ready", 21, new Vector2(0, -825), new Vector2(1000, 85), Accent);
            Label(canvas.transform, "Footer", "SOURCE + SAMPLES / UGUI / SPINE 4.1 / C#", 20, new Vector2(0, -905), new Vector2(1000, 50), Muted);
            CreateTutorial(demo, config);
            CreateControls(demo);
            CreateLists(demo);
            CreateSpine(demo, skeleton);
            CreateEffects(demo);
            EditorUtility.SetDirty(demo);
            EditorSceneManager.SaveScene(scene, GalleryScene);
            if (!EditorBuildSettings.scenes.Any(item => item.path == GalleryScene))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(GalleryScene, true) }).ToArray();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            ValidateExtensions();
            Debug.Log("STARTER_EXTENSIONS_GENERATED: " + GalleryScene);
        }

        [MenuItem("Starter/6. Validate Extensions")]
        public static void ValidateExtensions()
        {
            foreach (string path in new[] { GalleryScene, Content + "/Tutorial.asset", Content + "/DemoSkeleton.asset", Prefabs + "/BasicButton.prefab", Prefabs + "/VirtualList.prefab", Prefabs + "/SpineCard.prefab" })
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) throw new InvalidOperationException("Missing extension sample: " + path);
            AssetDatabase.LoadAssetAtPath<TutorialConfig>(Content + "/Tutorial.asset").Validate();
            if (AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(Content + "/DemoSkeleton.asset").GetSkeletonData(false).FindAnimation("show") == null)
                throw new InvalidOperationException("Sample skeleton is invalid.");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script: " + prefab.name);
                foreach (var graphic in prefab.GetComponentsInChildren<MaskableGraphic>(true))
                    if (graphic.GetComponent<CanvasRenderer>() == null)
                        throw new InvalidOperationException("Missing CanvasRenderer: " + prefab.name + "/" + graphic.name);
                foreach (var graphic in prefab.GetComponentsInChildren<SkeletonGraphic>(true))
                    if (graphic.skeletonDataAsset == null || graphic.skeletonDataAsset.GetSkeletonData(false) == null)
                        throw new InvalidOperationException("Missing skeleton reference: " + prefab.name);
            }
            Debug.Log("STARTER_EXTENSIONS_VALIDATED: tutorial, skeleton and UI prefabs.");
        }

        private static void CreateTutorial(ExtensionsDemo demo, TutorialConfig config)
        {
            var page = demo.pages[0].transform;
            Heading(page, "01 / A CONFIGURABLE GUIDE", "Click, drag, then report a validated gameplay event.");
            demo.startGuide = Button(page, "StartGuide", "RESTART", new Vector2(-320, 365), new Vector2(285, 76));
            demo.resumeGuide = Button(page, "ResumeGuide", "RESUME", new Vector2(0, 365), new Vector2(285, 76));
            demo.stopGuide = Button(page, "StopGuide", "STOP", new Vector2(320, 365), new Vector2(285, 76));
            demo.targets = page.gameObject.AddComponent<TutorialTargetRegistry>();
            demo.gate = page.gameObject.AddComponent<TutorialInputGate>();
            var first = Button(page, "ConfirmTarget", "1 / CONFIRM", new Vector2(0, 175), new Vector2(540, 110));
            first.gameObject.AddComponent<TutorialTarget>().Configure("confirm", demo.targets);
            var source = Button(page, "DragSource", "2 / DRAG", new Vector2(-230, -45), new Vector2(340, 130));
            source.gameObject.AddComponent<TutorialTarget>().Configure("drag", demo.targets);
            source.gameObject.AddComponent<DemoDragItem>().gate = demo.gate;
            var destination = Button(page, "DragDestination", "DROP HERE", new Vector2(230, -45), new Vector2(340, 130));
            destination.gameObject.AddComponent<TutorialTarget>().Configure("destination", demo.targets);
            demo.businessTarget = Button(page, "BusinessTarget", "3 / CLAIM REWARD", new Vector2(0, -265), new Vector2(640, 110));
            demo.businessTarget.gameObject.AddComponent<TutorialTarget>().Configure("reward", demo.targets);
            var overlay = Rect("GuideOverlay", page, new Vector2(0, -40), new Vector2(1000, 790));
            demo.mask = overlay.gameObject.AddComponent<TutorialMask>(); demo.mask.color = new Color(0, 0, 0, 0.7f);
            SavePrefab(overlay.gameObject, "GuideOverlay");
            var pointer = Rect("Finger", page, Vector2.zero, new Vector2(55, 55));
            var circle = pointer.gameObject.AddComponent<CircleImage>(); circle.color = Paper; circle.raycastTarget = false;
            demo.finger = pointer.gameObject.AddComponent<FingerPointer>();
            demo.dialogue = Panel(page, "Dialogue", new Vector2(0, -520), new Vector2(940, 165), Paper).gameObject;
            demo.dialogueText = Label(demo.dialogue.transform, "TalkText", "Press RESTART to begin. Resume keeps the next stable step ID.", 28, Vector2.zero, new Vector2(860, 145), Ink);
            demo.tutorial = page.gameObject.AddComponent<TutorialRunner>(); demo.tutorialConfig = config;
        }

        private static void CreateControls(ExtensionsDemo demo)
        {
            var page = demo.pages[1].transform;
            Heading(page, "02 / COMMON UI CONTROLS", "Shapes, labels and interactions with no game-specific services.");
            var card = Panel(page, "ShapeCard", new Vector2(0, 240), new Vector2(940, 300), Paper);
            SavePrefab(card.gameObject, "RoundedCard");
            var circle = Rect("CircleImage", card, new Vector2(-310, 30), new Vector2(130, 130)).gameObject.AddComponent<CircleImage>();
            circle.color = Ink; circle.FillPercent = 0.76f; circle.raycastTarget = false;
            var rounded = Rect("RoundedImage", card, new Vector2(-70, 30), new Vector2(220, 130)).gameObject.AddComponent<RoundRectangleImage>();
            rounded.roundRectangleRadius = 30; rounded.color = Ink; rounded.raycastTarget = false;
            var line = Rect("Line", card, new Vector2(265, 20), new Vector2(220, 140)).gameObject.AddComponent<UILineRenderer>();
            line.color = Ink; line.raycastTarget = false;
            line.SetPoints(new[] { new Vector2(-100, -30), new Vector2(-45, 25), new Vector2(5, -5), new Vector2(80, 65) });
            Label(card, "ShapeLabel", "CIRCLE / ROUNDED RECT / UGUI LINE", 22, new Vector2(0, -100), new Vector2(860, 55), Ink);
            var textCard = Panel(page, "TextCard", new Vector2(0, -105), new Vector2(940, 330), Paper);
            var gradientText = Label(textCard, "GradientText", "GRADIENT + OUTLINE", 43, new Vector2(0, 105), new Vector2(860, 70), Ink);
            var gradient = gradientText.gameObject.AddComponent<GradientColor>(); gradient.Direction = GradientColor.DIRECTION.Horizontal;
            gradient.ColorLeft = Ink; gradient.ColorRight = new Color32(119, 144, 66, 255);
            var outline = gradientText.gameObject.AddComponent<GraphicOutline>(); outline.effectColor = new Color(0, 0, 0, 0.2f);
            var richRect = Rect("RichLabel", textCard, new Vector2(0, 25), new Vector2(860, 65));
            demo.richText = richRect.gameObject.AddComponent<RichText>();
            SetText(demo.richText, "Reward <#coin>  <a=reward.details><uline>open details</uline></a>", 30, Ink);
            demo.inlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Content + "/White.png");
            SavePrefab(richRect.gameObject, "RichLabel");
            var storyText = Label(textCard, "StoryText", "Typewriter", 27, new Vector2(0, -85), new Vector2(860, 125), Ink);
            demo.story = storyText.gameObject.AddComponent<CommonStoryText>();
            demo.replayStory = Button(page, "ReplayStory", "REPLAY TYPEWRITER", new Vector2(0, -340), new Vector2(700, 80));
            SavePrefab(demo.replayStory.gameObject, "BasicButton");
            var tabs = Rect("Tabs", page, new Vector2(0, -480), new Vector2(900, 150));
            var group = tabs.gameObject.AddComponent<ToggleGroup>();
            for (int i = 0; i < 2; i++)
            {
                var pageObject = Label(tabs, "TabPage" + i, i == 0 ? "TAB / FIRST PAGE" : "TAB / SECOND PAGE", 23, new Vector2(0, -55), new Vector2(800, 55), Paper).gameObject;
                var tabObject = Panel(tabs, "Tab" + i, new Vector2(i == 0 ? -220 : 220, 30), new Vector2(400, 70), Accent);
                Label(tabObject, "Label", "TAB " + (i + 1), 25, Vector2.zero, new Vector2(340, 60), Ink);
                var tab = tabObject.gameObject.AddComponent<Tab>(); tab.targetGraphic = tabObject.GetComponent<Image>();
                tab.TargetPage = pageObject; tab.group = group; tab.isOn = i == 0;
            }
        }

        private static void CreateLists(ExtensionsDemo demo)
        {
            var page = demo.pages[2].transform;
            Heading(page, "03 / LISTS THAT REUSE CELLS", "1,000 items / paging / expandable groups / inline details.");
            demo.virtualList = CreateList<UILoopScrollView>(page, "VirtualList", new Vector2(-240, 180), false);
            demo.pagedList = CreateList<FlipPageLoopScrollView>(page, "PagedList", new Vector2(240, 180), true);
            demo.expandList = CreateList<ExpandLoopScrollView>(page, "ExpandList", new Vector2(-240, -295), false);
            demo.tipsList = CreateList<ExpandTipsLoopScrollView>(page, "TipsList", new Vector2(240, -295), false);
            demo.expandList.m_IsExpand = true;
            var header = Button(demo.expandList.GetComponent<ScrollRect>().content, "Header", "GROUP", Vector2.zero, new Vector2(400, 60));
            demo.expandList.m_ExpandButton = header.gameObject;
            var tips = Panel(demo.tipsList.GetComponent<ScrollRect>().content, "Tips", Vector2.zero, new Vector2(430, 90), Accent);
            Label(tips, "Detail", "INLINE DETAIL", 23, Vector2.zero, new Vector2(390, 80), Ink);
            demo.tipsList.m_ExpandTips = tips.gameObject; demo.tipsList.m_TipsSpacing = 8;
            Label(page, "NormalLabel", "VIRTUAL / 1000 ITEMS", 22, new Vector2(-240, 405), new Vector2(450, 50), Accent);
            Label(page, "PagedLabel", "HORIZONTAL / 8 PAGES", 22, new Vector2(240, 405), new Vector2(450, 50), Accent);
            Label(page, "ExpandLabel", "GROUPS / CLICK HEADER", 22, new Vector2(-240, -65), new Vector2(450, 50), Accent);
            Label(page, "TipsLabel", "DETAILS / CLICK ITEM", 22, new Vector2(240, -65), new Vector2(450, 50), Accent);
            demo.previousPage = Button(page, "PreviousPage", "<", new Vector2(100, -15), new Vector2(80, 60));
            demo.nextPage = Button(page, "NextPage", ">", new Vector2(380, -15), new Vector2(80, 60));
            demo.pageNumber = Label(page, "PageNumber", "PAGE 1 / 8", 23, new Vector2(240, -15), new Vector2(190, 60), Paper);
            SavePrefab(demo.virtualList.gameObject, "VirtualList");
            SavePrefab(demo.pagedList.gameObject, "PagedList");
            SavePrefab(demo.expandList.gameObject, "ExpandableList");
            SavePrefab(demo.tipsList.gameObject, "InlineTipsList");
        }

        private static void CreateSpine(ExtensionsDemo demo, SkeletonDataAsset data)
        {
            var page = demo.pages[3].transform;
            Heading(page, "04 / SPINE, WITH LIFECYCLE", "A generated skeleton. Replace it with your own licensed assets.");
            var card = Panel(page, "SpineCard", new Vector2(0, 140), new Vector2(940, 500), Paper);
            var graphic = SkeletonGraphic.NewSkeletonGraphicGameObject(data, card, AssetDatabase.LoadAssetAtPath<Material>(Content + "/SpineGraphic.mat"));
            graphic.name = "DemoSkeleton"; graphic.rectTransform.sizeDelta = new Vector2(500, 350); graphic.raycastTarget = false;
            demo.spine = graphic.gameObject.AddComponent<SpinePlayer>(); demo.spine.Configure(graphic);
            demo.intro = graphic.gameObject.AddComponent<SpineIntroLoop>(); demo.intro.Configure(demo.spine);
            demo.spineFade = graphic.gameObject.AddComponent<SpineTransition>();
            var label = Label(graphic.transform, "SlotLabel", "SLOT ALPHA", 31, Vector2.zero, new Vector2(300, 80), Ink);
            label.gameObject.AddComponent<CanvasGroup>();
            var follower = label.gameObject.AddComponent<SlotAlphaFollowerGraphic>(); follower.skeletonGraphic = graphic; follower.slotName = "label";
            var boneFollower = label.gameObject.AddComponent<BoneFollowerGraphic>(); boneFollower.skeletonGraphic = graphic; boneFollower.boneName = "root";
            demo.sequence = graphic.gameObject.AddComponent<SpineSequence>();
            demo.sequence.steps.Add(new SpineSequenceStep { player = demo.spine, animationName = "show", repeatCount = 2, loopAfter = "idle" });
            demo.playSequence = Button(page, "PlaySequence", "PLAY SHOW x2 > IDLE", new Vector2(0, -200), new Vector2(820, 90));
            demo.fadeSpine = Button(page, "FadeSpine", "FADE", new Vector2(-220, -335), new Vector2(380, 85));
            demo.speedSpine = Button(page, "SpeedSpine", "SPEED 1x / 2x", new Vector2(220, -335), new Vector2(380, 85));
            Label(page, "SpineNotes", "Track index and start time are separate.\nCompletion, interruption and cancellation have explicit results.\nThe label follows the root bone and reads slot alpha once.", 26, new Vector2(0, -510), new Vector2(940, 190), Paper);
            // Persist explicit references after editor component initialization has completed.
            var serialized = new SerializedObject(graphic);
            serialized.FindProperty("skeletonDataAsset").objectReferenceValue = data;
            serialized.FindProperty("m_Material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Content + "/SpineGraphic.mat");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(graphic);
            SavePrefab(card.gameObject, "SpineCard");
        }

        private static void CreateEffects(ExtensionsDemo demo)
        {
            var page = demo.pages[4].transform;
            Heading(page, "05 / PARTICLE CPU BUDGET", "Forced, high and normal priorities share one simulation clock.");
            demo.effectsWorld = new GameObject("EffectsWorld");
            demo.particleBudget = demo.effectsWorld.AddComponent<ParticleSimulationBudgetManager>();
            var renderTexture = new RenderTexture(640, 400, 16) { name = "ParticlePreview" };
            AssetDatabase.CreateAsset(renderTexture, Content + "/ParticlePreview.renderTexture");
            var cameraObject = new GameObject("ParticleCamera", typeof(Camera)); cameraObject.transform.SetParent(demo.effectsWorld.transform, false);
            var camera = cameraObject.GetComponent<Camera>(); camera.transform.localPosition = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 3; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink; camera.targetTexture = renderTexture;
            var preview = Rect("ParticlePreview", page, new Vector2(0, 160), new Vector2(940, 560)).gameObject.AddComponent<RawImage>();
            preview.texture = renderTexture; preview.raycastTarget = false;
            var material = new Material(Shader.Find("Particles/Standard Unlit")); material.SetColor("_Color", Accent);
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Content + "/White.png");
            AssetDatabase.CreateAsset(material, Content + "/Particle.mat");
            for (int i = 0; i < 3; i++)
            {
                var emitter = new GameObject("Emitter" + i, typeof(ParticleSystem)); emitter.layer = 31;
                emitter.transform.SetParent(demo.effectsWorld.transform, false); emitter.transform.localPosition = new Vector3((i - 1) * 2.8f, -0.5f, 0);
                var particles = emitter.GetComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main; main.loop = true; main.playOnAwake = true; main.startLifetime = 2; main.startSpeed = 1;
                main.startSize = 0.15f; main.maxParticles = 100; main.startColor = i == 0 ? Accent : i == 1 ? Paper : Muted;
                var emission = particles.emission; emission.rateOverTime = 18;
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.3f;
                emitter.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                emitter.AddComponent<ParticleSystemController>().PriorityPreset = (ParticlePriorityPreset)i;
            }
            demo.particleStats = Label(page, "ParticleStats", "Waiting for simulation...", 28, new Vector2(0, -260), new Vector2(900, 260), Paper);
            demo.toggleBudget = Button(page, "ToggleBudget", "TOGGLE BUDGET / AUTOMATIC", new Vector2(0, -480), new Vector2(820, 85));
            Label(page, "BudgetNote", "Soft budget: one Simulate call can exceed the limit. Profile on device.", 21, new Vector2(0, -580), new Vector2(960, 70), Muted);
        }

        private static T CreateList<T>(Transform parent, string name, Vector2 position, bool horizontal) where T : UILoopScrollView
        {
            var view = Panel(parent, name, position, new Vector2(440, 330), Paper);
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = horizontal; scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            var content = Rect("Content", view, Vector2.zero, new Vector2(440, 330));
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            scroll.content = content; scroll.viewport = view;
            var cell = Button(content, "Cell", "ITEM", Vector2.zero, horizontal ? new Vector2(440, 310) : new Vector2(420, 62));
            cell.GetComponent<Image>().color = Muted;
            var list = view.gameObject.AddComponent<T>(); list.m_CellGameObject = cell.gameObject;
            list.m_Direction = horizontal ? Scroll_Direction.Horizontal : Scroll_Direction.Vertical; list.m_IsShowArrow = false;
            list.m_RowSpacing = 8; list.m_ColSpacing = horizontal ? 0 : 8;
            return list;
        }

        private static TutorialConfig CreateTutorialConfig()
        {
            var config = ScriptableObject.CreateInstance<TutorialConfig>(); config.tutorialId = "starter.gallery";
            config.steps.Add(new TutorialStep { id = "confirm", type = TutorialStepType.Click, targetId = "confirm", talkText = "Step 1 / click the highlighted button." });
            config.steps.Add(new TutorialStep { id = "drag", type = TutorialStepType.Drag, targetId = "drag", secondTargetId = "destination", talkText = "Step 2 / drag the left item onto DROP HERE." });
            config.steps.Add(new TutorialStep { id = "reward", type = TutorialStepType.Signal, targetId = "reward", completionSignal = "reward.claimed", talkText = "Step 3 / claim the reward. The game validates it and reports a signal." });
            AssetDatabase.CreateAsset(config, Content + "/Tutorial.asset"); return config;
        }

        private static SkeletonDataAsset CreateSkeleton()
        {
            var white = new Texture2D(2, 2); white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); white.Apply();
            File.WriteAllBytes(Content + "/White.png", white.EncodeToPNG()); Object.DestroyImmediate(white);
            File.WriteAllText(Content + "/DemoAtlas.txt", "White.png\nsize: 2,2\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\nwhite\n  rotate: false\n  xy: 0,0\n  size: 2,2\n  orig: 2,2\n  offset: 0,0\n  index: -1\n");
            File.WriteAllText(Content + "/DemoSkeleton.txt", @"{
  ""skeleton"": {""hash"":""starter-generated"",""spine"":""4.1.00"",""width"":340,""height"":210},
  ""bones"": [{""name"":""root""}],
  ""slots"": [{""name"":""card"",""bone"":""root"",""attachment"":""white"",""color"":""cde585ff""},{""name"":""label"",""bone"":""root""}],
  ""skins"": [{""name"":""default"",""attachments"":{""card"":{""white"":{""width"":340,""height"":210}}}}],
  ""animations"": {
    ""show"": {""bones"":{""root"":{""translate"":[{""y"":-45},{""time"":0.45,""y"":0}],""scale"":[{""x"":0.8,""y"":0.8},{""time"":0.45,""x"":1,""y"":1}]}},""slots"":{""label"":{""alpha"":[{""value"":0},{""time"":0.45,""value"":1}]}}},
    ""idle"": {""bones"":{""root"":{""rotate"":[{""value"":-4},{""time"":1,""value"":4},{""time"":2,""value"":-4}]}},""slots"":{""label"":{""alpha"":[{""value"":1},{""time"":1,""value"":0.35},{""time"":2,""value"":1}]}}}
  }
}");
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath(Content + "/White.png");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            var material = new Material(Shader.Find("Spine/SkeletonGraphic")) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Content + "/White.png") };
            AssetDatabase.CreateAsset(material, Content + "/SpineGraphic.mat");
            var atlas = SpineAtlasAsset.CreateRuntimeInstance(AssetDatabase.LoadAssetAtPath<TextAsset>(Content + "/DemoAtlas.txt"), new[] { material }, true);
            AssetDatabase.CreateAsset(atlas, Content + "/DemoAtlas.asset");
            var data = SkeletonDataAsset.CreateRuntimeInstance(AssetDatabase.LoadAssetAtPath<TextAsset>(Content + "/DemoSkeleton.txt"), atlas, true, 1);
            AssetDatabase.CreateAsset(data, Content + "/DemoSkeleton.asset"); return data;
        }

        private static void Heading(Transform parent, string title, string description)
        {
            Label(parent, "Heading", title, 36, new Vector2(0, 550), new Vector2(960, 75), Paper);
            Label(parent, "Description", description, 23, new Vector2(0, 475), new Vector2(960, 70), Muted);
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, position, size); var image = rect.gameObject.AddComponent<RoundRectangleImage>();
            image.roundRectangleRadius = 18; image.color = color; return rect;
        }
        private static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            var rect = Panel(parent, name, position, size, Accent); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); rect.gameObject.AddComponent<UIBtnScaleEffect>();
            Label(rect, "Label", text, 26, Vector2.zero, size - new Vector2(20, 10), Ink); return button;
        }
        private static Text Label(Transform parent, string name, string text, int fontSize, Vector2 position, Vector2 size, Color color)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>(); SetText(label, text, fontSize, color);
            label.raycastTarget = false; return label;
        }
        private static void SetText(Text label, string text, int fontSize, Color color)
        {
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.text = text; label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter; label.color = color; label.supportRichText = true;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void SavePrefab(GameObject go, string name) { PrefabUtility.SaveAsPrefabAsset(go, Prefabs + "/" + name + ".prefab"); }
    }
}
