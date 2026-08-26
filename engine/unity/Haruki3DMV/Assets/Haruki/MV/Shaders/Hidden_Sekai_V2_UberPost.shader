Shader "Hidden/Sekai/V2/UberPost"
{
    Properties
    {
        [HideInInspector] _MainTex ("Source", 2D) = "black" {}
        [HideInInspector] _SatTex ("Saturation Blur", 2D) = "black" {}
        [HideInInspector] _BloomTex ("Bloom", 2D) = "black" {}
        [HideInInspector] _DirBlurTex ("Directional Blur", 2D) = "black" {}
        [HideInInspector] _ScreenDistortionNoiseTexture ("Distortion Noise", 2D) = "gray" {}
        [HideInInspector] _FrontLut ("Front LUT", 3D) = "" {}
        [HideInInspector] _BackLut ("Back LUT", 3D) = "" {}

        [HideInInspector] _DarkColor ("Dark Color", Color) = (0, 0, 0, 0)
        [HideInInspector] _BrightColor ("Bright Color", Color) = (0, 0, 0, 0)
        [HideInInspector] _DarkPosition ("Dark Position", Vector) = (0, 0, 0, 0)
        [HideInInspector] _LightVector ("Light Vector", Vector) = (0, 0, 0, 0)
        [HideInInspector] _IncidentLightColor ("Incident Light Color", Color) = (0, 0, 0, 0)
        [HideInInspector] _IncidentLightVector ("Incident Light Vector", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Saturation ("Saturation", Float) = 1
        [HideInInspector] _Solarisation ("Solarisation", Float) = 0
        [HideInInspector] _ChromaticAberrationParamType ("Chromatic Mode", Float) = 0
        [HideInInspector] _ChromaticAberrationOffsetR ("Chromatic R", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ChromaticAberrationOffsetG ("Chromatic G", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ChromaticAberrationOffsetB ("Chromatic B", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ChromaticAberrationScale ("Chromatic Scale", Vector) = (1, 1, 1, 0)
        [HideInInspector] _FadeOutBeforeProp ("Fade Out", Float) = 0
        [HideInInspector] _FadeOutBeforePropLerp ("Fade Out Lerp", Float) = 0
        [HideInInspector] _Sat ("Blur Saturation", Float) = 1
        [HideInInspector] _SatAlpha ("Blur Alpha", Float) = 0
        [HideInInspector] _BloomBlendMode ("Bloom Blend Mode", Float) = 0
        [HideInInspector] _FrontNonLutPosition ("Front Non-LUT Position", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FrontLutVector ("Front LUT Vector", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BackNonLutPosition ("Back Non-LUT Position", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BackLutVector ("Back LUT Vector", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FrontLutBlend ("Front LUT Blend", Float) = 0
        [HideInInspector] _BackLutBlend ("Back LUT Blend", Float) = 0
        [HideInInspector] _HalfCol ("LUT Half Column", Float) = 0
        [HideInInspector] _Threshold ("LUT Threshold", Float) = 1
        [HideInInspector] _DirBlurStrength ("Directional Blur Strength", Float) = 0
        [HideInInspector] _DirBlurDirection ("Directional Blur Direction", Vector) = (0, -1, 0, 0)
        [HideInInspector] _DirBlurCenterPosition ("Radial Blur Center", Vector) = (0.5, 0.5, 0, 0)
        [HideInInspector] _Vignette_Params1 ("Vignette Color Roundness", Vector) = (0, 0, 0, 1)
        [HideInInspector] _Vignette_Params2 ("Vignette Center Intensity Smoothness", Vector) = (0.5, 0.5, 0, 1)
        [HideInInspector] _SekaiScreenDistortionParam ("Screen Distortion", Vector) = (0, 0, 0, 0)
        [HideInInspector] _SekaiScreenDistortionNoiseTextureParam ("Screen Distortion Noise", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        // Pass indices and keyword names come from the JP 6.7.0 player.
        // The math below is transcribed from shader-0285..0303. In
        // particular, Overlay, saturation, solarisation, fade, vignette,
        // screen distortion and the 16-tap directional kernel retain the
        // constants emitted by the captured Vulkan programs.
        HLSLINCLUDE
        #pragma target 3.0
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_SatTex);
        SAMPLER(sampler_SatTex);
        TEXTURE2D(_BloomTex);
        SAMPLER(sampler_BloomTex);
        TEXTURE2D(_DirBlurTex);
        SAMPLER(sampler_DirBlurTex);
        TEXTURE2D(_ScreenDistortionNoiseTexture);
        SAMPLER(sampler_ScreenDistortionNoiseTexture);
        TEXTURE3D(_FrontLut);
        SAMPLER(sampler_FrontLut);
        TEXTURE3D(_BackLut);
        SAMPLER(sampler_BackLut);

        float4 _BlitTexture_TexelSize;
        float4 _DarkColor;
        float4 _BrightColor;
        float4 _DarkPosition;
        float4 _LightVector;
        float4 _IncidentLightColor;
        float4 _IncidentLightVector;
        float _Saturation;
        float _Solarisation;
        float _ChromaticAberrationParamType;
        float4 _ChromaticAberrationOffsetR;
        float4 _ChromaticAberrationOffsetG;
        float4 _ChromaticAberrationOffsetB;
        float4 _ChromaticAberrationScale;
        float _FadeOutBeforeProp;
        float _FadeOutBeforePropLerp;
        float _Sat;
        float _SatAlpha;
        float _BloomBlendMode;
        float4 _FrontNonLutPosition;
        float4 _FrontLutVector;
        float4 _BackNonLutPosition;
        float4 _BackLutVector;
        float _FrontLutBlend;
        float _BackLutBlend;
        float _HalfCol;
        float _Threshold;
        float _DirBlurStrength;
        float4 _DirBlurDirection;
        float4 _DirBlurCenterPosition;
        float4 _Vignette_Params1;
        float4 _Vignette_Params2;
        float4 _SekaiScreenDistortionParam;
        float4 _SekaiScreenDistortionNoiseTextureParam;

        float2 DistortUv(float2 uv)
        {
            if (_SekaiScreenDistortionParam.x <= 0.0)
            {
                return uv;
            }

            float wave = sin(uv.y * 2.0) * 0.5 + 0.5;
            wave = wave * 3.0 + 1.0;
            wave *= _SekaiScreenDistortionParam.y;
            float phase = wave * _SekaiScreenDistortionParam.z;
            float noisePhase = wave * uv.y;
            float3 noise = sin(float3(1.1, 2.3, 4.1) * noisePhase
                + phase
                + float3(0.3, 0.2, 0.1));
            float displacement = noise.x + noise.y * 0.5 + noise.z * 0.25;
            float2 noiseUv = float2(
                uv.x * (_ScreenParams.x / _ScreenParams.y),
                uv.y);
            noiseUv = noiseUv * _SekaiScreenDistortionNoiseTextureParam.xy
                + _SekaiScreenDistortionNoiseTextureParam.zw;
            float noiseTexture = SAMPLE_TEXTURE2D(
                _ScreenDistortionNoiseTexture,
                sampler_ScreenDistortionNoiseTexture,
                noiseUv).r * 2.0 - 1.0;
            float2 distorted = uv +
                (displacement + noiseTexture * 0.1)
                * _SekaiScreenDistortionParam.xx;
            return lerp(uv, distorted, _SekaiScreenDistortionParam.w);
        }

        half3 SampleSource(float2 uv)
        {
            #if defined(_CHROMATIC_ABERRATION)
                float2 center = uv - 0.5;
                float2 uvR = center * _ChromaticAberrationScale.x + 0.5
                    + _ChromaticAberrationOffsetR.xy;
                float2 uvG = center * _ChromaticAberrationScale.y + 0.5
                    + _ChromaticAberrationOffsetG.xy;
                float2 uvB = center * _ChromaticAberrationScale.z + 0.5
                    + _ChromaticAberrationOffsetB.xy;
                return half3(
                    SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvR, _BlitMipLevel).r,
                    SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvG, _BlitMipLevel).g,
                    SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvB, _BlitMipLevel).b);
            #else
                return SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv,
                    _BlitMipLevel).rgb;
            #endif
        }

        half3 Overlay(half3 source, half3 blend)
        {
            half3 low = 2.0h * source * blend;
            half3 high = 1.0h - 2.0h * (1.0h - source) * (1.0h - blend);
            return lerp(low, high, step(0.5h, source));
        }

        half3 ApplyLightOverlay(half3 color, float2 uv)
        {
            #if defined(_LIGHT_OVERLAY)
                float amount = saturate(
                    dot(_LightVector.xy, uv - _DarkPosition.xy)
                    * _LightVector.w);
                half3 overlay = lerp(
                    _DarkColor.rgb,
                    _BrightColor.rgb,
                    amount);
                color = Overlay(color, overlay);
            #endif
            return color;
        }

        half3 ApplyIncidentLight(half3 color, float2 uv)
        {
            #if defined(_INCIDENT_LIGHT)
                // Captured 6.7.0 program: xyz stores position and inverse
                // squared length; w is the integer add/multiply mode.
                float distanceFromOrigin = length(
                    uv - _IncidentLightVector.xy);
                float amount = saturate(
                    distanceFromOrigin * _IncidentLightVector.z);
                half mode = (half)_IncidentLightVector.w;
                half3 incident = lerp(
                    _IncidentLightColor.rgb,
                    mode.xxx,
                    amount);
                color = lerp(color + incident, color * incident, mode);
            #endif
            return color;
        }

        half3 ApplyBloom(half3 color, float2 uv)
        {
            #if defined(_BLOOM)
                half3 bloom = SAMPLE_TEXTURE2D(
                    _BloomTex,
                    sampler_BloomTex,
                    uv).rgb;
                if (_BloomBlendMode >= 0.5)
                {
                    color = color + bloom - color * bloom;
                }
                else
                {
                    color += bloom;
                }
                color = saturate(color);
            #endif
            return color;
        }

        half3 ApplyColorLut(half3 color, float2 uv)
        {
            #if defined(_COLOR_LUT)
                float3 lutUv = color * _Threshold + _HalfCol;
                half3 front = SAMPLE_TEXTURE3D(
                    _FrontLut,
                    sampler_FrontLut,
                    lutUv).rgb;
                half3 back = SAMPLE_TEXTURE3D(
                    _BackLut,
                    sampler_BackLut,
                    lutUv).rgb;

                float frontAmount = saturate(
                    dot(
                        _FrontLutVector.xy,
                        uv - _FrontNonLutPosition.xy)
                    * _FrontLutVector.w);
                color += _FrontLutBlend * frontAmount * (front - color);

                float backAmount = saturate(
                    dot(
                        _BackLutVector.xy,
                        uv - _BackNonLutPosition.xy)
                    * _BackLutVector.w);
                color += _BackLutBlend * backAmount * (back - color);
            #endif
            return color;
        }

        half3 ApplySaturationBlur(half3 color, float2 uv)
        {
            #if defined(_SATURATION_BLUR_V1)
                half3 blurred = SAMPLE_TEXTURE2D(
                    _SatTex,
                    sampler_SatTex,
                    uv).rgb;
                color = lerp(color, blurred, _SatAlpha);
            #elif defined(_SATURATION_BLUR_V2)
                half3 blurred = SAMPLE_TEXTURE2D(
                    _SatTex,
                    sampler_SatTex,
                    uv).rgb;
                half3 sourceTerm = color * color * _SatAlpha;
                half3 blurredTerm = blurred * blurred * _SatAlpha;
                color = max(
                    color,
                    sourceTerm + blurredTerm - sourceTerm * blurredTerm);
            #endif
            return color;
        }

        half3 ApplySaturation(half3 color, float saturation)
        {
            half luminance = dot(color, half3(0.299h, 0.587h, 0.114h));
            return lerp(luminance.xxx, color, saturation);
        }

        half3 ApplyFade(half3 color)
        {
            // UpdateFadeOutBeforeProp maps both serialized 0..1 values to
            // -1..1. The first is an additive pre-offset; the second selects
            // white (positive) or black (negative) and supplies its weight.
            color += _FadeOutBeforeProp;
            float fade = _FadeOutBeforePropLerp;
            half3 target = fade >= 0.0 ? 1.0h.xxx : 0.0h.xxx;
            return lerp(color, target, abs(fade));
        }

        half3 ApplyVignette(half3 color, float2 uv)
        {
            if (_Vignette_Params2.z <= 0.0)
            {
                return color;
            }

            float2 delta = abs(uv - _Vignette_Params2.xy)
                * _Vignette_Params2.z * 2.0;
            delta.x *= _Vignette_Params1.w;
            float vignette = pow(
                max(1.0 - dot(delta, delta), 0.0),
                _Vignette_Params2.w);
            return color * lerp(_Vignette_Params1.rgb, 1.0h.xxx, vignette);
        }

        half4 FragCopy(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = DistortUv(input.texcoord);
            return half4(SampleSource(uv), 1.0h);
        }

        half4 FragFinal(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = DistortUv(input.texcoord);
            half3 color = SampleSource(uv);
            color = ApplyBloom(color, uv);
            color = ApplyLightOverlay(color, input.texcoord);
            color = ApplyIncidentLight(color, input.texcoord);
            color = ApplySaturationBlur(color, uv);
            color = saturate(color);
            #if defined(_SATURATION)
                color = ApplySaturation(color, _Saturation);
            #endif
            color = lerp(color, 1.0h - color, _Solarisation);
            color = ApplyColorLut(color, input.texcoord);
            color = ApplyFade(color);
            color = ApplyVignette(color, input.texcoord);
            return half4(saturate(color), 1.0h);
        }

        half4 FragSaturationBlur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 color = SampleSource(input.texcoord);
            return half4(saturate(ApplySaturation(color, _Sat)), 1.0h);
        }

        half4 FragPreDirectionalBlur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return half4(SampleSource(DistortUv(input.texcoord)), 1.0h);
        }

        half4 FragDirectionalBlur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = DistortUv(input.texcoord);
            #if defined(_RADIAL_BLUR)
                // Radial mode is the captured one-sided 16-tap kernel.
                float2 direction =
                    (input.texcoord - _DirBlurCenterPosition.xy)
                    * _BlitTexture_TexelSize.xy;
                half3 color = 0.0h;
                [unroll]
                for (int index = 0; index < 16; index++)
                {
                    float t = index * 0.0625;
                    float weight = t - t * t;
                    color += SAMPLE_TEXTURE2D(
                        _DirBlurTex,
                        sampler_DirBlurTex,
                        uv - direction * t * _DirBlurStrength).rgb * weight;
                }
                color *= 0.3764705956h;
            #else
                // Directional mode uses eight symmetric pairs. CPU-side
                // UpdateDirectionalBlur supplies cos/sin(180-degree).
                float2 direction = _DirBlurDirection.xy
                    * _BlitTexture_TexelSize.xy;
                half3 color = 0.0h;
                [unroll]
                for (int index = 0; index < 8; index++)
                {
                    float t = index * 0.125;
                    float weight = t - t * t;
                    float2 offset = direction * t * _DirBlurStrength;
                    half3 negative = SAMPLE_TEXTURE2D(
                        _DirBlurTex,
                        sampler_DirBlurTex,
                        uv - offset).rgb;
                    half3 positive = SAMPLE_TEXTURE2D(
                        _DirBlurTex,
                        sampler_DirBlurTex,
                        uv + offset).rgb;
                    color += (negative + positive) * weight;
                }
                color *= 0.3809523880h;
            #endif
            #if defined(_SATURATION)
                color = ApplySaturation(color, _Saturation);
            #endif
            color = lerp(color, 1.0h - color, _Solarisation);
            color = ApplyFade(color);
            color = ApplyVignette(color, uv);
            return half4(saturate(color), 1.0h);
        }
        ENDHLSL

        Pass
        {
            Name "Copy"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy
            #pragma multi_compile_local_fragment _ _CHROMATIC_ABERRATION
            ENDHLSL
        }

        Pass
        {
            Name "Final"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFinal
            #pragma multi_compile_local_fragment _ _LIGHT_OVERLAY
            #pragma multi_compile_local_fragment _ _INCIDENT_LIGHT
            #pragma multi_compile_local_fragment _ _SATURATION
            #pragma multi_compile_local_fragment _ _CHROMATIC_ABERRATION
            #pragma multi_compile_local_fragment _ _COLOR_LUT
            #pragma multi_compile_local_fragment _ _BLOOM
            #pragma multi_compile_local_fragment _ _SATURATION_BLUR_V1 _SATURATION_BLUR_V2
            ENDHLSL
        }

        Pass
        {
            Name "Saturation Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSaturationBlur
            ENDHLSL
        }

        Pass
        {
            Name "Pre Directional Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPreDirectionalBlur
            #pragma multi_compile_local_fragment _ _CHROMATIC_ABERRATION
            ENDHLSL
        }

        Pass
        {
            Name "Directional Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDirectionalBlur
            #pragma multi_compile_local_fragment _ _DIRECTIONAL_BLUR _RADIAL_BLUR
            #pragma multi_compile_local_fragment _ _SATURATION
            ENDHLSL
        }
    }
}
