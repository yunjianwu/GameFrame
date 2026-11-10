using System;
using System.IO;
using Starter.Game;
using Starter.Extensions.Samples.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityGameFramework.Editor.ResourceTools;
using UnityGameFramework.Runtime;
using GfPlatform = UnityGameFramework.Editor.ResourceTools.Platform;

namespace Starter.Editor
{
    /// <summary>每次使用新输出目录；资源在构建时加入 StreamingAssets，不把编译产物放进 Assets。</summary>
    public static class StarterBuildTools
    {
        public const string DefaultOutputRoot = @"E:\AI\_DownLoads\unity-gf-demos\build\packages";
        internal static string StreamingAssetsForBuild { get; private set; }

        [MenuItem("Starter/3. Build Package for Active Target")]
        public static void BuildPackageForActiveTarget()
        {
            string run = CreateRunDirectory();
            string package = BuildPackage(EditorUserBuildSettings.activeBuildTarget, run);
            Debug.Log("STARTER_PACKAGE_BUILT: " + package);
        }

        [MenuItem("Starter/4. Build Windows Demo Player")]
        public static void BuildWindowsPlayer()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("Switch Build Settings to Windows x86_64 first, or use -buildTarget Win64 in batch mode.");
            StarterProjectBuilder.ValidateDemo();
            ExtensionsSampleBuilder.ValidateExtensions();
            AlgorithmDemoProjectBuilder.Validate();
            if (Directory.Exists("Assets/StreamingAssets"))
                throw new InvalidOperationException("This demo build expects no project StreamingAssets folder. Review the additional asset paths before combining them.");

            string run = CreateRunDirectory();
            string package = BuildPackage(BuildTarget.StandaloneWindows64, run);
            string player = Path.Combine(run, "player", "UnityGFDemos.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(player));
            try
            {
                StreamingAssetsForBuild = package;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { StarterProjectBuilder.BootScene, ExtensionsSampleBuilder.GalleryScene },
                    locationPathName = player,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Player build failed: " + report.summary.result);
                Debug.Log("STARTER_PLAYER_BUILT: " + player);
            }
            finally { StreamingAssetsForBuild = null; }
        }

        private static string CreateRunDirectory()
        {
            string root = Environment.GetEnvironmentVariable("STARTER_BUILD_ROOT");
            if (string.IsNullOrWhiteSpace(root)) root = DefaultOutputRoot;
            root = Path.GetFullPath(root);
            if (root.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(root, Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Build outputs must stay outside Assets.");
            string run = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            if (Directory.Exists(run) || File.Exists(run)) throw new IOException("Output already exists: " + run);
            Directory.CreateDirectory(run);
            return run;
        }

        private static string BuildPackage(BuildTarget target, string run)
        {
            StarterProjectBuilder.ValidateDemo();
            GfPlatform platform;
            switch (target)
            {
                case BuildTarget.StandaloneWindows64: platform = GfPlatform.Windows64; break;
                case BuildTarget.Android: platform = GfPlatform.Android; break;
                case BuildTarget.WebGL: platform = GfPlatform.WebGL; break;
                default: throw new NotSupportedException("Add an explicit GF platform mapping for " + target);
            }

            string resources = Path.Combine(run, "resources");
            if (Directory.Exists(resources)) throw new IOException("Resource output must be new: " + resources);
            Directory.CreateDirectory(resources);
            var builder = new ResourceBuilderController
            {
                OutputDirectory = resources,
                Platforms = platform,
                InternalResourceVersion = 1,
                AssetBundleCompression = AssetBundleCompressionType.LZ4,
                CompressionHelperTypeName = typeof(DefaultCompressionHelper).FullName,
                OutputPackageSelected = true,
                OutputFullSelected = false,
                OutputPackedSelected = false
            };
            // 上游构建器会清理旧输出。使用全新目录，并在调用前检查所有清理目标均不存在。
            foreach (string path in new[] { builder.WorkingPath, builder.OutputPackagePath, builder.OutputFullPath, builder.OutputPackedPath, builder.BuildReportPath })
                if (Directory.Exists(path) || File.Exists(path)) throw new IOException("Refusing to reuse GF output: " + path);
            if (!builder.RefreshCompressionHelper()) throw new InvalidOperationException("Cannot initialize compression helper.");
            bool completed = false;
            builder.ProcessResourceComplete += builtPlatform => { if (builtPlatform == platform) completed = true; };
            builder.BuildResourceError += error => Debug.LogError(error);
            string package = Path.Combine(builder.OutputPackagePath, platform.ToString());
            if (!builder.BuildResources() || !completed || !File.Exists(Path.Combine(package, "GameFrameworkVersion.dat")))
                throw new InvalidOperationException("Package build failed. Inspect the GF build report under " + resources);
            foreach (string name in new[] { "demo/config.dat", "demo/ui.dat", "demo/scenes/menu.dat", "demo/scenes/play.dat", "algorithms/scenes/boids.dat", "algorithms/scenes/flow-field.dat", "algorithms/scenes/quadtree.dat", "algorithms/scenes/navmesh.dat" })
                if (!File.Exists(Path.Combine(package, name))) throw new IOException("Missing package resource: " + name);
            return package;
        }
    }

    public sealed class StarterStreamingAssetsProcessor : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            string package = StarterBuildTools.StreamingAssetsForBuild;
            if (string.IsNullOrEmpty(package)) return;
            // pathInStreamingAssets 为空字符串时，加入的目录内容位于 StreamingAssets 根目录。
            context.AddAdditionalPathToStreamingAssets(package, string.Empty);
        }
    }
}
