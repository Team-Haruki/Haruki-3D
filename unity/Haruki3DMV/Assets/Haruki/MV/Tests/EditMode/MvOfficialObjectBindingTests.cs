using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvOfficialObjectBindingTests
    {
        private class ControlGroupBase : MonoBehaviour
        {
        }

        private sealed class DerivedControlGroup : ControlGroupBase
        {
        }

        private sealed class StageObjDrawCameraSelectController : MonoBehaviour
        {
        }

        private sealed class TextMeshPro : MonoBehaviour
        {
        }

        private sealed class PenlightParameter : MonoBehaviour
        {
            public bool Initialized { get; private set; }

            private void Initialize()
            {
                Initialized = true;
            }
        }

        [Test]
        public void PenlightInitializesAndBindsEveryTransformAsGameObject()
        {
            var root = new GameObject("Penlight");
            var child = new GameObject("PenlightColor0");
            child.transform.SetParent(root.transform, false);
            var parameter = root.AddComponent<PenlightParameter>();
            var bindings = new Dictionary<string, Object>();
            try
            {
                MvOfficialObjectBinding.InitializePenlight(root);
                MvOfficialObjectBinding.BindPenlightTransforms(root, bindings);

                Assert.That(parameter.Initialized, Is.True);
                Assert.That(bindings["Penlight"], Is.SameAs(root));
                Assert.That(bindings["PenlightColor0"], Is.SameAs(child));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PenlightInitializationOnlyUsesTheRootParameter()
        {
            var root = new GameObject("Penlight");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, false);
            var childParameter = child.AddComponent<PenlightParameter>();
            try
            {
                MvOfficialObjectBinding.InitializePenlight(root);

                Assert.That(childParameter.Initialized, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StageBindingsKeepConfirmedComponentTargetsAndOrder()
        {
            var root = new GameObject("Decoration");
            var groupObject = new GameObject("ControlGroupA");
            groupObject.transform.SetParent(root.transform, false);
            var group = groupObject.AddComponent<DerivedControlGroup>();
            var textObject = new GameObject("PrefixTypeWriter12Suffix");
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.AddComponent<TextMeshPro>();
            var camera0 = new GameObject("CameraSelectA");
            camera0.transform.SetParent(root.transform, false);
            var first = camera0.AddComponent<StageObjDrawCameraSelectController>();
            var camera1 = new GameObject("CameraSelectB");
            camera1.transform.SetParent(root.transform, false);
            camera1.SetActive(false);
            var second = camera1.AddComponent<StageObjDrawCameraSelectController>();
            var bindings = new Dictionary<string, Object>();
            try
            {
                MvOfficialObjectBinding.BindControlGroups(root, bindings);
                MvOfficialObjectBinding.BindStageDecorationTargets(root, bindings);

                Assert.That(bindings["ControlGroupA"], Is.SameAs(group));
                Assert.That(bindings["PrefixTypeWriter12Suffix"], Is.SameAs(text));
                Assert.That(bindings["StageObjDrawCameraSelectTrack0"], Is.SameAs(first));
                Assert.That(bindings["StageObjDrawCameraSelectTrack1"], Is.SameAs(second));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
