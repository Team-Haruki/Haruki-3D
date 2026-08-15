using System.Reflection;
using Haruki.MV;
using NUnit.Framework;
using Sekai.Core.Graphics;
using Sekai.Rendering;
using Sekai.Rendering.Components;
using UnityEngine;
using UnityEngine.Rendering;

namespace Haruki.MV.Tests
{
    public sealed class PostEffectV2Tests
    {
        [Test]
        public void EachCameraOwnsAnIndependentSekaiVolumeProfile()
        {
            var main = new GameObject("MainCamera");
            var cutIn = new GameObject("CutInCamera");

            try
            {
                main.AddComponent<Camera>();
                cutIn.AddComponent<Camera>();
                var mainState = main.AddComponent<MvPostEffectState>();
                var cutInState = cutIn.AddComponent<MvPostEffectState>();
                var mainPost = main.AddComponent<PostEffectV2>();
                var cutInPost = cutIn.AddComponent<PostEffectV2>();

                mainPost.Initialize(mainState, "MainPostEffect");
                cutInPost.Initialize(cutInState, "CutInPostEffect");

                Assert.That(mainPost.Volume, Is.Not.Null);
                Assert.That(cutInPost.Volume, Is.Not.Null);
                Assert.That(mainPost.Volume.Profile, Is.Not.SameAs(cutInPost.Volume.Profile));
                Assert.That(
                    main.GetComponent<Volume>().sharedProfile,
                    Is.SameAs(mainPost.Volume.Profile));
                Assert.That(
                    cutIn.GetComponent<Volume>().sharedProfile,
                    Is.SameAs(cutInPost.Volume.Profile));
            }
            finally
            {
                Object.DestroyImmediate(main);
                Object.DestroyImmediate(cutIn);
            }
        }

        [Test]
        public void SynchronizeCopiesTimelineStateIntoOfficialVolumeComponents()
        {
            var host = new GameObject("MainCamera");

            try
            {
                host.AddComponent<Camera>();
                var state = host.AddComponent<MvPostEffectState>();
                var post = host.AddComponent<PostEffectV2>();
                post.Initialize(state, "MainPostEffect");

                SetProperty(state, nameof(MvPostEffectState.BloomIntensity), 1.25f);
                SetProperty(state, nameof(MvPostEffectState.BloomScatter), 0.72f);
                SetProperty(state, nameof(MvPostEffectState.BloomUseBlend), true);
                SetEnabled(state, MvPostEffectKind.LegacyBloom, 0, true);
                SetEnabled(state, MvPostEffectKind.LegacyBloom, 1, true);
                SetEnabled(state, MvPostEffectKind.LegacyBloom, 2, true);

                SetProperty(state, nameof(MvPostEffectState.VignetteColor), Color.magenta);
                SetProperty(state, nameof(MvPostEffectState.VignetteCenter), new Vector2(0.4f, 0.6f));
                SetProperty(state, nameof(MvPostEffectState.VignetteIntensity), 0.35f);
                SetProperty(state, nameof(MvPostEffectState.VignetteSmoothness), 0.6f);
                SetProperty(state, nameof(MvPostEffectState.VignetteRoundness), 0.9f);
                for (var param = 0; param < 6; param++)
                {
                    SetEnabled(state, MvPostEffectKind.Vignette, param, true);
                }

                post.Synchronize();

                Assert.That(post.Volume.Bloom.IsActive(), Is.True);
                Assert.That(post.Volume.Bloom.intensity.value, Is.EqualTo(1.25f));
                Assert.That(post.Volume.Bloom.scatter.value, Is.EqualTo(0.72f));
                Assert.That(post.Volume.Bloom.useNewBlend.value, Is.True);
                Assert.That(post.Volume.Vignette.IsActive(), Is.True);
                Assert.That(post.Volume.Vignette.color.value, Is.EqualTo(Color.magenta));
                Assert.That(
                    post.Volume.Vignette.center.value,
                    Is.EqualTo(new Vector2(0.4f, 0.6f)));
                Assert.That(post.Volume.Vignette.intensity.value, Is.EqualTo(0.35f));
                Assert.That(post.Volume.Vignette.smoothness.value, Is.EqualTo(0.6f));
                Assert.That(post.Volume.Vignette.roundness.value, Is.EqualTo(0.9f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SaturationBlurParamTypeSelectsTheOfficialShaderVariant()
        {
            var host = new GameObject("MainCamera");

            try
            {
                host.AddComponent<Camera>();
                var state = host.AddComponent<MvPostEffectState>();
                var post = host.AddComponent<PostEffectV2>();
                post.Initialize(state, "MainPostEffect");

                SetEnabled(state, MvPostEffectKind.SaturationBlur, 1, true);
                post.Synchronize();

                Assert.That(
                    post.Volume.SaturationBlur.saturationBlurType.value,
                    Is.EqualTo(SaturationBlurVolumeType.V2));

                SetEnabled(state, MvPostEffectKind.SaturationBlur, 1, false);
                SetEnabled(state, MvPostEffectKind.SaturationBlur, 0, true);
                post.Synchronize();

                Assert.That(
                    post.Volume.SaturationBlur.saturationBlurType.value,
                    Is.EqualTo(SaturationBlurVolumeType.V1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DisabledTimelineFamilyDoesNotRemainActiveInTheVolumeStack()
        {
            var host = new GameObject("MainCamera");

            try
            {
                host.AddComponent<Camera>();
                var state = host.AddComponent<MvPostEffectState>();
                var post = host.AddComponent<PostEffectV2>();
                post.Initialize(state, "MainPostEffect");

                SetProperty(state, nameof(MvPostEffectState.Saturation), 1.5f);
                SetEnabled(state, MvPostEffectKind.Saturation, 0, true);
                post.Synchronize();
                Assert.That(post.Volume.Saturation.IsActive(), Is.True);

                SetEnabled(state, MvPostEffectKind.Saturation, 0, false);
                post.Synchronize();

                Assert.That(post.Volume.Saturation.IsActive(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CocParametersMatchTheRecoveredThinLensFormula()
        {
            var parameters = PostEffectV2.CalculateCocParameters(
                5.77521f,
                2f,
                60f);

            Assert.That(parameters.x, Is.EqualTo(5.77521f));
            Assert.That(parameters.y, Is.EqualTo(0.314948f).Within(0.00001f));
            Assert.That(parameters.z, Is.Zero);
            Assert.That(parameters.w, Is.Zero);
        }

        [Test]
        public void VerticalFovPreservesSixteenByNineAndExpandsNarrowerOutputs()
        {
            Assert.That(
                PostEffectV2.CalculateVerticalFov(50f, 16f / 9f),
                Is.EqualTo(50f));
            Assert.That(
                PostEffectV2.CalculateVerticalFov(50f, 3200f / 2136f),
                Is.EqualTo(57.9161f).Within(0.001f));
        }

        [Test]
        public void CameraParameterScaleDrivesOfficialDofFocusDistance()
        {
            var parent = new GameObject("mainCam");
            var parameter = new GameObject("CamParam");
            var cameraObject = new GameObject("Camera");

            try
            {
                parameter.transform.SetParent(parent.transform, false);
                parameter.transform.localScale = new Vector3(5.77521f, 1f, 1f);
                cameraObject.transform.SetParent(parent.transform, false);
                cameraObject.AddComponent<Camera>();
                var state = cameraObject.AddComponent<MvPostEffectState>();
                var post = cameraObject.AddComponent<PostEffectV2>();
                post.Initialize(state, "MainPostEffect", parameter.transform);
                SetEnabled(state, MvPostEffectKind.SekaiDof, 0, true);

                post.Synchronize();

                Assert.That(
                    post.Volume.SekaiDof.focusDistance.value,
                    Is.EqualTo(5.77521f));
                Assert.That(post.ParameterTransform, Is.SameAs(parameter.transform));
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(parameter);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void SetProperty<T>(MvPostEffectState state, string name, T value)
        {
            typeof(MvPostEffectState)
                .GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(state, value);
        }

        private static void SetEnabled(
            MvPostEffectState state,
            MvPostEffectKind kind,
            int paramType,
            bool enabled)
        {
            typeof(MvPostEffectState)
                .GetMethod("SetEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(state, new object[] { kind, paramType, enabled });
        }
    }
}
