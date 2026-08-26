using System.Linq;
using Haruki.MV.Editor;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV.Tests
{
    public sealed class MvRenderPipelineAssetBuilderTests
    {
        [Test]
        public void BuilderAssignsTheCapturedMainAndSubRendererIndices()
        {
            var pipeline = MvRenderPipelineAssetBuilder.BuildAndAssign();

            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(pipeline));
            var serialized = new SerializedObject(pipeline);
            Assert.That(serialized.FindProperty("m_RenderScale").floatValue, Is.EqualTo(1f));
            Assert.That(serialized.FindProperty("m_MSAA").intValue, Is.EqualTo(1));
            var list = serialized.FindProperty("m_RendererDataList");
            Assert.That(list.arraySize, Is.EqualTo(11));
            Assert.That(
                serialized.FindProperty("m_DefaultRendererIndex").intValue,
                Is.EqualTo(MvRecoveredCameraResources.PresentationRendererIndex));
            var presentation = list
                .GetArrayElementAtIndex(MvRecoveredCameraResources.PresentationRendererIndex)
                .objectReferenceValue as UniversalRendererData;
            var main = list.GetArrayElementAtIndex(5).objectReferenceValue as SekaiRendererData;
            var sub = list.GetArrayElementAtIndex(10).objectReferenceValue as SekaiRendererData;
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation, Is.Not.InstanceOf<SekaiRendererData>());
            Assert.That(presentation.rendererFeatures, Is.Empty);
            Assert.That(main, Is.Not.Null);
            Assert.That(sub, Is.Not.Null);
            AssertCoreRendererShaders(presentation);
            AssertCoreRendererShaders(main);
            AssertCoreRendererShaders(sub);
            Assert.That(main.useSekaiPostProcess, Is.True);
            Assert.That(sub.useSekaiPostProcess, Is.False);
            Assert.That(
                main.rendererFeatures.Select(feature => feature.name),
                Is.EqualTo(
                    MvRecoveredRendererContract.ForRenderer(5)
                        .Select(feature => feature.Name)));
            Assert.That(
                sub.rendererFeatures.Select(feature => feature.name),
                Is.EqualTo(
                    MvRecoveredRendererContract.ForRenderer(10)
                        .Select(feature => feature.Name)));
        }

        private static void AssertCoreRendererShaders(UniversalRendererData rendererData)
        {
            var serialized = new SerializedObject(rendererData);
            Assert.That(
                serialized.FindProperty("shaders.blitPS").objectReferenceValue,
                Is.Not.Null,
                $"{rendererData.name} has no URP blit shader.");
            Assert.That(
                serialized.FindProperty("shaders.coreBlitPS").objectReferenceValue,
                Is.Not.Null,
                $"{rendererData.name} has no URP CoreBlit shader.");
        }
    }
}
