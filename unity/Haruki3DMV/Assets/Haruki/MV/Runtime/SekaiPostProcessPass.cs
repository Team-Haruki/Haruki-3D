using System;
using System.Collections.Generic;
using Sekai.Rendering.Components;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ChromaticAberration = sekai_rendering.Runtime.Components.ChromaticAberration;

namespace Sekai.Rendering.PostPrcessV2
{
    // The misspelt namespace is the one serialized by the 6.7.0 player.
    public enum ProfileId
    {
        Blur = 0,
        SaturationBlur = 1,
        Dof = 2,
        Bloom = 3,
        Uber = 4,
        SMAA = 5,
        ScreenDistortion = 6,
    }

    /// <summary>
    /// Official Sekai post-process pass boundary. The recovered pass consumes
    /// the camera VolumeStack, keeps its blur/bloom work surface 256 pixels
    /// high, and leaves the final camera target at its selected video size.
    /// </summary>
    public sealed class SekaiPostProcessPass : ScriptableRenderPass, IDisposable
    {
        private const int PostHeight = 256;

        private static readonly ProfileId[] s_ExecutionOrder =
        {
            ProfileId.Blur,
            ProfileId.Dof,
            ProfileId.Bloom,
            ProfileId.SaturationBlur,
            ProfileId.ScreenDistortion,
            ProfileId.Uber,
            ProfileId.SMAA,
        };

        private SekaiBuffer _buffer;
        private RenderTextureDescriptor _descriptor;
        private RTHandle _source;
        private RTHandle _destination;
        private readonly MaterialLibrary _materials;
        private readonly TextureResources _textures;
        private readonly BloomExtension _bloomExtension;
        private Vector2Int _postSize;
        private RTHandle _blurHandle;
        private RTHandle _boxBlurHandle;
        private RTHandle _saturationHandle;
        private RTHandle _bloomHandle;
        private RTHandle _cocHandle;
        private RTHandle _dofHandle;
        private RTHandle _dofMidBackgroundHandle;
        private RTHandle _dofMidForegroundHandle;
        private RTHandle _dofLowBackgroundHandle;
        private RTHandle _dofLowForegroundHandle;
        private RTHandle _dofTmpMidHandle;
        private RTHandle _dofTmpLowHandle;
        private RTHandle _directionalBlurHandle;
        private RTHandle _smaaInputHandle;
        private RTHandle _smaaEdgeHandle;
        private RTHandle _smaaBlendHandle;
        private RTHandle _smaaStencilHandle;

        private static readonly int BloomTextureId = Shader.PropertyToID("_BloomTex");
        private static readonly int BloomBlendModeId = Shader.PropertyToID("_BloomBlendMode");
        private static readonly int SaturationTextureId = Shader.PropertyToID("_SatTex");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int SaturationBlurId = Shader.PropertyToID("_Sat");
        private static readonly int SaturationBlurAlphaId = Shader.PropertyToID("_SatAlpha");
        private static readonly int SolarisationId = Shader.PropertyToID("_Solarisation");
        private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        private static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        private static readonly int DarkPositionId = Shader.PropertyToID("_DarkPosition");
        private static readonly int LightVectorId = Shader.PropertyToID("_LightVector");
        private static readonly int IncidentLightColorId = Shader.PropertyToID("_IncidentLightColor");
        private static readonly int IncidentLightVectorId = Shader.PropertyToID("_IncidentLightVector");
        private static readonly int FadeId = Shader.PropertyToID("_FadeOutBeforeProp");
        private static readonly int FadeLerpId = Shader.PropertyToID("_FadeOutBeforePropLerp");
        private static readonly int FrontLutId = Shader.PropertyToID("_FrontLut");
        private static readonly int BackLutId = Shader.PropertyToID("_BackLut");
        private static readonly int FrontLutBlendId = Shader.PropertyToID("_FrontLutBlend");
        private static readonly int BackLutBlendId = Shader.PropertyToID("_BackLutBlend");
        private static readonly int FrontNonLutPositionId = Shader.PropertyToID("_FrontNonLutPosition");
        private static readonly int BackNonLutPositionId = Shader.PropertyToID("_BackNonLutPosition");
        private static readonly int FrontLutVectorId = Shader.PropertyToID("_FrontLutVector");
        private static readonly int BackLutVectorId = Shader.PropertyToID("_BackLutVector");
        private static readonly int HalfColumnId = Shader.PropertyToID("_HalfCol");
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int DirectionalBlurTextureId = Shader.PropertyToID("_DirBlurTex");
        private static readonly int DirectionalBlurStrengthId = Shader.PropertyToID("_DirBlurStrength");
        private static readonly int DirectionalBlurDirectionId = Shader.PropertyToID("_DirBlurDirection");
        private static readonly int DirectionalBlurCenterId = Shader.PropertyToID("_DirBlurCenterPosition");
        private static readonly int VignetteParams1Id = Shader.PropertyToID("_Vignette_Params1");
        private static readonly int VignetteParams2Id = Shader.PropertyToID("_Vignette_Params2");
        private static readonly int MetricsId = Shader.PropertyToID("_Metrics");
        private static readonly int AreaTextureId = Shader.PropertyToID("_AreaTexture");
        private static readonly int SearchTextureId = Shader.PropertyToID("_SearchTexture");
        private static readonly int BlendTextureId = Shader.PropertyToID("_BlendTexture");
        private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
        private static readonly int StencilMaskId = Shader.PropertyToID("_StencilMask");
        private static readonly int DepthTextureId = Shader.PropertyToID("_DepthTexture");
        private static readonly int BlurBackgroundMidTextureId =
            Shader.PropertyToID("_BlurBgMidTex");
        private static readonly int BlurForegroundMidTextureId =
            Shader.PropertyToID("_BlurFgMidTex");
        private static readonly int BlurForegroundLowTextureId =
            Shader.PropertyToID("_BlurFgLowTex");
        private static readonly int OffsetsId = Shader.PropertyToID("_Offsets");
        private static readonly int ChromaticAberrationParamTypeId =
            Shader.PropertyToID("_ChromaticAberrationParamType");
        private static readonly int ChromaticAberrationOffsetRId =
            Shader.PropertyToID("_ChromaticAberrationOffsetR");
        private static readonly int ChromaticAberrationOffsetGId =
            Shader.PropertyToID("_ChromaticAberrationOffsetG");
        private static readonly int ChromaticAberrationOffsetBId =
            Shader.PropertyToID("_ChromaticAberrationOffsetB");
        private static readonly int ChromaticAberrationScaleId =
            Shader.PropertyToID("_ChromaticAberrationScale");
        private static readonly int ScreenDistortionParamId =
            Shader.PropertyToID("_SekaiScreenDistortionParam");
        private static readonly int ScreenDistortionNoiseTextureParamId =
            Shader.PropertyToID("_SekaiScreenDistortionNoiseTextureParam");

        public SekaiPostProcessPass()
        {
            _materials = new MaterialLibrary();
            _textures = new TextureResources();
            _bloomExtension = new BloomExtension();
        }

        public static IReadOnlyList<ProfileId> ExecutionOrder => s_ExecutionOrder;

        public RenderTextureDescriptor Descriptor => _descriptor;

        public Vector2Int PostSize => _postSize;

        public bool HasOfficialShaderLibrary => _materials.IsComplete;

        public IReadOnlyList<string> MissingShaderNames => _materials.MissingShaderNames;

        public static Vector2Int CalculatePostSize(RenderTextureDescriptor descriptor)
        {
            if (descriptor.width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(descriptor.width));
            }
            if (descriptor.height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(descriptor.height));
            }

            return new Vector2Int(
                Mathf.RoundToInt((float)descriptor.width / descriptor.height * PostHeight),
                PostHeight);
        }

        public static Vector4 CalculateLightVector(
            Vector2 brightPosition,
            Vector2 darkPosition)
        {
            return CalculateDirectedEffectVector(
                brightPosition - darkPosition);
        }

        public static Vector4 CalculateIncidentLightVector(
            Vector2 position,
            float length,
            int blendMode)
        {
            return new Vector4(
                position.x,
                position.y,
                length > 0f ? 1f / (length * length) : 0f,
                blendMode);
        }

        public static Vector4 CalculateLutVector(
            Vector2 lutPoint,
            Vector2 nonLutPoint)
        {
            return CalculateDirectedEffectVector(lutPoint - nonLutPoint);
        }

        public static Vector2 CalculateLutSampling(int textureWidth)
        {
            if (textureWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(textureWidth));
            }

            return new Vector2(
                0.5f / textureWidth,
                (textureWidth - 1f) / textureWidth);
        }

        public static Vector4 CalculateDirectionalBlurVector(float degrees)
        {
            var radians = (180f - degrees) * Mathf.Deg2Rad;
            return new Vector4(
                Mathf.Cos(radians),
                Mathf.Sin(radians),
                0f,
                0f);
        }

        public static Vector4 CalculateScreenDistortionParameters(
            float intensity,
            float scale,
            float offset,
            bool useNoise)
        {
            return new Vector4(intensity, scale, offset, useNoise ? 1f : 0f);
        }

        public static Vector4 CalculateScreenDistortionNoiseParameters(
            Vector2 noiseScale,
            Vector2 noiseScrollSpeed,
            float time)
        {
            return new Vector4(
                noiseScale.x,
                noiseScale.y,
                noiseScrollSpeed.x * time,
                noiseScrollSpeed.y * time);
        }

        private static Vector4 CalculateDirectedEffectVector(Vector2 delta)
        {
            var length = delta.magnitude;
            return new Vector4(
                delta.x,
                delta.y,
                length,
                length > 0f ? 1f / (length * length) : 0f);
        }

        public void Setup(
            RenderPassEvent evt,
            SekaiBuffer buffer,
            RenderTextureDescriptor baseDescriptor,
            in RTHandle source,
            in RTHandle destination)
        {
            renderPassEvent = evt;
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _descriptor = baseDescriptor;
            _source = source;
            _destination = destination;
            _postSize = CalculatePostSize(baseDescriptor);
        }

        public override void OnCameraSetup(
            CommandBuffer cmd,
            ref RenderingData renderingData)
        {
            // The official body marks the pass as having completed camera
            // setup. Targets are provided by SekaiRenderer in Setup().
        }

        public override void Configure(
            CommandBuffer cmd,
            RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (_destination != null)
            {
                ConfigureTarget(_destination);
            }
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            if (_buffer == null || _source == null || _destination == null)
            {
                return;
            }

            // PostEffectV2 owns a camera-local Volume. URP updates the camera
            // stack before renderer features execute, matching the official
            // VolumeManager.instance.stack lookup in SekaiPostProcessPass.
            var stack = VolumeManager.instance.stack;
            if (stack == null)
            {
                return;
            }

            if (!_materials.IsComplete || !_textures.IsComplete)
            {
                return;
            }

            var lightOverlay = stack.GetComponent<LightOverlay>();
            var incidentLight = stack.GetComponent<IncidentLight>();
            var saturation = stack.GetComponent<Saturation>();
            var solarisation = stack.GetComponent<Solarisation>();
            var fade = stack.GetComponent<FadeOutBeforeProp>();
            var saturationBlur = stack.GetComponent<SaturationBlur>();
            var lut = stack.GetComponent<LegacyLut>();
            var bloom = stack.GetComponent<LegacyBloom>();
            var antialiasing = stack.GetComponent<Antialiasing>();
            var vignette = stack.GetComponent<Sekai.Rendering.Components.PostProcessV2.Vignette>();
            var directionalBlur = stack.GetComponent<DirectionalBlur>();
            var sekaiDof = stack.GetComponent<SekaiDof>();
            var screenDistortion = stack.GetComponent<ScreenDistortion>();
            var chromaticAberration = stack.GetComponent<ChromaticAberration>();

            AllocateTargets();
            var cmd = CommandBufferPool.Get("SekaiPostProcessPass");
            try
            {
                var camera = renderingData.cameraData.camera;
                var source = _buffer.SekaiBufferColorHandle ?? _source;
                var dofActive = sekaiDof != null && sekaiDof.IsActive();
                if (dofActive)
                {
                    source = UpdateSekaiDof(cmd, source, sekaiDof);
                }

                if (saturationBlur != null && saturationBlur.IsActive())
                {
                    if (!dofActive)
                    {
                        UpdateBlur(cmd, source);
                    }
                    UpdateSaturationBlur(cmd, saturationBlur);
                }

                if (bloom != null && bloom.IsActive())
                {
                    _bloomExtension.SetupBloom(
                        _buffer.SekaiBufferBrightnessHandle,
                        _bloomHandle);
                    _bloomExtension.Intensity = bloom.intensity.value;
                    _bloomExtension.Scatter = bloom.scatter.value;
                    _bloomExtension.Execute(
                        cmd,
                        camera,
                        _materials.Bloom,
                        _postSize.x,
                        _postSize.y);
                }

                UpdateUberMaterial(
                    lightOverlay,
                    incidentLight,
                    saturation,
                    solarisation,
                    fade,
                    saturationBlur,
                    lut,
                    bloom,
                    vignette,
                    directionalBlur,
                    screenDistortion,
                    chromaticAberration);

                var useSmaa = antialiasing != null &&
                    antialiasing.IsActive() &&
                    antialiasing.mode.value ==
                        AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var uberDestination = useSmaa ? _smaaInputHandle : _destination;
                BlitUber(cmd, source, uberDestination, directionalBlur);
                if (useSmaa)
                {
                    ExecuteSmaa(cmd, _smaaInputHandle, _destination, antialiasing);
                }
                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }

        private void AllocateTargets()
        {
            var postDescriptor = CreateColorDescriptor(_postSize.x, _postSize.y);
            ReAllocate(ref _blurHandle, postDescriptor, FilterMode.Bilinear, "_BlurTex");
            ReAllocate(ref _boxBlurHandle, postDescriptor, FilterMode.Bilinear, "_BoxBlurTex");
            ReAllocate(ref _saturationHandle, postDescriptor, FilterMode.Bilinear, "_SatTex");
            ReAllocate(ref _bloomHandle, postDescriptor, FilterMode.Bilinear, "_BloomTex");

            ReAllocate(
                ref _dofMidBackgroundHandle,
                postDescriptor,
                FilterMode.Bilinear,
                "_BlurBgMidTex");
            ReAllocate(
                ref _dofMidForegroundHandle,
                postDescriptor,
                FilterMode.Bilinear,
                "_BlurFgMidTex");
            ReAllocate(
                ref _dofTmpMidHandle,
                postDescriptor,
                FilterMode.Bilinear,
                "_BlurTmpMidTex");

            var lowDescriptor = CreateColorDescriptor(
                Mathf.Max(1, _postSize.x / 2),
                Mathf.Max(1, _postSize.y / 2));
            ReAllocate(
                ref _dofLowBackgroundHandle,
                lowDescriptor,
                FilterMode.Bilinear,
                "_BlurBgLowTex");
            ReAllocate(
                ref _dofLowForegroundHandle,
                lowDescriptor,
                FilterMode.Bilinear,
                "_BlurFgLowTex");
            ReAllocate(
                ref _dofTmpLowHandle,
                lowDescriptor,
                FilterMode.Bilinear,
                "_BlurTmpLowTex");

            var fullDescriptor = CreateColorDescriptor(_descriptor.width, _descriptor.height);
            ReAllocate(ref _cocHandle, fullDescriptor, FilterMode.Bilinear, "_ColorCoCTex");
            ReAllocate(ref _dofHandle, fullDescriptor, FilterMode.Bilinear, "_DofTex");
            ReAllocate(ref _directionalBlurHandle, fullDescriptor, FilterMode.Bilinear, "_DirBlurTex");
            ReAllocate(ref _smaaInputHandle, fullDescriptor, FilterMode.Bilinear, "_SekaiSmaaInput");
            ReAllocate(ref _smaaEdgeHandle, fullDescriptor, FilterMode.Bilinear, "_EdgeColorTexture");
            ReAllocate(ref _smaaBlendHandle, fullDescriptor, FilterMode.Point, "_BlendTexture");

            var stencilDescriptor = fullDescriptor;
            stencilDescriptor.graphicsFormat = GraphicsFormat.None;
            stencilDescriptor.depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;
            ReAllocate(ref _smaaStencilHandle, stencilDescriptor, FilterMode.Point, "_EdgeStencilTexture");
        }

        private RenderTextureDescriptor CreateColorDescriptor(int width, int height)
        {
            var descriptor = _descriptor;
            descriptor.width = width;
            descriptor.height = height;
            descriptor.depthBufferBits = 0;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private static void ReAllocate(
            ref RTHandle handle,
            RenderTextureDescriptor descriptor,
            FilterMode filterMode,
            string name)
        {
            RenderingUtils.ReAllocateIfNeeded(
                ref handle,
                descriptor,
                filterMode,
                TextureWrapMode.Clamp,
                false,
                1,
                0f,
                name);
        }

        private void UpdateBlur(CommandBuffer cmd, RTHandle source)
        {
            Blitter.BlitCameraTexture(cmd, source, _blurHandle, _materials.Uber, 0);
            Blitter.BlitCameraTexture(cmd, _blurHandle, _boxBlurHandle, _materials.BoxBlur, 0);
            Blitter.BlitCameraTexture(cmd, _boxBlurHandle, _blurHandle, _materials.BoxBlur, 1);
        }

        private RTHandle UpdateSekaiDof(
            CommandBuffer cmd,
            RTHandle source,
            SekaiDof component)
        {
            var material = _materials.SekaiDof;
            cmd.SetGlobalTexture(
                DepthTextureId,
                _buffer.SekaiBufferDepthHandle.nameID);

            // Pass 0 copies the signed CoC payload produced by the scene MRT
            // from _DepthBuffer.r into the full-resolution source alpha.
            Blitter.BlitCameraTexture(cmd, source, _cocHandle, material, 0);

            var texel = new Vector4(
                1f / _postSize.x,
                1f / _postSize.y,
                0f,
                0f);
            cmd.SetGlobalVector(OffsetsId, texel);
            var midTargets = new RenderTargetIdentifier[]
            {
                _dofMidBackgroundHandle.nameID,
                _dofMidForegroundHandle.nameID,
            };
            CoreUtils.SetRenderTarget(
                cmd,
                midTargets,
                BuiltinRenderTextureType.None,
                ClearFlag.Color,
                Color.clear);
            Blitter.BlitTexture(cmd, _cocHandle, Vector4.one, material, 1);

            var widthOverHeight = (float)_postSize.x / _postSize.y;
            var oneOverBaseSize = 1f / (_postSize.y * 2f);
            BlurDof(
                cmd,
                _dofMidBackgroundHandle,
                _dofTmpMidHandle,
                _dofMidBackgroundHandle,
                widthOverHeight,
                oneOverBaseSize,
                2);

            cmd.SetGlobalVector(
                OffsetsId,
                new Vector4(texel.x * 2f, texel.y * 2f, 0f, 0f));
            Blitter.BlitCameraTexture(
                cmd,
                _dofMidBackgroundHandle,
                _dofLowBackgroundHandle,
                material,
                4);
            BlurDof(
                cmd,
                _dofLowBackgroundHandle,
                _dofTmpLowHandle,
                _dofLowBackgroundHandle,
                widthOverHeight,
                oneOverBaseSize,
                3);

            // The official runtime reuses this background result as the
            // saturation-blur input surface.
            Blitter.BlitCameraTexture(
                cmd,
                _dofMidBackgroundHandle,
                _blurHandle,
                material,
                0);
            cmd.SetGlobalTexture(
                BlurBackgroundMidTextureId,
                _dofMidBackgroundHandle.nameID);

            if (component.disableForeBokeh.value)
            {
                Blitter.BlitCameraTexture(cmd, _cocHandle, _dofHandle, material, 8);
                return _dofHandle;
            }

            BlurDof(
                cmd,
                _dofMidForegroundHandle,
                _dofTmpMidHandle,
                _dofMidForegroundHandle,
                widthOverHeight,
                oneOverBaseSize,
                5);
            cmd.SetGlobalTexture(
                BlurForegroundMidTextureId,
                _dofMidForegroundHandle.nameID);

            cmd.SetGlobalVector(
                OffsetsId,
                new Vector4(texel.x * 2f, texel.y * 2f, 0f, 0f));
            Blitter.BlitCameraTexture(
                cmd,
                _dofMidForegroundHandle,
                _dofLowForegroundHandle,
                material,
                4);
            cmd.SetGlobalTexture(
                BlurForegroundLowTextureId,
                _dofLowForegroundHandle.nameID);
            BlurDof(
                cmd,
                _dofLowForegroundHandle,
                _dofTmpLowHandle,
                _dofLowForegroundHandle,
                widthOverHeight,
                oneOverBaseSize,
                6);
            Blitter.BlitCameraTexture(cmd, _cocHandle, _dofHandle, material, 7);
            return _dofHandle;
        }

        private void BlurDof(
            CommandBuffer cmd,
            RTHandle source,
            RTHandle temporary,
            RTHandle destination,
            float widthOverHeight,
            float oneOverBaseSize,
            int pass)
        {
            const float radius = 1.75f;
            cmd.SetGlobalVector(
                OffsetsId,
                new Vector4(0f, oneOverBaseSize * radius, 0f, 0f));
            Blitter.BlitCameraTexture(
                cmd,
                source,
                temporary,
                _materials.SekaiDof,
                pass);
            cmd.SetGlobalVector(
                OffsetsId,
                new Vector4(
                    oneOverBaseSize * radius / widthOverHeight,
                    0f,
                    0f,
                    0f));
            Blitter.BlitCameraTexture(
                cmd,
                temporary,
                destination,
                _materials.SekaiDof,
                pass);
        }

        private void UpdateSaturationBlur(CommandBuffer cmd, SaturationBlur component)
        {
            _materials.Uber.SetFloat(SaturationBlurId, component.saturation.value);
            _materials.Uber.SetFloat(SaturationBlurAlphaId, component.alpha.value);
            Blitter.BlitCameraTexture(
                cmd,
                _blurHandle,
                _saturationHandle,
                _materials.Uber,
                2);
        }

        private void UpdateUberMaterial(
            LightOverlay lightOverlay,
            IncidentLight incidentLight,
            Saturation saturation,
            Solarisation solarisation,
            FadeOutBeforeProp fade,
            SaturationBlur saturationBlur,
            LegacyLut lut,
            LegacyBloom bloom,
            Sekai.Rendering.Components.PostProcessV2.Vignette vignette,
            DirectionalBlur directionalBlur,
            ScreenDistortion screenDistortion,
            ChromaticAberration chromaticAberration)
        {
            var material = _materials.Uber;
            SetKeyword(material, "_LIGHT_OVERLAY", lightOverlay != null && lightOverlay.IsActive());
            if (lightOverlay != null && lightOverlay.IsActive())
            {
                material.SetColor(DarkColorId, lightOverlay.darkColor.value);
                material.SetColor(BrightColorId, lightOverlay.brightColor.value);
                material.SetVector(DarkPositionId, lightOverlay.darkPosition.value);
                material.SetVector(
                    LightVectorId,
                    CalculateLightVector(
                        lightOverlay.brightPosition.value,
                        lightOverlay.darkPosition.value));
            }

            SetKeyword(material, "_INCIDENT_LIGHT", incidentLight != null && incidentLight.IsActive());
            if (incidentLight != null && incidentLight.IsActive())
            {
                material.SetColor(IncidentLightColorId, incidentLight.incidentLightColor.value);
                material.SetVector(
                    IncidentLightVectorId,
                    CalculateIncidentLightVector(
                        incidentLight.incidentLightPosition.value,
                        incidentLight.incidentLightLength.value,
                        incidentLight.incidentLightAddBlend.value));
            }

            var saturationActive = saturation != null && saturation.IsActive();
            SetKeyword(material, "_SATURATION", saturationActive);
            material.SetFloat(SaturationId, saturationActive ? saturation.saturation.value : 1f);
            material.SetFloat(
                SolarisationId,
                solarisation != null && solarisation.IsActive()
                    ? solarisation.solarisation.value
                    : 0f);
            material.SetFloat(
                FadeId,
                fade != null && fade.IsActive()
                    ? fade.fadeOutBeforeProp.value * 2f - 1f
                    : 0f);
            material.SetFloat(
                FadeLerpId,
                fade != null && fade.IsActive()
                    ? fade.fadeOutBeforePropLerp.value * 2f - 1f
                    : 0f);

            var saturationBlurActive = saturationBlur != null && saturationBlur.IsActive();
            SetKeyword(
                material,
                "_SATURATION_BLUR_V1",
                saturationBlurActive &&
                    saturationBlur.saturationBlurType.value == SaturationBlurVolumeType.V1);
            SetKeyword(
                material,
                "_SATURATION_BLUR_V2",
                saturationBlurActive &&
                    saturationBlur.saturationBlurType.value == SaturationBlurVolumeType.V2);
            if (saturationBlurActive)
            {
                material.SetTexture(SaturationTextureId, _saturationHandle.rt);
                material.SetFloat(SaturationBlurAlphaId, saturationBlur.alpha.value);
            }

            var bloomActive = bloom != null && bloom.IsActive();
            SetKeyword(material, "_BLOOM", bloomActive);
            if (bloomActive)
            {
                material.SetTexture(BloomTextureId, _bloomHandle.rt);
                material.SetFloat(BloomBlendModeId, bloom.useNewBlend.value ? 1f : 0f);
            }

            UpdateLut(material, lut);
            UpdateVignette(material, vignette);
            UpdateDirectionalBlur(material, directionalBlur);
            UpdateScreenDistortion(material, screenDistortion);
            UpdateChromaticAberration(material, chromaticAberration);
        }

        private static void UpdateScreenDistortion(
            Material material,
            ScreenDistortion component)
        {
            var active = component != null && component.IsActive();
            material.SetVector(
                ScreenDistortionParamId,
                active
                    ? CalculateScreenDistortionParameters(
                        component.IntensityAmount.value,
                        component.ScaleAmount.value,
                        component.OffsetAmount.value,
                        component.UseNoise.value)
                    : Vector4.zero);
            material.SetVector(
                ScreenDistortionNoiseTextureParamId,
                active && component.UseNoise.value
                    ? CalculateScreenDistortionNoiseParameters(
                        component.NoiseScale.value,
                        component.NoiseScrollSpeed.value,
                        component.Time.value)
                    : Vector4.zero);
        }

        private static void UpdateChromaticAberration(
            Material material,
            ChromaticAberration component)
        {
            var active = component != null && component.IsActive();
            SetKeyword(material, "_CHROMATIC_ABERRATION", active);
            if (!active)
            {
                return;
            }

            material.SetInt(
                ChromaticAberrationParamTypeId,
                component.ChromaticAberrationMode.value);
            material.SetVector(
                ChromaticAberrationOffsetRId,
                component.OffsetR.value);
            material.SetVector(
                ChromaticAberrationOffsetGId,
                component.OffsetG.value);
            material.SetVector(
                ChromaticAberrationOffsetBId,
                component.OffsetB.value);
            material.SetVector(
                ChromaticAberrationScaleId,
                new Vector4(
                    component.ScaleR.value,
                    component.ScaleG.value,
                    component.ScaleB.value,
                    0f));
        }

        private static void UpdateLut(Material material, LegacyLut lut)
        {
            var front = lut?.frontTex.value as Texture3D;
            var back = lut?.backTex.value as Texture3D;
            var active = lut != null && lut.IsActive() && front != null && back != null;
            SetKeyword(material, "_COLOR_LUT", active);
            if (!active)
            {
                return;
            }

            material.SetTexture(FrontLutId, front);
            material.SetTexture(BackLutId, back);
            material.SetFloat(FrontLutBlendId, lut.frontBlend.value);
            material.SetFloat(BackLutBlendId, lut.backBlend.value);
            material.SetVector(FrontNonLutPositionId, lut.frontNonPoint.value);
            material.SetVector(BackNonLutPositionId, lut.backNonPoint.value);
            material.SetVector(
                FrontLutVectorId,
                CalculateLutVector(lut.frontPoint.value, lut.frontNonPoint.value));
            material.SetVector(
                BackLutVectorId,
                CalculateLutVector(lut.backPoint.value, lut.backNonPoint.value));
            var sampling = CalculateLutSampling(front.width);
            material.SetFloat(HalfColumnId, sampling.x);
            material.SetFloat(ThresholdId, sampling.y);
        }

        private static void UpdateVignette(
            Material material,
            Sekai.Rendering.Components.PostProcessV2.Vignette vignette)
        {
            if (vignette == null || !vignette.IsActive())
            {
                material.SetVector(VignetteParams1Id, new Vector4(0f, 0f, 0f, 1f));
                material.SetVector(VignetteParams2Id, new Vector4(0.5f, 0.5f, 0f, 1f));
                return;
            }

            var color = vignette.color.value;
            var center = vignette.center.value;
            material.SetVector(
                VignetteParams1Id,
                new Vector4(color.r, color.g, color.b, vignette.roundness.value));
            material.SetVector(
                VignetteParams2Id,
                new Vector4(
                    center.x,
                    center.y,
                    vignette.intensity.value,
                    vignette.smoothness.value));
        }

        private static void UpdateDirectionalBlur(
            Material material,
            DirectionalBlur directionalBlur)
        {
            var active = directionalBlur != null && directionalBlur.IsActive();
            var radial = active && directionalBlur.blurType.value == 1;
            SetKeyword(material, "_DIRECTIONAL_BLUR", active && !radial);
            SetKeyword(material, "_RADIAL_BLUR", radial);
            if (!active)
            {
                return;
            }

            material.SetFloat(DirectionalBlurStrengthId, directionalBlur.blurStrength.value);
            material.SetVector(
                DirectionalBlurDirectionId,
                CalculateDirectionalBlurVector(directionalBlur.blurDirection.value));
            material.SetVector(DirectionalBlurCenterId, directionalBlur.blurCenterPosition.value);
        }

        private void BlitUber(
            CommandBuffer cmd,
            RTHandle source,
            RTHandle destination,
            DirectionalBlur directionalBlur)
        {
            if (directionalBlur == null || !directionalBlur.IsActive())
            {
                Blitter.BlitCameraTexture(cmd, source, destination, _materials.Uber, 1);
                return;
            }

            Blitter.BlitCameraTexture(
                cmd,
                source,
                _directionalBlurHandle,
                _materials.Uber,
                3);
            _materials.Uber.SetTexture(
                DirectionalBlurTextureId,
                _directionalBlurHandle.rt);
            Blitter.BlitCameraTexture(
                cmd,
                _directionalBlurHandle,
                destination,
                _materials.Uber,
                4);
        }

        private void ExecuteSmaa(
            CommandBuffer cmd,
            RTHandle source,
            RTHandle destination,
            Antialiasing antialiasing)
        {
            var material = _materials.SubpixelMorphologicalAntialiasing;
            material.SetVector(
                MetricsId,
                new Vector4(
                    1f / _descriptor.width,
                    1f / _descriptor.height,
                    _descriptor.width,
                    _descriptor.height));
            material.SetTexture(AreaTextureId, _textures.AreaTexture);
            material.SetTexture(SearchTextureId, _textures.SearchTexture);
            material.SetFloat(StencilRefId, 64f);
            material.SetFloat(StencilMaskId, 64f);
            material.DisableKeyword("_SMAA_PRESET_LOW");
            material.DisableKeyword("_SMAA_PRESET_MEDIUM");
            material.DisableKeyword("_SMAA_PRESET_HIGH");
            switch (antialiasing.antialiasingQuality.value)
            {
                case AntialiasingQuality.Low:
                    material.EnableKeyword("_SMAA_PRESET_LOW");
                    break;
                case AntialiasingQuality.Medium:
                    material.EnableKeyword("_SMAA_PRESET_MEDIUM");
                    break;
                default:
                    material.EnableKeyword("_SMAA_PRESET_HIGH");
                    break;
            }

            var pixelRect = new Rect(0f, 0f, _descriptor.width, _descriptor.height);
            CoreUtils.SetRenderTarget(
                cmd,
                _smaaEdgeHandle,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                _smaaStencilHandle,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                ClearFlag.ColorStencil,
                Color.clear);
            cmd.SetViewport(pixelRect);
            Blitter.BlitTexture(cmd, source, Vector2.one, material, 0);

            CoreUtils.SetRenderTarget(
                cmd,
                _smaaBlendHandle,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                ClearFlag.Color,
                Color.clear);
            cmd.SetViewport(pixelRect);
            Blitter.BlitTexture(cmd, _smaaEdgeHandle, Vector2.one, material, 1);
            cmd.SetGlobalTexture(BlendTextureId, _smaaBlendHandle.nameID);
            Blitter.BlitCameraTexture(cmd, source, destination, material, 2);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        public void Dispose()
        {
            _bloomExtension.Dispose();
            Release(ref _blurHandle);
            Release(ref _boxBlurHandle);
            Release(ref _saturationHandle);
            Release(ref _bloomHandle);
            Release(ref _cocHandle);
            Release(ref _dofHandle);
            Release(ref _dofMidBackgroundHandle);
            Release(ref _dofMidForegroundHandle);
            Release(ref _dofLowBackgroundHandle);
            Release(ref _dofLowForegroundHandle);
            Release(ref _dofTmpMidHandle);
            Release(ref _dofTmpLowHandle);
            Release(ref _directionalBlurHandle);
            Release(ref _smaaInputHandle);
            Release(ref _smaaEdgeHandle);
            Release(ref _smaaBlendHandle);
            Release(ref _smaaStencilHandle);
            _materials.Dispose();
            _textures.Dispose();
            _buffer = null;
            _source = null;
            _destination = null;
        }

        private static void Release(ref RTHandle handle)
        {
            handle?.Release();
            handle = null;
        }
    }

    public sealed class MaterialLibrary : IDisposable
    {
        private static readonly string[] s_ShaderNames =
        {
            "Hidden/Sekai/V2/UberPost",
            "Hidden/CP/PostEffect/BoxBlur",
            "Hidden/SekaiRP/PostEffect/Bloom",
            "Hidden/Sekai/V2/SekaiDepthOfField",
            "Hidden/Sekai/SubpixelMorphologicalAntialiasing",
        };

        private readonly Material[] _materials;
        private readonly string[] _missingShaderNames;

        public MaterialLibrary()
        {
            _materials = new Material[s_ShaderNames.Length];
            var missing = new List<string>();
            for (var index = 0; index < s_ShaderNames.Length; index++)
            {
                var shader = Shader.Find(s_ShaderNames[index]);
                if (shader == null)
                {
                    missing.Add(s_ShaderNames[index]);
                    continue;
                }
                _materials[index] = CoreUtils.CreateEngineMaterial(shader);
            }
            _missingShaderNames = missing.ToArray();
        }

        public static IReadOnlyList<string> ShaderNames => s_ShaderNames;

        public Material Uber => _materials[0];

        public Material BoxBlur => _materials[1];

        public Material Bloom => _materials[2];

        public Material SekaiDof => _materials[3];

        public Material SubpixelMorphologicalAntialiasing => _materials[4];

        public bool IsComplete => _missingShaderNames.Length == 0;

        public IReadOnlyList<string> MissingShaderNames => _missingShaderNames;

        public void Dispose()
        {
            foreach (var material in _materials)
            {
                CoreUtils.Destroy(material);
            }
        }
    }

    public sealed class TextureResources : IDisposable
    {
        public const string AreaTexturePath = "Textures/SMAA/AreaTex";
        public const string SearchTexturePath = "Textures/SMAA/SearchTex";

        private readonly Texture2D _areaTexture;
        private readonly Texture2D _searchTexture;

        public TextureResources()
        {
            _areaTexture = Resources.Load<Texture2D>(AreaTexturePath);
            _searchTexture = Resources.Load<Texture2D>(SearchTexturePath);
        }

        public bool IsComplete => _areaTexture != null && _searchTexture != null;

        public Texture2D AreaTexture => _areaTexture;

        public Texture2D SearchTexture => _searchTexture;

        public void Dispose()
        {
            // Resources owns these textures; the official Cleanup only clears
            // the retained references rather than destroying bundle assets.
        }
    }
}
