using System;
using System.Reflection;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV.Tests
{
    public sealed class PlanarReflectionPassTests
    {
        private PlanarReflectionPass _pass;

        [SetUp]
        public void SetUp()
        {
            _pass = PlanarReflectionPass.Instance;
            _pass.Dispose();
            _pass.EnablePlanarReflection = false;
            _pass.EnableObjectTransparentSorting = false;
            _pass.TargetTransform = null;
            _pass.Meshes = null;
            _pass.SetShaderEnableKeyword(false);
        }

        [TearDown]
        public void TearDown()
        {
            _pass.Dispose();
            _pass.EnablePlanarReflection = false;
            _pass.EnableObjectTransparentSorting = false;
            _pass.TargetTransform = null;
            _pass.Meshes = null;
            _pass.SetShaderEnableKeyword(false);
        }

        [Test]
        public void SetPassClampsSizeAndAllocatesTheRecoveredTargetsWhenEnabled()
        {
            _pass.EnablePlanarReflection = true;
            _pass.SetPass(null, new PlanarReflectionInfo
            {
                width = 1,
                height = -4,
                clipPlaneOffset = 0.25f,
                planeOffset = 0.5f,
            });

            var info = GetField<PlanarReflectionInfo>("_planarReflectionInfo");
            var color = GetField<RTHandle>("_reflectionRT");
            var depth = GetField<RTHandle>("_reflectionDepthRT");

            Assert.That(_pass.renderPassEvent, Is.EqualTo(RenderPassEvent.BeforeRenderingOpaques));
            Assert.That(info.width, Is.EqualTo(2));
            Assert.That(info.height, Is.EqualTo(2));
            Assert.That(info.clipPlaneOffset, Is.EqualTo(0.25f));
            Assert.That(info.planeOffset, Is.EqualTo(0.5f));
            Assert.That(color, Is.Not.Null);
            Assert.That(depth, Is.Not.Null);
            Assert.That(color.rt.graphicsFormat, Is.EqualTo(GraphicsFormat.R8G8B8A8_UNorm));
            Assert.That(color.rt.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(color.rt.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            // The recovered call supplies enum value 4 (R8G8B8A8_SRGB), but
            // RTHandleSystem normalizes an allocation with Depth24 into a
            // depth-only RenderTexture.
            Assert.That(depth.rt.graphicsFormat, Is.EqualTo(GraphicsFormat.None));
            Assert.That(depth.rt.depth, Is.EqualTo(24));
        }

        [Test]
        public void SetPassDoesNotAllocateTargetsWhileReflectionIsDisabled()
        {
            _pass.SetPass(null, new PlanarReflectionInfo { width = 1024, height = 1024 });

            Assert.That(GetField<RTHandle>("_reflectionRT"), Is.Null);
            Assert.That(GetField<RTHandle>("_reflectionDepthRT"), Is.Null);
        }

        [Test]
        public void ReflectionMatrixMirrorsAcrossTheRequestedPlane()
        {
            var matrix = InvokeReflectionMatrix(Vector3.up, 0f);
            var point = matrix.MultiplyPoint3x4(new Vector3(2f, 3f, 4f));

            Assert.That(point.x, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(point.y, Is.EqualTo(-3f).Within(0.00001f));
            Assert.That(point.z, Is.EqualTo(4f).Within(0.00001f));

            matrix = InvokeReflectionMatrix(Vector3.up, -2f);
            point = matrix.MultiplyPoint3x4(new Vector3(0f, 5f, 0f));
            Assert.That(point.y, Is.EqualTo(-1f).Within(0.00001f));
        }

        [Test]
        public void CameraSpacePlaneAppliesClipOffsetBeforeTransformation()
        {
            _pass.SetPass(null, new PlanarReflectionInfo
            {
                width = 2,
                height = 2,
                clipPlaneOffset = 0.25f,
            });

            var plane = InvokeCameraSpacePlane(
                Matrix4x4.identity,
                new Vector3(0f, 2f, 0f),
                Vector3.up,
                1f);

            Assert.That(GetPlaneNormal(plane), Is.EqualTo(Vector3.up));
            Assert.That(GetPlaneDistance(plane), Is.EqualTo(-2.25f).Within(0.00001f));
        }

        private Matrix4x4 InvokeReflectionMatrix(Vector3 normal, float distance)
        {
            var plane = CreatePlane(normal, distance);
            var method = typeof(PlanarReflectionPass).GetMethod(
                "CalculateReflectionMatrix",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var args = new object[] { Matrix4x4.zero, plane };
            method.Invoke(null, args);
            return (Matrix4x4)args[0];
        }

        private object InvokeCameraSpacePlane(
            Matrix4x4 matrix,
            Vector3 position,
            Vector3 normal,
            float sideSign)
        {
            var method = typeof(PlanarReflectionPass).GetMethod(
                "CameraSpacePlane",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(_pass, new object[] { matrix, position, normal, sideSign });
        }

        private static object CreatePlane(Vector3 normal, float distance)
        {
            var type = typeof(PlanarReflectionPass).GetNestedType(
                "Plane",
                BindingFlags.NonPublic);
            Assert.That(type, Is.Not.Null);
            return Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { normal, distance },
                null);
        }

        private static Vector3 GetPlaneNormal(object plane)
        {
            var field = plane.GetType().GetField("normal");
            Assert.That(field, Is.Not.Null);
            return (Vector3)field.GetValue(plane);
        }

        private static float GetPlaneDistance(object plane)
        {
            var field = plane.GetType().GetField("distance");
            Assert.That(field, Is.Not.Null);
            return (float)field.GetValue(plane);
        }

        private T GetField<T>(string fieldName)
        {
            var field = typeof(PlanarReflectionPass).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(_pass);
        }
    }
}
