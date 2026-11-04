using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starter.Game;
using Starter.Game.Procedures;
using Starter.Runtime;
using Starter.Runtime.Startup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGameFramework.Editor.ResourceTools;
using UnityGameFramework.Runtime;

namespace Starter.Editor
{
    /// <summary>A targeted migration: append registrations and missing assets without regenerating existing content.</summary>
    public static class ProcedureFlowConfigurator
    {
        public const string SettingsPath = "Assets/Game/Settings/StartupSettings.asset";
        private static readonly Type[] RequiredTypes =
        {
            typeof(ProcedureBoot), typeof(ProcedureLaunch), typeof(ProcedureCheckVersion), typeof(ProcedureUpdateResource),
            typeof(ProcedurePreload), typeof(ProcedureEnterGame), typeof(ProcedureChangeScene), typeof(ProcedureMenu), typeof(ProcedurePlay)
        };

        [MenuItem("Starter/7. Configure Procedure Flow")]
        public static void ConfigureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Configure();
        }

        public static void Configure()
        {
            if (!File.Exists(StarterProjectBuilder.BootScene)) throw new InvalidOperationException("Create the demo first.");
            // Batch Unity starts with a clean untitled scene; NewScene(Additive) rejects that setup.
            Scene active = SceneManager.GetActiveScene();
            if (Application.isBatchMode && SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && !active.isDirty)
                EditorSceneManager.OpenScene(StarterProjectBuilder.BootScene, OpenSceneMode.Single);
            Directory.CreateDirectory("Assets/Game/Settings");
            AssetDatabase.Refresh();
            StartupSettings settings = AssetDatabase.LoadAssetAtPath<StartupSettings>(SettingsPath);
            if (settings == null)
            {
                if (File.Exists(SettingsPath)) throw new InvalidOperationException("Unexpected asset at " + SettingsPath);
                settings = ScriptableObject.CreateInstance<StartupSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            EnsureBusinessScene(AssetNames.MenuScene, "Menu Environment");
            EnsureBusinessScene(AssetNames.PlayScene, "Play Environment");
            EnsureSceneResources();
            Scene boot = SceneManager.GetSceneByPath(StarterProjectBuilder.BootScene);
            bool openedHere = !boot.IsValid() || !boot.isLoaded;
            if (openedHere) boot = EditorSceneManager.OpenScene(StarterProjectBuilder.BootScene, OpenSceneMode.Additive);
            try
            {
                GameBootstrap host = Find<GameBootstrap>(boot);
                ProcedureComponent procedure = Find<ProcedureComponent>(boot);
                if (host == null || procedure == null) throw new InvalidOperationException("Boot needs GameBootstrap and GF ProcedureComponent.");
                var serialized = new SerializedObject(procedure);
                var names = serialized.FindProperty("m_AvailableProcedureTypeNames");
                var registered = new HashSet<string>();
                for (int i = 0; i < names.arraySize; i++) registered.Add(names.GetArrayElementAtIndex(i).stringValue);
                foreach (Type type in RequiredTypes)
                {
                    if (!registered.Add(type.FullName)) continue;
                    int index = names.arraySize++;
                    names.GetArrayElementAtIndex(index).stringValue = type.FullName;
                }
                var entrance = serialized.FindProperty("m_EntranceProcedureTypeName");
                if (string.IsNullOrEmpty(entrance.stringValue)) entrance.stringValue = typeof(ProcedureBoot).FullName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (host.StartupSettings == null)
                {
                    var serializedHost = new SerializedObject(host);
                    serializedHost.FindProperty("_startupSettings").objectReferenceValue = settings;
                    serializedHost.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(boot);
                EditorSceneManager.SaveScene(boot);
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(boot, true); }
            var buildScenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { AssetNames.MenuScene, AssetNames.PlayScene })
            {
                var entry = buildScenes.Find(item => item.path == path);
                if (entry == null) buildScenes.Add(new EditorBuildSettingsScene(path, true));
                else entry.enabled = true;
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("STARTER_PROCEDURES_CONFIGURED: startup settings, nine procedures and two GF business scenes.");
        }

        public static void Validate()
        {
            foreach (string path in new[] { AssetNames.MenuScene, AssetNames.PlayScene, SettingsPath })
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) throw new InvalidOperationException("Missing Procedure asset: " + path);
            Scene boot = SceneManager.GetSceneByPath(StarterProjectBuilder.BootScene);
            bool openedHere = !boot.IsValid() || !boot.isLoaded;
            if (openedHere) boot = EditorSceneManager.OpenScene(StarterProjectBuilder.BootScene, OpenSceneMode.Additive);
            try
            {
                var host = Find<GameBootstrap>(boot);
                var procedure = Find<ProcedureComponent>(boot);
                if (host == null || host.StartupSettings == null || procedure == null)
                    throw new InvalidOperationException("Boot Procedure flow is not configured. Run Starter/7. Configure Procedure Flow.");
                host.StartupSettings.Validate(false);
                var serialized = new SerializedObject(procedure);
                var names = serialized.FindProperty("m_AvailableProcedureTypeNames");
                var registered = new HashSet<string>();
                for (int i = 0; i < names.arraySize; i++)
                    if (!registered.Add(names.GetArrayElementAtIndex(i).stringValue)) throw new InvalidOperationException("Duplicate Procedure registration.");
                foreach (Type type in RequiredTypes)
                    if (!registered.Contains(type.FullName)) throw new InvalidOperationException("Procedure is not registered: " + type.FullName);
                if (serialized.FindProperty("m_EntranceProcedureTypeName").stringValue != typeof(ProcedureBoot).FullName)
                    throw new InvalidOperationException("This demo expects ProcedureBoot as its entrance. Review custom entry wiring before using the demo validator.");
                foreach (GameObject root in boot.GetRootGameObjects())
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                            throw new InvalidOperationException("Missing script in Boot: " + child.name);
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(boot, true); }
            var collection = new ResourceCollection();
            if (!collection.Load() || !collection.HasAsset(AssetDatabase.AssetPathToGUID(AssetNames.MenuScene)) ||
                !collection.HasAsset(AssetDatabase.AssetPathToGUID(AssetNames.PlayScene)))
                throw new InvalidOperationException("Business scenes are missing from the GF resource collection.");
        }

        private static void EnsureBusinessScene(string path, string rootName)
        {
            if (File.Exists(path)) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject(rootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                EditorSceneManager.SaveScene(scene, path);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void EnsureSceneResources()
        {
            var collection = new ResourceCollection();
            if (!collection.Load()) throw new InvalidOperationException("Cannot load GF resource collection.");
            bool changed = false;
            string[] scenes = { AssetNames.MenuScene, AssetNames.PlayScene };
            string[] resources = { "demo/scenes/menu", "demo/scenes/play" };
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!collection.HasResource(resources[i], null))
                {
                    if (!collection.AddResource(resources[i], null, null, LoadType.LoadFromFile, true))
                        throw new InvalidOperationException("Cannot add scene resource: " + resources[i]);
                    changed = true;
                }
                string guid = AssetDatabase.AssetPathToGUID(scenes[i]);
                if (collection.HasAsset(guid)) continue;
                if (!collection.AssignAsset(guid, resources[i], null)) throw new InvalidOperationException("Cannot assign scene: " + scenes[i]);
                changed = true;
            }
            if (changed && !collection.Save()) throw new InvalidOperationException("Cannot save GF resource collection.");
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null) return result;
            }
            return null;
        }
    }
}
