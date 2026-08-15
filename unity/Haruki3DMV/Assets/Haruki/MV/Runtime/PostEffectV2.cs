using System;
using Haruki.MV;
using Sekai.Rendering;
using Sekai.Rendering.Components;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sekai.Core.Graphics
{
    /// <summary>
    /// Camera-local bridge matching the official PostEffectV2 -> SekaiVolume
    /// ownership boundary. Timeline mixers write MvPostEffectState; this
    /// component publishes that state to the camera's own VolumeProfile.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class PostEffectV2 : MonoBehaviour
    {
        private const float TargetAspect = 16f / 9f;
        private static readonly int CocParamsId = Shader.PropertyToID("_CoCParams");
        private static readonly int ScreenDistortionNoiseTextureId =
            Shader.PropertyToID("_ScreenDistortionNoiseTexture");

        [SerializeField]
        private Transform parameterTransform;

        private MvPostEffectState _state;

        public Camera CurrentCamera { get; private set; }
        public SekaiVolume Volume { get; private set; }

        public Transform ParameterTransform => parameterTransform;

        public void Initialize(
            MvPostEffectState state,
            string profileName,
            Transform cameraParameter = null)
        {
            _state = state != null ? state : throw new ArgumentNullException(nameof(state));
            CurrentCamera = GetComponent<Camera>();
            if (CurrentCamera == null)
            {
                throw new InvalidOperationException("PostEffectV2 requires a Camera.");
            }

            parameterTransform = cameraParameter != null
                ? cameraParameter
                : FindParameterTransform(CurrentCamera.transform);

            Volume?.Dispose();
            Volume = new SekaiVolume();
            Volume.SetupVolume(gameObject, profileName);
            Volume.Enabled = true;

            // Captured High MV quality uses SMAA High regardless of the final
            // 720p/1080p/1440p/UHD output size.
            Volume.Antialiasing.mode.value =
                AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Volume.Antialiasing.antialiasingQuality.value = AntialiasingQuality.High;
            Volume.Antialiasing.SetActive(true);
            Synchronize();
        }

        public void Synchronize()
        {
            if (_state == null || Volume == null)
            {
                return;
            }

            SynchronizeChromaticAberration();
            SynchronizeDirectionalBlur();
            SynchronizeFadeOut();
            SynchronizeBloom();
            SynchronizeLegacyDof();
            SynchronizeIncidentLight();
            SynchronizeLightOverlay();
            SynchronizeSaturation();
            SynchronizeSaturationBlur();
            SynchronizeScreenDistortion();
            SynchronizeSekaiDof();
            SynchronizeSolarisation();
            SynchronizeVignette();
            SynchronizeLut();
        }

        private void LateUpdate()
        {
            SynchronizeCameraParameters();
            Synchronize();
        }

        private void OnDestroy()
        {
            Volume?.Dispose();
            Volume = null;
            CurrentCamera = null;
            parameterTransform = null;
            _state = null;
        }

        private void SynchronizeChromaticAberration()
        {
            var active = AnyEnabled(MvPostEffectKind.ChromaticAberration, 0, 1);
            var component = Volume.ChromaticAberration;
            component.SetActive(active);
            if (!active)
            {
                return;
            }
            component.ChromaticAberrationMode.value =
                _state.IsEnabled(MvPostEffectKind.ChromaticAberration, 1) ? 1 : 0;
            component.OffsetR.value = _state.ChromaticOffsetR;
            component.OffsetG.value = _state.ChromaticOffsetG;
            component.OffsetB.value = _state.ChromaticOffsetB;
            component.ScaleR.value = _state.ChromaticScale.x;
            component.ScaleG.value = _state.ChromaticScale.y;
            component.ScaleB.value = _state.ChromaticScale.z;
        }

        private void SynchronizeDirectionalBlur()
        {
            var directional = _state.IsEnabled(MvPostEffectKind.DirectionalBlur, 0);
            var radial = _state.IsEnabled(MvPostEffectKind.DirectionalBlur, 1);
            var component = Volume.DirectionalBlur;
            component.SetActive(directional || radial);
            if (radial)
            {
                component.blurType.value = 1;
                component.blurStrength.value = _state.RadialBlurStrength;
                component.blurCenterPosition.value = _state.RadialBlurCenter;
            }
            else if (directional)
            {
                component.blurType.value = 0;
                component.blurStrength.value = _state.DirectionalBlurStrength;
                component.blurDirection.value = _state.DirectionalBlurDirection;
            }
            else
            {
                component.blurStrength.value = 0f;
            }
        }

        private void SynchronizeFadeOut()
        {
            var fade = Volume.FadeOut;
            var fadeActive = AnyEnabled(MvPostEffectKind.FadeOut, 0, 1);
            fade.SetActive(fadeActive);
            fade.fadeOut.value = fadeActive ? _state.FadeOut : 0.5f;
            fade.fadeOutLerp.value = fadeActive ? _state.FadeOutLerp : 0.5f;

            var beforeProp = Volume.FadeOutBeforeProp;
            var beforePropActive = AnyEnabled(MvPostEffectKind.FadeOut, 2, 3);
            beforeProp.SetActive(beforePropActive);
            beforeProp.fadeOutBeforeProp.value =
                beforePropActive ? _state.FadeOutBeforeProp : 0.5f;
            beforeProp.fadeOutBeforePropLerp.value =
                beforePropActive ? _state.FadeOutBeforePropLerp : 0.5f;
        }

        private void SynchronizeBloom()
        {
            var active = AnyEnabled(MvPostEffectKind.LegacyBloom, 0, 1, 2);
            var component = Volume.Bloom;
            component.SetActive(active);
            component.useNewBlend.value = active && _state.BloomUseBlend;
            component.intensity.value = active ? _state.BloomIntensity : 0f;
            component.scatter.value = active ? _state.BloomScatter : 0f;
        }

        private void SynchronizeLegacyDof()
        {
            Volume.LegacyDof.SetActive(
                AnyEnabled(MvPostEffectKind.LegacyDof, 0, 1));
        }

        private void SynchronizeIncidentLight()
        {
            var activeOrder = -1;
            foreach (var order in _state.IncidentLights.Keys)
            {
                if (_state.IsEnabled(MvPostEffectKind.IncidentLight, order) &&
                    (activeOrder < 0 || order < activeOrder))
                {
                    activeOrder = order;
                }
            }
            var component = Volume.IncidentLight;
            var value = default(MvIncidentLightState);
            var active = activeOrder >= 0 &&
                _state.IncidentLights.TryGetValue(activeOrder, out value);
            component.SetActive(active);
            component.incidentLightUse.value = active;
            if (!active)
            {
                component.incidentLightLength.value = 0f;
                return;
            }
            component.incidentLightAddBlend.value = value.Type;
            component.incidentLightColor.value = value.Color;
            component.incidentLightPosition.value = value.Position;
            component.incidentLightLength.value = value.Length;
        }

        private void SynchronizeLightOverlay()
        {
            var active = AnyEnabled(MvPostEffectKind.LightOverlay, 0, 1, 2, 3);
            var component = Volume.LightOverlay;
            component.SetActive(active);
            if (!active)
            {
                return;
            }
            component.brightColor.value = _state.LightOverlayBrightColor;
            component.darkColor.value = _state.LightOverlayDarkColor;
            component.brightPosition.value = _state.LightOverlayBrightPosition;
            component.darkPosition.value = _state.LightOverlayDarkPosition;
        }

        private void SynchronizeSaturation()
        {
            var active = _state.IsEnabled(MvPostEffectKind.Saturation, 0);
            Volume.Saturation.SetActive(active);
            Volume.Saturation.saturation.value = active ? _state.Saturation : 1f;
        }

        private void SynchronizeSaturationBlur()
        {
            var active = AnyEnabled(MvPostEffectKind.SaturationBlur, 0, 1);
            var component = Volume.SaturationBlur;
            component.SetActive(active);
            component.saturationBlurType.value =
                _state.IsEnabled(MvPostEffectKind.SaturationBlur, 1)
                    ? SaturationBlurVolumeType.V2
                    : SaturationBlurVolumeType.V1;
            component.saturation.value = active ? _state.SaturationBlurSat : 1f;
            component.alpha.value = active ? _state.SaturationBlurAlpha : 0f;
        }

        private void SynchronizeScreenDistortion()
        {
            var active = AnyEnabled(MvPostEffectKind.ScreenDistortion, 0, 1, 2, 3);
            var component = Volume.ScreenDistortion;
            component.SetActive(active);
            component.IntensityAmount.value = active ? _state.ScreenDistortionIntensity : 0f;
            component.ScaleAmount.value = active ? _state.ScreenDistortionScale : 1f;
            component.OffsetAmount.value = active ? _state.ScreenDistortionOffset : 0f;
            component.UseNoise.value = active && _state.ScreenDistortionUseNoise;
            component.NoiseScale.value = _state.ScreenDistortionNoiseScale;
            component.NoiseScrollSpeed.value = _state.ScreenDistortionUvScrollSpeed;
            component.Time.value = active ? _state.ScreenDistortionTime : 0f;
            if (active && component.UseNoise.value &&
                _state.ScreenDistortionNoiseTexture != null)
            {
                Shader.SetGlobalTexture(
                    ScreenDistortionNoiseTextureId,
                    _state.ScreenDistortionNoiseTexture);
            }
        }

        private void SynchronizeSekaiDof()
        {
            var active = AnyEnabled(MvPostEffectKind.SekaiDof, 0, 1, 2);
            var component = Volume.SekaiDof;
            component.SetActive(active);
            if (!active)
            {
                return;
            }
            component.aperture.value = _state.DofAperture;
            component.focalLength.value = _state.DofFocalLength;
            component.disableForeBokeh.value = _state.DofDisableForeBokeh;
            if (parameterTransform != null)
            {
                component.focusDistance.value = parameterTransform.localScale.x;
            }

            Shader.SetGlobalVector(
                CocParamsId,
                CalculateCocParameters(
                    component.focusDistance.value,
                    component.aperture.value,
                    component.focalLength.value));
        }

        public static Vector4 CalculateCocParameters(
            float focusDistance,
            float aperture,
            float focalLength)
        {
            var focalLengthMeters = focalLength / 1000f;
            var maxCoc = focalLength / aperture * focalLengthMeters /
                (focusDistance - focalLengthMeters);
            return new Vector4(focusDistance, maxCoc, 0f, 0f);
        }

        public static float CalculateVerticalFov(float verticalFov, float currentAspect)
        {
            if (!(currentAspect > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(currentAspect));
            }
            if (currentAspect >= TargetAspect)
            {
                return verticalFov;
            }

            var halfVertical = verticalFov * 0.5f * Mathf.Deg2Rad;
            var horizontal = 2f * Mathf.Atan(Mathf.Tan(halfVertical) * TargetAspect);
            return 2f * Mathf.Atan(Mathf.Tan(horizontal * 0.5f) / currentAspect) *
                Mathf.Rad2Deg;
        }

        private void SynchronizeCameraParameters()
        {
            if (CurrentCamera == null || parameterTransform == null)
            {
                return;
            }

            CurrentCamera.fieldOfView = CalculateVerticalFov(
                parameterTransform.localPosition.z * 100f,
                CurrentCamera.aspect);
        }

        private static Transform FindParameterTransform(Transform cameraTransform)
        {
            return cameraTransform?.parent?.Find("CamParam");
        }

        private void SynchronizeSolarisation()
        {
            var active = _state.IsEnabled(MvPostEffectKind.Solarization, 0);
            Volume.Solarisation.SetActive(active);
            Volume.Solarisation.solarisation.value = active ? _state.Solarization : 0f;
        }

        private void SynchronizeVignette()
        {
            var active = _state.IsEnabled(MvPostEffectKind.Vignette, 0);
            var component = Volume.Vignette;
            component.SetActive(active);
            if (!active)
            {
                component.intensity.value = 0f;
                return;
            }
            component.color.value = _state.VignetteColor;
            component.center.value = _state.VignetteCenter;
            component.intensity.value = _state.VignetteIntensity;
            component.smoothness.value = _state.VignetteSmoothness;
            component.roundness.value = _state.VignetteRoundness;
        }

        private void SynchronizeLut()
        {
            var active = (_state.LutFrontTexture != null && _state.LutFrontBlend > 0f) ||
                (_state.LutBackTexture != null && _state.LutBackBlend > 0f);
            var component = Volume.Lut;
            component.SetActive(active);
            component.frontTex.value = _state.LutFrontTexture;
            component.backTex.value = _state.LutBackTexture;
            component.frontBlend.value = active ? _state.LutFrontBlend : 0f;
            component.backBlend.value = active ? _state.LutBackBlend : 0f;
            component.frontPoint.value = _state.LutFrontPosition;
            component.frontNonPoint.value = _state.LutFrontNonLutPosition;
            component.backPoint.value = _state.LutBackPosition;
            component.backNonPoint.value = _state.LutBackNonLutPosition;
        }

        private bool AnyEnabled(MvPostEffectKind kind, params int[] paramTypes)
        {
            foreach (var paramType in paramTypes)
            {
                if (_state.IsEnabled(kind, paramType))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
