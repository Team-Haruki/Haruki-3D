using System;
using System.IO;
using Haruki.MV;
using Sekai.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV.Editor
{
    public static class MvRenderPipelineAssetBuilder
    {
        public const string DefaultAssetRoot =
            "Assets/Haruki/MV/Generated/Rendering";

        public static UniversalRenderPipelineAsset BuildAndAssign(
            string assetRoot = DefaultAssetRoot)
        {
            if (string.IsNullOrWhiteSpace(assetRoot) ||
                !assetRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The generated render-pipeline root must be below Assets/.",
                    nameof(assetRoot));
            }

            AssetDatabase.DeleteAsset(assetRoot);
            var absoluteRoot = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetRoot);
            Directory.CreateDirectory(absoluteRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var main = MvRecoveredRendererFactory.CreateMainRendererData();
            var sub = MvRecoveredRendererFactory.CreateSubRendererData();
            var presentation = ScriptableObject.CreateInstance<UniversalRendererData>();
            presentation.name = "HarukiPresentationRenderer";
            PopulateUrpResources(presentation);
            PopulateUrpResources(main);
            PopulateUrpResources(sub);
            PersistRendererData(
                presentation,
                assetRoot + "/HarukiPresentationRenderer.asset");
            PersistRendererData(main, assetRoot + "/SekaiMainRenderer.asset");
            PersistRendererData(sub, assetRoot + "/SekaiSubRenderer.asset");

            var pipeline = UniversalRenderPipelineAsset.Create(presentation);
            pipeline.name = "HarukiSekaiMvPipeline";
            AssetDatabase.CreateAsset(
                pipeline,
                assetRoot + "/HarukiSekaiMvPipeline.asset");

            var serialized = new SerializedObject(pipeline);
            var rendererDataList = serialized.FindProperty("m_RendererDataList");
            rendererDataList.arraySize =
                MvRecoveredCameraResources.SubRendererIndex + 1;
            for (var index = 0; index < rendererDataList.arraySize; index++)
            {
                rendererDataList.GetArrayElementAtIndex(index).objectReferenceValue =
                    presentation;
            }
            rendererDataList
                .GetArrayElementAtIndex(MvRecoveredCameraResources.MainRendererIndex)
                .objectReferenceValue = main;
            rendererDataList
                .GetArrayElementAtIndex(MvRecoveredCameraResources.SubRendererIndex)
                .objectReferenceValue = sub;
            serialized.FindProperty("m_DefaultRendererIndex").intValue =
                MvRecoveredCameraResources.PresentationRendererIndex;
            serialized.FindProperty("m_RenderScale").floatValue = 1f;
            serialized.FindProperty("m_MSAA").intValue = 1;
            serialized.FindProperty("m_SupportsHDR").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            return pipeline;
        }

        private static void PopulateUrpResources(UniversalRendererData rendererData)
        {
            const string packageRoot =
                "Packages/com.unity.render-pipelines.universal";
            ResourceReloader.ReloadAllNullIn(rendererData, packageRoot);
        }

        private static void PersistRendererData(
            UniversalRendererData rendererData,
            string assetPath)
        {
            var features = rendererData.rendererFeatures.ToArray();
            AssetDatabase.CreateAsset(rendererData, assetPath);
            foreach (var feature in features)
            {
                feature.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
            }
            AssetDatabase.SaveAssets();

            var serialized = new SerializedObject(rendererData);
            var featureMap = serialized.FindProperty("m_RendererFeatureMap");
            featureMap.arraySize = features.Length;
            for (var index = 0; index < features.Length; index++)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        features[index],
                        out _,
                        out long localId))
                {
                    throw new InvalidOperationException(
                        $"Could not persist renderer feature '{features[index].name}'.");
                }
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }
    }
}
