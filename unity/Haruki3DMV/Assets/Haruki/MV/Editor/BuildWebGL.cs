using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Timeline;
using Sekai.Core;

namespace Haruki.MV.Editor
{
    public static class BuildWebGL
    {
        private const string BootstrapScenePath = "Assets/Haruki/MV/Generated/Bootstrap.unity";
        private const string RecoveredAssetRoot = "Assets/Haruki/MV/Generated/MvBuild";
        private const string BuildName = "HarukiMV";
        private const string AllowDummyShadersEnvironmentVariable =
            "HARUKI_MV_ALLOW_DUMMY_SHADERS";

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
            MvRenderPipelineAssetBuilder.BuildAndAssign();
            PreserveProjectShaders();
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

        private static void PreserveProjectShaders()
        {
            var projectShaders = AssetDatabase
                .FindAssets("t:Shader", new[] { "Assets/Haruki/MV/Shaders" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Shader>)
                .Where(shader => shader != null)
                .ToArray();
            var graphicsSettingsAsset =
                Unsupported.GetSerializedAssetInterfaceSingleton("GraphicsSettings");
            var graphicsSettings = new SerializedObject(graphicsSettingsAsset);
            var alwaysIncluded = graphicsSettings.FindProperty("m_AlwaysIncludedShaders") ??
                throw new InvalidOperationException(
                    "GraphicsSettings does not expose m_AlwaysIncludedShaders.");
            var existingShaders = Enumerable.Range(0, alwaysIncluded.arraySize)
                .Select(index => alwaysIncluded.GetArrayElementAtIndex(index).objectReferenceValue)
                .OfType<Shader>();
            var preservedShaders = existingShaders
                .Where(shader => shader != null)
                .Concat(projectShaders)
                .GroupBy(shader => shader.name, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(shader => shader.name, StringComparer.Ordinal)
                .ToArray();
            alwaysIncluded.arraySize = preservedShaders.Length;
            for (var index = 0; index < preservedShaders.Length; index++)
            {
                alwaysIncluded
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue = preservedShaders[index];
            }
            graphicsSettings.ApplyModifiedPropertiesWithoutUndo();
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
            if (buildEntries.Any(entry => entry.name?.StartsWith(
                    "live_pv/model/mesh_flare_para/textures/",
                    StringComparison.Ordinal) == true) &&
                !buildEntries.Any(entry => string.Equals(
                    entry.name,
                    "live_pv/model/mesh_flare_para/common",
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "MV source set includes MeshFlarePara textures but omits the official " +
                    "live_pv/model/mesh_flare_para/common controller bundle.");
            }

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
                RemapRecoveredShaderGuids(RecoveredAssetRoot);
                RemapRecoveredScriptGuids(recoveredAssets, RecoveredAssetRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidateRecoveredRuntimePrerequisites(recoveredAssets, RecoveredAssetRoot);

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
                    else if (entry.kind == "height_fog")
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "height_fog.prefab",
                            entry.name,
                            "height fog prefab");
                        addressableNames = new[] { "height_fog" };
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
                    else if (entry.kind == "character_head_optional")
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "optional.prefab",
                            entry.name,
                            "character head-optional prefab");
                        addressableNames = new[] { "head_optional" };
                    }
                    else if (entry.kind == "music_item")
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "item.prefab",
                            entry.name,
                            "music-item prefab");
                        addressableNames = new[] { "item" };
                    }
                    else if (string.Equals(
                        entry.name,
                        MvOfficialRuntimeData.MeshFlareControllerBundleName,
                        StringComparison.Ordinal))
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "MeshFlarePara.prefab",
                            entry.name,
                            "MeshFlarePara controller prefab");
                        addressableNames = new[] { "mesh_flare_para" };
                    }
                    else if (string.Equals(
                        entry.name,
                        MvOfficialRuntimeData.WaterCausticsBundleName,
                        StringComparison.Ordinal))
                    {
                        assetNames = RequireSingleAsset<GameObject>(
                            sourceDirectory,
                            "WaterCausticsProjector.prefab",
                            entry.name,
                            "WaterCaustics projector prefab");
                        addressableNames = new[] { "water_caustics" };
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

        private static void RemapRecoveredShaderGuids(string importedRoot)
        {
            const string replacementRoot = "Assets/Haruki/MV/Shaders";
            var replacementShaders = AssetDatabase.FindAssets("t:Shader", new[] { replacementRoot })
                .Select(guid => new
                {
                    Guid = guid,
                    Shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid))
                })
                .Where(entry => entry.Shader != null)
                .GroupBy(entry => entry.Shader.name, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single().Guid,
                    StringComparer.Ordinal);
            var recoveredStageAliases = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Sekai/Live/Stage/ColorMap"] = "Haruki/MV/RecoveredStageColorMap",
                ["Sekai/Live/Stage/Texture"] = "Haruki/MV/RecoveredStageTexture",
                ["Sekai/Live/Stage/LightMap"] = "Haruki/MV/RecoveredStageLightMap",
                ["Sekai/Live/Stage/LightMap-Transparent"] = "Haruki/MV/RecoveredStageLightMapTransparent",
                ["Sekai/Live/Stage/LightMap-Cutout"] = "Haruki/MV/RecoveredStageLightMapCutout",
                ["Sekai/Live/Stage/LightMap-Emission"] = "Haruki/MV/RecoveredStageLightMapEmission",
                ["Sekai/Live/Stage/LightMap-Reflection"] = "Haruki/MV/RecoveredStageOpaque",
                ["Sekai/Live/Brightness/LightMap"] = "Haruki/MV/RecoveredStageOpaque",
                ["Sekai/Live/Brightness/FixedPenlight"] = "Haruki/MV/RecoveredStageOpaque",
                ["Sekai/Live/CameraProp/Default"] = "Haruki/MV/RecoveredStageOpaque",
                ["Sekai/Live/Default/Transparent"] = "Haruki/MV/RecoveredStageTransparent",
                ["Sekai/Live/Default/Soft-Transparent"] = "Haruki/MV/RecoveredStageTransparent",
                ["Sekai/Live/Brightness/Transparent"] = "Haruki/MV/RecoveredStageTransparent",
                ["Sekai/Live/Brightness/Billboard-Transparent"] = "Haruki/MV/RecoveredStageTransparent",
                ["Sekai/Live/Particle/Uber-Transparent"] = "Haruki/MV/RecoveredStageTransparent",
                ["Sekai/Live/Default/ColorMap-Add"] = "Haruki/MV/RecoveredStageAdditive",
                ["Sekai/Live/Brightness/ColorMap-Add"] = "Haruki/MV/RecoveredStageAdditive",
                ["Sekai/Live/Brightness/Add"] = "Haruki/MV/RecoveredStageAdditive",
                ["Sekai/Live/Brightness/Monitor"] = "Haruki/MV/RecoveredStageMonitor",
                ["Sekai/Live/MusicItem/Toon"] = "Sekai/Live/MusicItem/Toon",
                ["Hidden/Sekai/Live/MusicItem/Toon"] = "Hidden/Sekai/Live/MusicItem/Toon",
            };
            foreach (var alias in recoveredStageAliases)
            {
                if (!replacementShaders.TryGetValue(alias.Value, out var replacementGuid))
                {
                    throw new InvalidOperationException(
                        $"Recovered stage shader '{alias.Value}' is missing.");
                }
                replacementShaders[alias.Key] = replacementGuid;
            }
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var urpLitPath = urpLit == null ? string.Empty : AssetDatabase.GetAssetPath(urpLit);
            var urpLitGuid = string.IsNullOrEmpty(urpLitPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(urpLitPath);
            if (!string.IsNullOrEmpty(urpLitGuid))
            {
                replacementShaders["Universal Render Pipeline/Lit"] = urpLitGuid;
            }
            var shaderNamePattern = new Regex(
                @"^\s*Shader\s+""([^""]+)""",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            var replacedShaderPaths = new List<string>();

            foreach (var shaderPath in Directory.GetFiles(
                importedRoot,
                "*.shader",
                SearchOption.AllDirectories))
            {
                var shaderText = File.ReadAllText(shaderPath);
                if (!shaderText.Contains("DummyShaderTextExporter"))
                {
                    continue;
                }
                var shaderNameMatch = shaderNamePattern.Match(shaderText);
                if (!shaderNameMatch.Success ||
                    !replacementShaders.TryGetValue(shaderNameMatch.Groups[1].Value, out var replacementGuid))
                {
                    continue;
                }
                var metaPath = shaderPath + ".meta";
                if (!File.Exists(metaPath))
                {
                    throw new FileNotFoundException(
                        $"Recovered shader metadata is missing for '{shaderNameMatch.Groups[1].Value}'.",
                        metaPath);
                }
                replacements.Add(ReadMetaGuid(metaPath), replacementGuid);
                replacedShaderPaths.Add(shaderPath);
            }

            if (replacements.Count == 0)
            {
                return;
            }
            var serializedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".anim",
                ".asset",
                ".controller",
                ".mat",
                ".playable",
                ".prefab",
                ".unity",
            };
            var serializedPaths = Directory.GetFiles(importedRoot, "*", SearchOption.AllDirectories)
                .Where(path => serializedExtensions.Contains(Path.GetExtension(path)))
                .ToArray();
            foreach (var path in serializedPaths)
            {
                var text = File.ReadAllText(path);
                foreach (var replacement in replacements)
                {
                    text = text.Replace(replacement.Key, replacement.Value);
                }
                File.WriteAllText(path, text);
            }
            foreach (var replacement in replacements)
            {
                var unresolved = serializedPaths.FirstOrDefault(path =>
                    File.ReadAllText(path).Contains(replacement.Key));
                if (unresolved != null)
                {
                    throw new InvalidOperationException(
                        $"Recovered shader GUID {replacement.Key} remained in {unresolved}.");
                }
            }
            foreach (var shaderPath in replacedShaderPaths)
            {
                File.Delete(shaderPath);
                File.Delete(shaderPath + ".meta");
            }
            Debug.Log(
                $"Replaced {replacements.Count} recovered placeholder shader GUID(s) with " +
                "evidence-backed project shaders.");
        }

        private static void RemapRecoveredScriptGuids(
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

            var customTimelineScripts = new[]
            {
                "DirectionalBlurClip",
                "DirectionalBlurTrack",
                "CutInClip",
                "CutInTrack",
                "FadeOutClip",
                "FadeOutTrack",
                "LegacyBloomClip",
                "LegacyBloomTrack",
                "LightOverlayClip",
                "LightOverlayTrack",
                "LiveEffectClip",
                "LiveEffectTrack",
                "LutClip",
                "LutTrack",
                "MeshFlareParaClip",
                "MeshFlareParaTrack",
                "SaturationClip",
                "SaturationTrack",
                "SaturationBlurClip",
                "SaturationBlurTrack",
                "SekaiDofClip",
                "SekaiDofTrack",
                "VignetteClip",
                "VignetteTrack",
                "WaterEyeClip",
                "WaterEyeTrack",
            };
            var recoveredGameScripts = Path.Combine(
                recoveredAssets,
                "Scripts",
                "Assembly-CSharp",
                "Sekai",
                "Core",
                "Live");
            foreach (var scriptName in customTimelineScripts)
            {
                var recoveredMeta = Path.Combine(
                    scriptName.StartsWith("LiveEffect", StringComparison.Ordinal)
                        ? Path.Combine(recoveredAssets, "Scripts", "Assembly-CSharp")
                        : recoveredGameScripts,
                    scriptName + ".cs.meta");
                if (!File.Exists(recoveredMeta))
                {
                    throw new FileNotFoundException(
                        $"Recovered script metadata is missing for {scriptName}.",
                        recoveredMeta);
                }
                var projectScript = AssetDatabase.FindAssets(
                        $"{scriptName} t:MonoScript",
                        new[] { "Assets/Haruki/MV/Runtime" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .SingleOrDefault(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        scriptName,
                        StringComparison.Ordinal));
                if (projectScript == null)
                {
                    throw new FileNotFoundException(
                        $"Haruki runtime script is missing for {scriptName}.");
                }
                replacements.Add(
                    ReadMetaGuid(recoveredMeta),
                    AssetDatabase.AssetPathToGUID(projectScript));
            }

            var explicitRecoveredComponentScripts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SekaiSpringBone"] = "Scripts/Assembly-CSharp/Sekai/SekaiSpringBone.cs.meta",
                ["SpringCapsuleCollider"] =
                    "Scripts/ThirdParty.SpringBone/UTJ/SpringCapsuleCollider.cs.meta",
                ["SpringManager"] = "Scripts/ThirdParty.SpringBone/UTJ/SpringManager.cs.meta",
                ["SpringSphereCollider"] =
                    "Scripts/ThirdParty.SpringBone/UTJ/SpringSphereCollider.cs.meta",
            };
            foreach (var entry in explicitRecoveredComponentScripts)
            {
                var recoveredMeta = Path.Combine(
                    recoveredAssets,
                    entry.Value.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(recoveredMeta))
                {
                    throw new FileNotFoundException(
                        $"Recovered script metadata is missing for {entry.Key}.",
                        recoveredMeta);
                }
                replacements.Add(
                    ReadMetaGuid(recoveredMeta),
                    FindRuntimeScriptGuid(entry.Key));
            }

            var recoveredComponentScripts = new[]
            {
                "CharacterAdjuster",
                "CharacterAccessoryTransformController",
                "CharacterAccessoryTransformData",
                "ChromaticAberrationClip",
                "ChromaticAberrationTrack",
                "EnablePostEffectToCameraDecorationTrack",
                "ExtraBone",
                "HeightFogClip",
                "HeightFogController",
                "HeightFogTrack",
                "IncidentLightClip",
                "IncidentLightTrack",
                "LegacyDofClip",
                "LegacyDofTrack",
                "LiveMonitor",
                "LookAtAxis",
                "MeshFlareParaController",
                "MeshFlareParaTexData",
                "MusicItemModel",
                "MusicItemOpacityClip",
                "MusicItemOpacityTrack",
                "MusicItemUvScrollClip",
                "MusicItemUvScrollTrack",
                "PenlightAnimationKey",
                "PenlightColor",
                "PenlightKey",
                "PenlightParameter",
                "SekaiAmbientLight",
                "SekaiCharacterAmbientLight",
                "SekaiCharacterHair",
                "SekaiCharacterEye",
                "SekaiCharacterOutlineFeature",
                "SekaiCharacterRimLight",
                "SekaiDirectionalLight",
                "SekaiGlobalSettings",
                "SekaiGlobalFlipBookProjector",
                "ScreenDistortionClip",
                "ScreenDistortionTrack",
                "ShaderProperty",
                "SolarizationClip",
                "SolarizationTrack",
                "WaterSurfaceClip",
                "WaterSurfaceController",
                "WaterSurfaceTrack",
                "WaterEyePreset",
                "WaterEyePresetTable",
            };
            // AssetRipper preserves scripts from every source assembly under Assets/Scripts.
            // Runtime components such as SekaiCharacterHair/Eye and ExtraBone live in
            // Unity.RenderPipelines.Universal.Runtime rather than Assembly-CSharp, so
            // restricting this search to Assembly-CSharp leaves their serialized GUIDs
            // pointing at dummy scripts.
            var recoveredScriptsRoot = Path.Combine(recoveredAssets, "Scripts");
            foreach (var scriptName in recoveredComponentScripts)
            {
                var recoveredMetas = Directory.GetFiles(
                    recoveredScriptsRoot,
                    scriptName + ".cs.meta",
                    SearchOption.AllDirectories);
                if (recoveredMetas.Length == 0)
                {
                    continue;
                }
                if (recoveredMetas.Length != 1)
                {
                    throw new FileNotFoundException(
                        $"Recovered component script metadata is ambiguous or missing for {scriptName}.",
                        recoveredScriptsRoot);
                }
                var projectScript = AssetDatabase.FindAssets(
                        $"{scriptName} t:MonoScript",
                        new[] { "Assets/Haruki/MV/Runtime" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .SingleOrDefault(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        scriptName,
                        StringComparison.Ordinal));
                if (projectScript == null)
                {
                    throw new FileNotFoundException(
                        $"Haruki runtime component script is missing for {scriptName}.");
                }
                replacements.Add(
                    ReadMetaGuid(recoveredMetas[0]),
                    AssetDatabase.AssetPathToGUID(projectScript));
            }

            var animationPropertyNames = new Dictionary<uint, string>
            {
                [0xBC8CF78A] = "ambientColor.r",
                [0xD1511361] = "ambientColor.g",
                [0xA13BE7EE] = "ambientColor.b",
                [0x3832B654] = "ambientColor.a",
                [0xE9B2B892] = "intensity",
                [0x0B5F698D] = "glowIntensity",
                [0x1BB27D40] = "shadowColor.r",
                [0x766F99AB] = "shadowColor.g",
                [0x06056D24] = "shadowColor.b",
                [0x9F0C3C9E] = "shadowColor.a",
                [0xDF7C3605] = "shadowThreshold",
                [0xFBA7B536] = "rimColor.r",
                [0x967A51DD] = "rimColor.g",
                [0xE610A552] = "rimColor.b",
                [0x7F19F4E8] = "rimColor.a",
                [0x93875A49] = "range",
                [0xF0225CF4] = "emission",
                [0x9B01F251] = "lightInfluence",
                [0x3D7E83F9] = "isUseShadowColor",
                [0x929571D4] = "shadowRimColor.r",
                [0xFF48953F] = "shadowRimColor.g",
                [0x8F2261B0] = "shadowRimColor.b",
                [0x162B300A] = "shadowRimColor.a",
                [0xD22DC43F] = "shadowSharpness",
                [0x3738F1E6] = "fogColor.r",
                [0x5AE5150D] = "fogColor.g",
                [0x2A8FE182] = "fogColor.b",
                [0xB386B038] = "fogColor.a",
                [0x8333F82D] = "fogStart",
                [0x150CCE81] = "fogEnd",
            };

            var serializedPaths = Directory.GetFiles(
                    importedRoot,
                    "*",
                    SearchOption.AllDirectories)
                .Where(path =>
                    path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".playable", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (var path in serializedPaths)
            {
                var text = File.ReadAllText(path);
                foreach (var replacement in replacements)
                {
                    text = text.Replace(replacement.Key, replacement.Value);
                }
                text = Regex.Replace(
                    text,
                    @"script_0x([0-9A-Fa-f]+)",
                    match => uint.TryParse(
                            match.Groups[1].Value,
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var hash)
                        && animationPropertyNames.TryGetValue(hash, out var propertyName)
                            ? propertyName
                            : match.Value);
                text = RemapRecoveredMaterialAnimationProperties(text);
                File.WriteAllText(path, text);
            }

            foreach (var propertyName in animationPropertyNames)
            {
                var unresolved = serializedPaths.FirstOrDefault(path =>
                    Regex.IsMatch(
                        File.ReadAllText(path),
                        $@"script_0x0*{propertyName.Key:X8}(?![0-9A-Fa-f])",
                        RegexOptions.IgnoreCase));
                if (unresolved != null)
                {
                    throw new InvalidOperationException(
                        $"Recovered animation property 0x{propertyName.Key:X8} remained in {unresolved}.");
                }
            }

            foreach (var replacement in replacements)
            {
                var unresolved = serializedPaths.FirstOrDefault(path =>
                    File.ReadAllText(path).Contains(replacement.Key));
                if (unresolved != null)
                {
                    throw new InvalidOperationException(
                        $"Recovered script GUID {replacement.Key} remained in {unresolved}.");
                }
            }
        }

        private static string RemapRecoveredMaterialAnimationProperties(string text)
        {
            var properties = new Dictionary<uint, string>
            {
                [0x0686C589] = "_MainTex_ST",
                [0x0021EAFC] = "_ColorTex_ST",
                [0x0AE5C260] = "_SubTex_ST",
                [0x0DAF8B71] = "_Color",
            };
            const string vectorComponents = "xyzw";
            const string colorComponents = "rgba";
            return Regex.Replace(
                text,
                @"material\.path_0x([0-9A-Fa-f]+)_[A-Za-z]+",
                match =>
                {
                    if (!uint.TryParse(
                            match.Groups[1].Value,
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var encoded))
                    {
                        return match.Value;
                    }

                    var component = (int)(encoded >> 28);
                    var baseHash = encoded & 0x0FFFFFFF;
                    if (!properties.TryGetValue(baseHash, out var propertyName))
                    {
                        return match.Value;
                    }

                    if (component < 4)
                    {
                        return $"material.{propertyName}.{vectorComponents[component]}";
                    }
                    if (component < 8)
                    {
                        return $"material.{propertyName}.{colorComponents[component - 4]}";
                    }
                    return match.Value;
                });
        }

        private static void ValidateRecoveredRuntimePrerequisites(
            string recoveredAssets,
            string importedRoot)
        {
            var unresolved = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var serializedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".anim",
                ".asset",
                ".controller",
                ".playable",
                ".prefab",
                ".unity",
            };
            var scriptReference = new Regex(
                @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*3\}",
                RegexOptions.CultureInvariant);

            foreach (var path in Directory.GetFiles(importedRoot, "*", SearchOption.AllDirectories)
                .Where(path => serializedExtensions.Contains(Path.GetExtension(path))))
            {
                foreach (Match match in scriptReference.Matches(File.ReadAllText(path)))
                {
                    var guid = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                    {
                        continue;
                    }
                    if (!unresolved.TryGetValue(guid, out var paths))
                    {
                        paths = new List<string>();
                        unresolved.Add(guid, paths);
                    }
                    if (paths.Count < 3)
                    {
                        paths.Add(path);
                    }
                }
            }

            var gaps = new List<string>();
            if (unresolved.Count != 0)
            {
                var recoveredScriptNames = Directory.GetFiles(
                        Path.Combine(recoveredAssets, "Scripts"),
                        "*.cs.meta",
                        SearchOption.AllDirectories)
                    .GroupBy(ReadMetaGuid, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => Path.GetFileNameWithoutExtension(
                            Path.GetFileNameWithoutExtension(group.First())),
                        StringComparer.Ordinal);
                var details = unresolved
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry =>
                        $"{(recoveredScriptNames.TryGetValue(entry.Key, out var name) ? name : "unknown script")} " +
                        $"({entry.Key}): {string.Join(", ", entry.Value)}");
                gaps.Add(
                    "unresolved MonoBehaviour scripts (implement and remap their official contracts):\n" +
                    string.Join("\n", details));
            }

            var referenceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".anim",
                ".asset",
                ".controller",
                ".mat",
                ".playable",
                ".prefab",
                ".unity",
            };
            var referencedAssetText = Directory.GetFiles(
                    importedRoot,
                    "*",
                    SearchOption.AllDirectories)
                .Where(path => referenceExtensions.Contains(Path.GetExtension(path)))
                .Select(File.ReadAllText)
                .ToArray();
            var dummyShaders = Directory.GetFiles(importedRoot, "*.shader", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("DummyShaderTextExporter"))
                .Where(path => referencedAssetText.Any(text => text.Contains(ReadMetaGuid(path + ".meta"))))
                .Take(8)
                .ToArray();
            if (dummyShaders.Length != 0)
            {
                if (AllowDummyShadersForDevelopment())
                {
                    Debug.LogWarning(
                        $"{AllowDummyShadersEnvironmentVariable}=1: rebuilding local MV evidence " +
                        "with AssetRipper placeholder shaders. The result is suitable only for " +
                        "runtime/Timeline development and must not be published. Examples:\n" +
                        string.Join("\n", dummyShaders));
                }
                else
                {
                    gaps.Add(
                        "Referenced AssetRipper dummy shaders cannot be rebuilt for WebGL. Supply real " +
                        "WebGL/ShaderLab implementations and remap the recovered shader GUIDs. " +
                        $"For local runtime-only investigation, explicitly set " +
                        $"{AllowDummyShadersEnvironmentVariable}=1. Examples:\n" +
                        string.Join("\n", dummyShaders));
                }
            }

            var recoveredMvData = AssetDatabase
                .FindAssets("t:MusicVideoData", new[] { importedRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MusicVideoData>)
                .Where(data => data != null)
                .ToArray();
            var requiresPlanarReflection = recoveredMvData.Any(data =>
                data.stageInfo?.enablePlanarReflection == true);
            var requiresEffectDistortion = recoveredMvData.Any(data =>
                data.stageInfo?.enableEffectDistortion == true);

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                gaps.Add(
                    "no default Scriptable Render Pipeline asset is assigned. The official 3DMV " +
                    "renderer is URP and its renderer-feature graph must be supplied explicitly.");
            }
            else
            {
                ValidateRecoveredRendererGraph(
                    GraphicsSettings.defaultRenderPipeline,
                    requiresPlanarReflection,
                    requiresEffectDistortion,
                    gaps);
            }

            var missingCameraResources = new[]
                {
                    MvCameraNode.MainCameraResource,
                    MvCameraNode.SubCameraResource,
                }
                .Where(path => Resources.Load<GameObject>(path) == null)
                .ToArray();
            foreach (var resourcePath in missingCameraResources)
            {
                GameObject recovered = null;
                try
                {
                    recovered = MvRecoveredCameraResources.Create(resourcePath);
                }
                catch (Exception exception)
                {
                    gaps.Add(
                        $"camera Resource '{resourcePath}' is missing and its recovered " +
                        $"runtime contract could not be constructed: {exception.Message}");
                }
                finally
                {
                    if (recovered != null)
                    {
                        UnityEngine.Object.DestroyImmediate(recovered);
                    }
                }
            }

            if (gaps.Count != 0)
            {
                throw new InvalidOperationException(
                    "Recovered MV assets are not safe to publish as a WebGL player:\n- " +
                    string.Join("\n- ", gaps));
            }
        }

        private static bool AllowDummyShadersForDevelopment()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable(AllowDummyShadersEnvironmentVariable),
                "1",
                StringComparison.Ordinal);
        }

        private static void ValidateRecoveredRendererGraph(
            RenderPipelineAsset pipelineAsset,
            bool requiresPlanarReflection,
            bool requiresEffectDistortion,
            ICollection<string> gaps)
        {
            var pipeline = new SerializedObject(pipelineAsset);
            var renderScale = pipeline.FindProperty("m_RenderScale");
            if (renderScale == null || !Mathf.Approximately(renderScale.floatValue, 1f))
            {
                gaps.Add(
                    $"the assigned URP asset must use the captured render scale 1.0; found " +
                    $"{(renderScale == null ? "no m_RenderScale" : renderScale.floatValue.ToString())}.");
            }
            var msaa = pipeline.FindProperty("m_MSAA");
            if (msaa == null || msaa.intValue != 1)
            {
                gaps.Add(
                    $"the assigned URP asset must disable MSAA (captured sample count 1); found " +
                    $"{(msaa == null ? "no m_MSAA" : msaa.intValue.ToString())}.");
            }
            var rendererDataList = pipeline.FindProperty("m_RendererDataList");
            if (rendererDataList == null || !rendererDataList.isArray)
            {
                gaps.Add(
                    $"the assigned render pipeline '{pipelineAsset.name}' does not expose the " +
                    "URP m_RendererDataList required by the captured 3DMV camera contract.");
                return;
            }

            foreach (var rendererIndex in new[]
                {
                    MvRecoveredCameraResources.MainRendererIndex,
                    MvRecoveredCameraResources.SubRendererIndex,
                })
            {
                if (rendererDataList.arraySize <= rendererIndex)
                {
                    gaps.Add(
                        $"the assigned URP asset has {rendererDataList.arraySize} renderer entries; " +
                        $"the captured 3DMV contract requires renderer index {rendererIndex}.");
                    continue;
                }

                var rendererData = rendererDataList
                    .GetArrayElementAtIndex(rendererIndex)
                    .objectReferenceValue;
                if (rendererData == null)
                {
                    gaps.Add(
                        $"URP renderer index {rendererIndex} is null; the captured 3DMV feature " +
                        "graph must be assigned explicitly.");
                    continue;
                }

                var serializedRenderer = new SerializedObject(rendererData);
                var featureList = serializedRenderer.FindProperty("m_RendererFeatures");
                if (featureList == null || !featureList.isArray)
                {
                    gaps.Add(
                        $"URP renderer index {rendererIndex} ('{rendererData.name}') does not expose " +
                        "m_RendererFeatures.");
                    continue;
                }

                var actual = new List<MvRendererFeatureDescriptor>(featureList.arraySize);
                for (var featureIndex = 0; featureIndex < featureList.arraySize; featureIndex++)
                {
                    var feature = featureList
                        .GetArrayElementAtIndex(featureIndex)
                        .objectReferenceValue;
                    actual.Add(feature == null
                        ? new MvRendererFeatureDescriptor("<null>", "<null>")
                        : new MvRendererFeatureDescriptor(feature.name, feature.GetType().Name));
                    if (feature != null)
                    {
                        ValidateRecoveredRendererFeatureSettings(
                            feature,
                            rendererIndex,
                            requiresPlanarReflection,
                            requiresEffectDistortion,
                            gaps);
                    }
                }

                foreach (var error in MvRecoveredRendererContract.Validate(rendererIndex, actual))
                {
                    gaps.Add(error);
                }
            }
        }

        private static void ValidateRecoveredRendererFeatureSettings(
            UnityEngine.Object feature,
            int rendererIndex,
            bool requiresPlanarReflection,
            bool requiresEffectDistortion,
            ICollection<string> gaps)
        {
            var serialized = new SerializedObject(feature);
            switch (feature.GetType().Name)
            {
                case "SekaiCharacterOutlineFeature":
                    RequireFloat(
                        serialized,
                        "settings.outlineWidthMin",
                        MvRecoveredRendererContract.OutlineWidthMin,
                        rendererIndex,
                        feature.name,
                        gaps);
                    RequireFloat(
                        serialized,
                        "settings.outlineWidthMax",
                        MvRecoveredRendererContract.OutlineWidthMax,
                        rendererIndex,
                        feature.name,
                        gaps);
                    RequireFloat(
                        serialized,
                        "settings.outlineDistanceNear",
                        MvRecoveredRendererContract.OutlineDistanceNear,
                        rendererIndex,
                        feature.name,
                        gaps);
                    RequireFloat(
                        serialized,
                        "settings.outlineDistanceFar",
                        MvRecoveredRendererContract.OutlineDistanceFar,
                        rendererIndex,
                        feature.name,
                        gaps);
                    var fovCurve = serialized.FindProperty("settings.fovCurve");
                    var capturedCurve = MvRecoveredRendererContract.CreateOutlineFovCurve();
                    if (fovCurve == null ||
                        !CurveMatches(fovCurve.animationCurveValue, capturedCurve))
                    {
                        gaps.Add(
                            $"renderer {rendererIndex} feature '{feature.name}' requires the " +
                            "captured two-key ClampForever settings.fovCurve.");
                    }
                    break;

                case "PlanarReflectionFeature":
                    RequireInteger(
                        serialized,
                        "_planarReflectionInfo.width",
                        MvRecoveredRendererContract.PlanarReflectionWidth,
                        rendererIndex,
                        feature.name,
                        gaps);
                    RequireInteger(
                        serialized,
                        "_planarReflectionInfo.height",
                        MvRecoveredRendererContract.PlanarReflectionHeight,
                        rendererIndex,
                        feature.name,
                        gaps);
                    RequireFloat(
                        serialized,
                        "_planarReflectionInfo.clipPlaneOffset",
                        MvRecoveredRendererContract.PlanarReflectionClipPlaneOffset,
                        rendererIndex,
                        feature.name,
                        gaps);
                    RequireFloat(
                        serialized,
                        "_planarReflectionInfo.planeOffset",
                        MvRecoveredRendererContract.PlanarReflectionPlaneOffset,
                        rendererIndex,
                        feature.name,
                        gaps);
                    if (!requiresPlanarReflection)
                    {
                        break;
                    }
                    if (AllowDummyShadersForDevelopment())
                    {
                        WarnIfShaderMissing(
                            serialized,
                            "_drawStencilShader",
                            MvRecoveredRendererContract.PlanarReflectionStencilShader,
                            rendererIndex,
                            feature.name);
                    }
                    else
                    {
                        RequireShader(
                            serialized,
                            "_drawStencilShader",
                            MvRecoveredRendererContract.PlanarReflectionStencilShader,
                            rendererIndex,
                            feature.name,
                            gaps);
                    }
                    break;

                case "SekaiAfterTransparentRendererFeature":
                    if (requiresEffectDistortion &&
                        Shader.Find(MvRecoveredRendererContract.ApplyDistortionShader) == null)
                    {
                        var message =
                            $"renderer {rendererIndex} feature '{feature.name}' requires shader " +
                            $"'{MvRecoveredRendererContract.ApplyDistortionShader}', which the " +
                            "official feature resolves at runtime.";
                        if (AllowDummyShadersForDevelopment())
                        {
                            Debug.LogWarning(
                                $"{AllowDummyShadersEnvironmentVariable}=1: {message} " +
                                "The development player keeps the inactive pass disabled.");
                        }
                        else
                        {
                            gaps.Add(message);
                        }
                    }
                    break;
            }
        }

        private static bool CurveMatches(AnimationCurve actual, AnimationCurve expected)
        {
            if (actual == null || expected == null ||
                actual.preWrapMode != expected.preWrapMode ||
                actual.postWrapMode != expected.postWrapMode ||
                actual.length != expected.length)
            {
                return false;
            }

            for (var index = 0; index < actual.length; index++)
            {
                var left = actual[index];
                var right = expected[index];
                if (!Mathf.Approximately(left.time, right.time) ||
                    !Mathf.Approximately(left.value, right.value) ||
                    !Mathf.Approximately(left.inTangent, right.inTangent) ||
                    !Mathf.Approximately(left.outTangent, right.outTangent) ||
                    !Mathf.Approximately(left.inWeight, right.inWeight) ||
                    !Mathf.Approximately(left.outWeight, right.outWeight) ||
                    left.weightedMode != right.weightedMode)
                {
                    return false;
                }
            }
            return true;
        }

        private static void RequireFloat(
            SerializedObject serialized,
            string propertyPath,
            float expected,
            int rendererIndex,
            string featureName,
            ICollection<string> gaps)
        {
            var property = serialized.FindProperty(propertyPath);
            if (property == null || !Mathf.Approximately(property.floatValue, expected))
            {
                gaps.Add(
                    $"renderer {rendererIndex} feature '{featureName}' requires {propertyPath}=" +
                    $"{expected}; found {(property == null ? "missing" : property.floatValue.ToString())}.");
            }
        }

        private static void RequireInteger(
            SerializedObject serialized,
            string propertyPath,
            int expected,
            int rendererIndex,
            string featureName,
            ICollection<string> gaps)
        {
            var property = serialized.FindProperty(propertyPath);
            if (property == null || property.intValue != expected)
            {
                gaps.Add(
                    $"renderer {rendererIndex} feature '{featureName}' requires {propertyPath}=" +
                    $"{expected}; found {(property == null ? "missing" : property.intValue.ToString())}.");
            }
        }

        private static void RequireShader(
            SerializedObject serialized,
            string propertyPath,
            string expectedName,
            int rendererIndex,
            string featureName,
            ICollection<string> gaps)
        {
            var property = serialized.FindProperty(propertyPath);
            var shader = property == null ? null : property.objectReferenceValue as Shader;
            if (shader == null || !string.Equals(shader.name, expectedName, StringComparison.Ordinal))
            {
                gaps.Add(
                    $"renderer {rendererIndex} feature '{featureName}' requires {propertyPath} " +
                    $"shader '{expectedName}'; found '{(shader == null ? "missing" : shader.name)}'.");
            }
        }

        private static void WarnIfShaderMissing(
            SerializedObject serialized,
            string propertyPath,
            string expectedName,
            int rendererIndex,
            string featureName)
        {
            var property = serialized.FindProperty(propertyPath);
            var shader = property == null ? null : property.objectReferenceValue as Shader;
            if (shader != null && string.Equals(shader.name, expectedName, StringComparison.Ordinal))
            {
                return;
            }
            Debug.LogWarning(
                $"{AllowDummyShadersEnvironmentVariable}=1: renderer {rendererIndex} feature " +
                $"'{featureName}' is missing {propertyPath} shader '{expectedName}'. " +
                "The development player keeps the inactive pass disabled.");
        }

        private static string FindRuntimeScriptGuid(string scriptName)
        {
            var projectScript = AssetDatabase.FindAssets(
                    $"{scriptName} t:MonoScript",
                    new[] { "Assets/Haruki/MV/Runtime" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .SingleOrDefault(path => string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    scriptName,
                    StringComparison.Ordinal));
            if (projectScript == null)
            {
                throw new FileNotFoundException(
                    $"Haruki runtime component script is missing for {scriptName}.");
            }
            return AssetDatabase.AssetPathToGUID(projectScript);
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
            const string configMarker =
                "\n      // By default, Unity keeps WebGL canvas render target size matched with";
            if (!html.Contains(marker))
            {
                throw new InvalidOperationException("Unity WebGL template no longer exposes its instance callback.");
            }
            if (!html.Contains(configMarker))
            {
                throw new InvalidOperationException("Unity WebGL template no longer exposes its canvas config block.");
            }
            const string exposure =
                "\n                window.harukiMvUnityInstance = unityInstance;";
            const string outputSettings =
                "\n      config.devicePixelRatio = 1;" +
                "\n      config.matchWebGLToCanvasSize = false;\n";
            html = html.Replace(exposure, string.Empty);
            html = html.Replace(outputSettings, string.Empty);
            html = html.Replace(configMarker, outputSettings + configMarker);
            html = html.Replace("width=960 height=600", "width=960 height=540");
            html = html.Replace(
                "canvas.style.height = \"600px\";",
                "canvas.style.height = \"540px\";");
            File.WriteAllText(
                indexPath,
                html.Replace(marker, marker + exposure)
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
