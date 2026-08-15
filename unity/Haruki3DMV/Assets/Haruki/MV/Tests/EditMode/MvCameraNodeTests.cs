using System.Collections.Generic;
using NUnit.Framework;
using Sekai.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Utils;

namespace Haruki.MV.Tests
{
    public sealed class MvCameraNodeTests
    {
        [Test]
        public void UsesRecoveredResourcesPathsRatherThanCameraUtilityShortNames()
        {
            Assert.That(
                MvCameraNode.MainCameraResource,
                Is.EqualTo("Core/Common/Camera/MainCamera_MV"));
            Assert.That(
                MvCameraNode.SubCameraResource,
                Is.EqualTo("Core/Common/Camera/SubCamera"));
        }

        [Test]
        public void LoadsOfficialCameraResourcesAndBindsCustomSubCamera()
        {
            var root = new GameObject("MV");
            var mainPrefab = CameraPrefab("MainCamera_MV");
            var subPrefab = CameraPrefab("SubCamera");
            var bindings = new Dictionary<string, Object>
            {
                ["Directional Blur Track"] = root,
                ["Fade Out Track"] = root,
                ["Legacy Bloom Track"] = root,
                ["Light Overlay Track"] = root,
                ["Sekai Dof Track"] = root,
                ["Vignette Track"] = root,
            };
            var data = ScriptableObject.CreateInstance<MusicVideoData>();
            data.cameraInfo = new MusicVideoCameraInfo
            {
                useSubCamera = true,
                subCameraResolution = 1,
                subCameraCustomWidth = 1536,
                subCameraCustomHeight = 768,
            };

            var node = new MvCameraNode(
                bindings,
                root.transform,
                path => path == MvCameraNode.MainCameraResource
                    ? mainPrefab
                    : path == MvCameraNode.SubCameraResource ? subPrefab : null);
            try
            {
                node.Load(data);

                Assert.That(bindings["MainCamera"], Is.SameAs(node.MainCameraRoot));
                Assert.That(bindings["SubCamera"], Is.SameAs(node.SubCameraRoot));
                Assert.That(node.SubCamera.targetTexture.width, Is.EqualTo(1536));
                Assert.That(node.SubCamera.targetTexture.height, Is.EqualTo(768));
                Assert.That(node.SubCamera.targetTexture.depth, Is.EqualTo(24));
                Assert.That(
                    node.SubCamera.targetTexture.graphicsFormat,
                    Is.EqualTo(GraphicsFormat.R8G8B8A8_UNorm));
                Assert.That(node.SubCamera.targetTexture.antiAliasing, Is.EqualTo(1));
                Assert.That(node.SubCamera.targetTexture.sRGB, Is.False);
                Assert.That(node.SubCamera.targetTexture.useDynamicScale, Is.False);
                Assert.That(node.MainAdjustment, Is.Not.Null);
                Assert.That(node.SubAdjustment, Is.Not.Null);
                Assert.That(node.PostEffectState, Is.Not.Null);
                Assert.That(node.PostEffect, Is.Not.Null);
                Assert.That(node.PostEffect.Volume, Is.Not.Null);
                Assert.That(node.PostEffect.Volume.Profile, Is.Not.Null);
                Assert.That(
                    node.MainCamera.GetComponent<Volume>().sharedProfile,
                    Is.SameAs(node.PostEffect.Volume.Profile));
                Assert.That(node.PostEffect.CurrentCamera, Is.SameAs(node.MainCamera));
                Assert.That(
                    bindings["Directional Blur Track"],
                    Is.SameAs(node.PostEffectState));
                Assert.That(
                    bindings["Vignette Track"],
                    Is.SameAs(node.PostEffectState));
            }
            finally
            {
                node.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(mainPrefab);
                Object.DestroyImmediate(subPrefab);
            }
        }

        [Test]
        public void RecoveredMainCameraResourceMatchesCapturedHierarchyAndCameraState()
        {
            var root = MvRecoveredCameraResources.Create(MvCameraNode.MainCameraResource);
            try
            {
                var mainCam = root.transform.Find("mainCam");
                Assert.That(mainCam, Is.Not.Null);
                Assert.That(mainCam.GetSiblingIndex(), Is.EqualTo(0));
                Assert.That(
                    mainCam.localPosition,
                    Is.EqualTo(new Vector3(1.8f, 1f, 6f)).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(mainCam.Find("CamParam").GetSiblingIndex(), Is.EqualTo(0));
                Assert.That(mainCam.Find("Camera").GetSiblingIndex(), Is.EqualTo(1));
                Assert.That(
                    mainCam.Find("CamParam").localPosition,
                    Is.EqualTo(new Vector3(0f, 0f, 0.5f)).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(
                    mainCam.Find("CamParam").localScale,
                    Is.EqualTo(new Vector3(1f, 1f, 35f)).Using(Vector3ComparerWithEqualsOperator.Instance));

                var camera = mainCam.Find("Camera").GetComponent<Camera>();
                Assert.That(camera.nearClipPlane, Is.EqualTo(0.1f));
                Assert.That(camera.farClipPlane, Is.EqualTo(500f));
                Assert.That(camera.fieldOfView, Is.EqualTo(50f));
                Assert.That(camera.depth, Is.EqualTo(0f));
                Assert.That(camera.cullingMask, Is.EqualTo(555745280));
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Color));
                Assert.That(camera.allowHDR, Is.False);
                Assert.That(camera.allowMSAA, Is.False);
                Assert.That(camera.allowDynamicResolution, Is.False);
                AssertRendererIndex(camera, MvRecoveredCameraResources.MainRendererIndex);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RecoveredSubCameraResourceMatchesCapturedHierarchyAndCameraState()
        {
            var root = MvRecoveredCameraResources.Create(MvCameraNode.SubCameraResource);
            try
            {
                var subCam = root.transform.Find("subCam");
                Assert.That(subCam, Is.Not.Null);
                Assert.That(subCam.Find("target").GetSiblingIndex(), Is.EqualTo(0));
                Assert.That(subCam.Find("Camera").GetSiblingIndex(), Is.EqualTo(1));
                Assert.That(subCam.Find("CamParam").GetSiblingIndex(), Is.EqualTo(2));
                Assert.That(
                    subCam.Find("CamParam").localPosition,
                    Is.EqualTo(new Vector3(0f, 0f, 0.35f)).Using(Vector3ComparerWithEqualsOperator.Instance));

                var camera = subCam.Find("Camera").GetComponent<Camera>();
                Assert.That(camera.nearClipPlane, Is.EqualTo(0.01f));
                Assert.That(camera.farClipPlane, Is.EqualTo(1000f));
                Assert.That(camera.fieldOfView, Is.EqualTo(30f));
                Assert.That(camera.depth, Is.EqualTo(-2f));
                Assert.That(camera.cullingMask, Is.EqualTo(20971520));
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Depth));
                Assert.That(camera.allowHDR, Is.False);
                Assert.That(camera.allowMSAA, Is.False);
                Assert.That(camera.allowDynamicResolution, Is.False);
                AssertRendererIndex(camera, MvRecoveredCameraResources.SubRendererIndex);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FallsBackToRecoveredCameraWhenBaseApkResourceIsUnavailable()
        {
            var root = new GameObject("MV");
            var data = ScriptableObject.CreateInstance<MusicVideoData>();
            data.cameraInfo = new MusicVideoCameraInfo();
            var node = new MvCameraNode(
                new Dictionary<string, Object>(),
                root.transform,
                _ => null);
            try
            {
                node.Load(data);

                Assert.That(node.UsedRecoveredResourceFallback, Is.True);
                Assert.That(node.MainCamera.transform.parent.name, Is.EqualTo("mainCam"));
                Assert.That(node.MainCamera.nearClipPlane, Is.EqualTo(0.1f));
                Assert.That(
                    node.PostEffect.ParameterTransform,
                    Is.SameAs(node.MainCamera.transform.parent.Find("CamParam")));
            }
            finally
            {
                node.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
            }
        }

        private static GameObject CameraPrefab(string name)
        {
            var root = new GameObject(name);
            root.AddComponent<Camera>();
            return root;
        }

        private static void AssertRendererIndex(Camera camera, int expected)
        {
            var data = camera.GetComponent("UniversalAdditionalCameraData");
            Assert.That(data, Is.Not.Null);
            var serialized = new SerializedObject(data);
            Assert.That(
                serialized.FindProperty("m_RendererIndex").intValue,
                Is.EqualTo(expected));
        }
    }
}
