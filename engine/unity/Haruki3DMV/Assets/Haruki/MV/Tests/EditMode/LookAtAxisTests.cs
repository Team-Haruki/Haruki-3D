using System.Reflection;
using NUnit.Framework;
using Sekai.Core;
using Sekai.Core.Rendering;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Haruki.MV.Tests
{
    public sealed class LookAtAxisTests
    {
        private static readonly MethodInfo UpdateCamera = typeof(LookAtAxis).GetMethod(
            "UpdateCamera",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown()
        {
            RenderConfig.lookAtTargetCamera = null;
        }

        [TestCase(LookAtAxis.Axis.X)]
        [TestCase(LookAtAxis.Axis.Y)]
        [TestCase(LookAtAxis.Axis.Z)]
        public void ConstrainsOfficialLookRotationToSelectedLocalAxis(LookAtAxis.Axis axis)
        {
            var parent = new GameObject("parent");
            var target = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var cameraObject = new GameObject("camera", typeof(Camera));
            try
            {
                target.transform.SetParent(parent.transform, false);
                parent.transform.rotation = Quaternion.Euler(13f, 29f, 7f);
                target.transform.localPosition = new Vector3(2f, 3f, 5f);
                cameraObject.transform.position = new Vector3(-4f, 1f, -2f);

                var component = target.AddComponent<LookAtAxis>();
                component.axis = axis;
                InvokeUpdate(component, cameraObject.GetComponent<Camera>());

                var expectedRotation = Quaternion.Inverse(parent.transform.rotation)
                    * Quaternion.LookRotation(
                        target.transform.position - cameraObject.transform.position,
                        Vector3.up);
                var expectedEuler = expectedRotation.eulerAngles;
                switch (axis)
                {
                    case LookAtAxis.Axis.X:
                        expectedEuler.y = 0f;
                        expectedEuler.z = 0f;
                        break;
                    case LookAtAxis.Axis.Y:
                        expectedEuler.x = 0f;
                        expectedEuler.z = 0f;
                        break;
                    case LookAtAxis.Axis.Z:
                        expectedEuler.x = 0f;
                        expectedEuler.y = 0f;
                        break;
                }

                Assert.That(
                    target.transform.localEulerAngles,
                    Is.EqualTo(expectedEuler).Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void IgnoresEveryCameraExceptConfiguredLookAtTarget()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var acceptedCamera = new GameObject("accepted", typeof(Camera));
            var ignoredCamera = new GameObject("ignored", typeof(Camera));
            try
            {
                target.transform.rotation = Quaternion.Euler(11f, 22f, 33f);
                var original = target.transform.localEulerAngles;
                var component = target.AddComponent<LookAtAxis>();
                RenderConfig.lookAtTargetCamera = acceptedCamera.GetComponent<Camera>();

                InvokeUpdate(component, ignoredCamera.GetComponent<Camera>());

                Assert.That(
                    target.transform.localEulerAngles,
                    Is.EqualTo(original).Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(ignoredCamera);
                Object.DestroyImmediate(acceptedCamera);
                Object.DestroyImmediate(target);
            }
        }

        private static void InvokeUpdate(LookAtAxis component, Camera camera)
        {
            Assert.That(UpdateCamera, Is.Not.Null);
            UpdateCamera.Invoke(component, new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), camera });
        }
    }
}
