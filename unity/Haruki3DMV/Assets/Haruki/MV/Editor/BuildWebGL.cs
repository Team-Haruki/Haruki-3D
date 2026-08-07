using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;
using Sekai.Core;

namespace Haruki.MV.Editor
{
    public static class BuildWebGL
    {
        private const string BootstrapScenePath = "Assets/Haruki/MV/Generated/Bootstrap.unity";
        private const string RecoveredAssetRoot = "Assets/Haruki/MV/Generated/MvBuild";
        private const string BuildName = "HarukiMV";

        [Serializable]
        private sealed class MvSourceSetBuildManifest
        {
            public int music_id;
            public string asset_version;
            public string asset_hash;
            public MvSourceSetBuildEntry[] bundles = Array.Empty<MvSourceSetBuildEntry>();
        }

        [Serializable]
        private sealed class MvSourceSetBuildEntry
        {
            public string name;
            public string kind;
            public string[] dependencies = Array.Empty<string>();
        }

        public static void PerformBuild()
        {
            CreateBootstrapScene();

            PlayerSettings.companyName = "Team Haruki";
            PlayerSettings.productName = "Haruki 3DMV";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            // Dynamic MV bundles may reference engine component types absent from the bootstrap scene.
            PlayerSettings.stripEngineCode = false;

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                throw new InvalidOperationException("Unity WebGL Build Support is unavailable.");
            }

            var outputPath = Environment.GetEnvironmentVariable("HARUKI_MV_BUILD_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", BuildName));
            }

            Directory.CreateDirectory(outputPath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Unity WebGL build failed: {report.summary.result}");
            }

            ExposeBridgeInstance(outputPath);
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HARUKI_MV_RECOVERED_PROJECT")))
            {
                BuildRecoveredBundles(outputPath);
            }
            Debug.Log($"Haruki 3DMV WebGL build written to {outputPath}");
        }

        private static void BuildRecoveredBundles(string playerOutputPath)
        {
            var recoveredProject = Environment.GetEnvironmentVariable("HARUKI_MV_RECOVERED_PROJECT");
            var recoveredAssets = Path.Combine(recoveredProject, "Assets");
            if (!Directory.Exists(recoveredAssets))
            {
                recoveredAssets = Path.Combine(recoveredProject, "ExportedProject", "Assets");
            }
            if (!Directory.Exists(recoveredAssets))
            {
                throw new DirectoryNotFoundException(
                    $"HARUKI_MV_RECOVERED_PROJECT has no Unity Assets directory: {recoveredProject}");
            }

            var sourceSetPath = Environment.GetEnvironmentVariable("HARUKI_MV_SOURCE_SET_MANIFEST");
            if (string.IsNullOrWhiteSpace(sourceSetPath) || !File.Exists(sourceSetPath))
            {
                throw new FileNotFoundException(
                    "HARUKI_MV_SOURCE_SET_MANIFEST must point to mv-source-set.json.",
                    sourceSetPath);
            }
            var sourceSet = JsonUtility.FromJson<MvSourceSetBuildManifest>(File.ReadAllText(sourceSetPath));
            var stages = sourceSet?.bundles?
                .Where(entry => entry != null &&
                    (entry.kind == "stage" || entry.name?.StartsWith("live_pv/model/stage/") == true))
                .ToArray() ?? Array.Empty<MvSourceSetBuildEntry>();
            var mvDataEntries = sourceSet?.bundles?
                .Where(entry => entry != null &&
                    (entry.kind == "mv_data" || entry.name?.StartsWith("live_pv/mv_data/") == true))
                .ToArray() ?? Array.Empty<MvSourceSetBuildEntry>();
            var timelines = sourceSet?.bundles?
                .Where(entry => entry != null &&
                    (entry.kind?.StartsWith("timeline_", StringComparison.Ordinal) == true
                        || entry.name?.StartsWith("live_pv/timeline/", StringComparison.Ordinal) == true))
                .ToArray() ?? Array.Empty<MvSourceSetBuildEntry>();
            if (stages.Length == 0)
            {
                throw new InvalidOperationException("MV source set has no stage bundle.");
            }
            if (mvDataEntries.Length == 0)
            {
                throw new InvalidOperationException("MV source set has no MusicVideoData bundle.");
            }
            if (timelines.Length == 0)
            {
                throw new InvalidOperationException("MV source set has no Timeline bundle.");
            }
            var buildEntries = sourceSet?.bundles?
                .Where(entry => entry != null)
                .ToArray() ?? Array.Empty<MvSourceSetBuildEntry>();

            AssetDatabase.DeleteAsset(RecoveredAssetRoot);
            try
            {
                foreach (var entry in buildEntries)
                {
                    ValidateLogicalBundleName(entry.name);
                    CopyRecoveredGroup(
                        recoveredAssets,
                        "AssetBundles/" + entry.name,
                        RecoveredAssetRoot + "/" + entry.name);
                }
                RemapRecoveredTimelineScriptGuids(recoveredAssets, RecoveredAssetRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var bundleBuilds = new List<AssetBundleBuild>(buildEntries.Length);
                foreach (var entry in buildEntries)
                {
                    var sourceDirectory = RecoveredAssetRoot + "/" + entry.name;
                    var assetNames = AssetDatabase.FindAssets(string.Empty, new[] { sourceDirectory })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Where(path => !AssetDatabase.IsValidFolder(path))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();
                    if (assetNames.Length == 0)
                    {
                        throw new FileNotFoundException(
                            $"Recovered asset group was empty for '{entry.name}'.",
                            sourceDirectory);
                    }

                    string[] addressableNames = null;
                    if (entry.kind == "stage")
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "stage.prefab",
                            entry.name,
                            "stage prefab");
                        addressableNames = new[] { "stage" };
                    }
                    else if (entry.kind == "mv_data")
                    {
                        assetNames = RequireSingleAsset<MusicVideoData>(
                            sourceDirectory,
                            "data.asset",
                            entry.name,
                            "MusicVideoData");
                        addressableNames = new[] { "data" };
                    }
                    else if (entry.kind?.StartsWith("timeline_", StringComparison.Ordinal) == true)
                    {
                        assetNames = RequireSingleAsset<TimelineAsset>(
                            sourceDirectory,
                            "timeline.playable",
                            entry.name,
                            "TimelineAsset");
                        addressableNames = new[] { "timeline" };
                    }
                    else if (entry.kind == "stage_decoration" || entry.kind == "camera_decoration")
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "decoration.prefab",
                            entry.name,
                            "decoration prefab");
                        addressableNames = new[] { "decoration" };
                    }
                    else if (entry.kind == "penlight")
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "penlight.prefab",
                            entry.name,
                            "penlight prefab");
                        addressableNames = new[] { "penlight" };
                    }
                    else if (entry.kind == "character_body")
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "body.prefab",
                            entry.name,
                            "character body prefab");
                        addressableNames = new[] { "body" };
                    }
                    else if (entry.kind == "character_face" &&
                        !entry.name.EndsWith("/common", StringComparison.Ordinal))
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "face.prefab",
                            entry.name,
                            "character face prefab");
                        addressableNames = new[] { "face" };
                    }

                    bundleBuilds.Add(new AssetBundleBuild
                    {
                        assetBundleName = entry.name,
                        assetNames = assetNames,
                        addressableNames = addressableNames
                    });
                }

                var bundleOutput = Path.Combine(
                    playerOutputPath,
                    "StreamingAssets",
                    "sekai_webgl_bundles");
                if (Directory.Exists(bundleOutput))
                {
                    Directory.Delete(bundleOutput, true);
                }
                foreach (var entry in buildEntries)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(bundleOutput, entry.name)));
                }
                var manifest = BuildPipeline.BuildAssetBundles(
                    bundleOutput,
                    bundleBuilds.ToArray(),
                    BuildAssetBundleOptions.ChunkBasedCompression |
                    BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    BuildTarget.WebGL);
                if (manifest == null || buildEntries.Any(entry =>
                    !File.Exists(Path.Combine(bundleOutput, entry.name))))
                {
                    throw new InvalidOperationException("One or more MV WebGL bundles were not produced.");
                }

                File.WriteAllText(
                    Path.Combine(bundleOutput, "deps.json"),
                    JsonUtility.ToJson(new MvBundleSetManifest
                    {
                        musicId = sourceSet.music_id,
                        assetVersion = sourceSet.asset_version,
                        assetHash = sourceSet.asset_hash,
                        requested = buildEntries.Select(entry => entry.name).ToArray(),
                        entries = buildEntries.Select(entry =>
                            new MvBundleSetEntry
                            {
                                name = entry.name,
                                deps = entry.dependencies ?? Array.Empty<string>()
                            }).ToArray()
                    }, true));
                Debug.Log($"MV {sourceSet.music_id} WebGL bundle(s) written to {bundleOutput}");
            }
            finally
            {
                AssetDatabase.DeleteAsset(RecoveredAssetRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static string[] RequireSingleAsset<T>(
            string sourceDirectory,
            string fileName,
            string bundleName,
            string description)
            where T : UnityEngine.Object
        {
            var path = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { sourceDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .SingleOrDefault(candidate =>
                    string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase));
            if (path == null || AssetDatabase.LoadAssetAtPath<T>(path) == null)
            {
                throw new FileNotFoundException(
                    $"Recovered {description} was not readable for '{bundleName}'.",
                    sourceDirectory);
            }
            return new[] { path };
        }

        private static void ValidateLogicalBundleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                name.StartsWith("/", StringComparison.Ordinal) ||
                name.Split('/').Any(segment => segment is "" or "." or ".."))
            {
                throw new InvalidOperationException($"Invalid MV bundle name '{name}'.");
            }
        }

        private static void CopyRecoveredGroup(string recoveredAssets, string relativeSource, string destination)
        {
            var source = Path.Combine(recoveredAssets, relativeSource.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException($"Recovered Unity asset group is missing: {source}");
            }
            var destinationParent = Path.GetDirectoryName(Path.GetFullPath(destination));
            if (!string.IsNullOrEmpty(destinationParent))
            {
                Directory.CreateDirectory(destinationParent);
            }
            FileUtil.CopyFileOrDirectory(source, destination);
        }

        private static void RemapRecoveredTimelineScriptGuids(
            string recoveredAssets,
            string importedRoot)
        {
            var recoveredScripts = Path.Combine(recoveredAssets, "Scripts", "Unity.Timeline");
            if (!Directory.Exists(recoveredScripts))
            {
                throw new DirectoryNotFoundException(
                    $"Recovered Unity.Timeline script metadata is missing: {recoveredScripts}");
            }

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TimelineAsset).Assembly)
                ?? throw new InvalidOperationException("Unity Timeline package was not resolved.");
            var packageMetas = Directory.GetFiles(package.resolvedPath, "*.cs.meta", SearchOption.AllDirectories)
                .GroupBy(Path.GetFileName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var recoveredMeta in Directory.GetFiles(
                recoveredScripts,
                "*.cs.meta",
                SearchOption.AllDirectories))
            {
                if (!packageMetas.TryGetValue(Path.GetFileName(recoveredMeta), out var candidates)
                    || candidates.Length != 1)
                {
                    continue;
                }
                replacements.Add(ReadMetaGuid(recoveredMeta), ReadMetaGuid(candidates[0]));
            }

            foreach (var path in Directory.GetFiles(
                importedRoot,
                "timeline.playable",
                SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path);
                foreach (var replacement in replacements)
                {
                    text = text.Replace(replacement.Key, replacement.Value);
                }
                File.WriteAllText(path, text);
            }
        }

        private static string ReadMetaGuid(string path)
        {
            const string prefix = "guid: ";
            var line = File.ReadLines(path)
                .First(value => value.StartsWith(prefix, StringComparison.Ordinal));
            return line.Substring(prefix.Length).Trim();
        }

        private static void ExposeBridgeInstance(string outputPath)
        {
            var indexPath = Path.Combine(outputPath, "index.html");
            var html = File.ReadAllText(indexPath);
            const string marker = ".then((unityInstance) => {";
            if (!html.Contains(marker))
            {
                throw new InvalidOperationException("Unity WebGL template no longer exposes its instance callback.");
            }
            File.WriteAllText(
                indexPath,
                html.Replace(marker, marker + "\n                window.harukiMvUnityInstance = unityInstance;")
            );
        }

        private static void CreateBootstrapScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BootstrapScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bridge = new GameObject(HarukiMvBridge.ObjectName);
            bridge.AddComponent<MvPlaybackCoordinator>();
            bridge.AddComponent<MvSceneBundleLoader>();
            bridge.AddComponent<MvBundleSetLoader>();
            bridge.AddComponent<MvPlayerAssembler>();
            bridge.AddComponent<HarukiMvBridge>();
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }
    }
}
