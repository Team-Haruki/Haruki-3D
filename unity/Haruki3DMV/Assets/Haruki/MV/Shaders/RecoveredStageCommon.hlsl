#ifndef HARUKI_RECOVERED_STAGE_COMMON_INCLUDED
#define HARUKI_RECOVERED_STAGE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "SekaiFlipBookProjector.hlsl"

TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
TEXTURE2D(_ColorTex);    SAMPLER(sampler_ColorTex);
TEXTURE2D(_LightMapTex); SAMPLER(sampler_LightMapTex);
TEXTURE2D(_BgTex);       SAMPLER(sampler_BgTex);
TEXTURE2D(_SubTex);      SAMPLER(sampler_SubTex);
TEXTURE2D(_EmissionTex); SAMPLER(sampler_EmissionTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _ColorTex_ST;
float4 _LightMapTex_ST;
float4 _BgTex_ST;
float4 _SubTex_ST;
float4 _EmissionTex_ST;
float4 _Color;
float4 _BaseColor;
float4 _EmissionColor;
float4 _LightColor;
float4 _FadeColor;
float4 _SheetValue;
float _Intensity;
float _Brightness;
float _Fade;
float _LocalTime;
float _Cutoff;
float _AlphaClipOn;
float _RotateTheta;
float _RotateOffset;
float _BloomWrite;
float _BloomScale;
float _LightIntensityMin;
float _LightIntensityMax;
CBUFFER_END

float4 _CoCParams;
float4 _SekaiAmbientLightColor;
float _SekaiLightIntensity;
float _SekaiGlowLightIntensity;
float _SekaiAllLightIntensity;
float4 _SekaiFogColor;
float4 _SekaiFogFactor;
float4 _SekaiGlobalSpotLightPos;
float4 _SekaiGlobalSpotLightColor;
float _SekaiGlobalSpotLightRadiusNear;
float _SekaiGlobalSpotLightRadiusFar;
float _SekaiGlobalSpotLightEnabled;
float4 _FlipBookColor_Stage;
float _FlipBookScale_Stage;
float2 _FlipBookUVScroll_Stage;
float _FlipBookMaskThreshold_Stage;
float _FlipBookMaskFallOff_Stage;
float _FlipBookUpperDotMaskOpacity_Stage;
float _FlipBookLowerDotMaskOpacity_Stage;

struct RecoveredStageAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float4 color : COLOR;
};

struct RecoveredStageVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    float fog : TEXCOORD3;
    float4 clipPosition : TEXCOORD4;
    float2 lightUv : TEXCOORD5;
    float flipBookOrientation : TEXCOORD6;
};

struct RecoveredStageMrt
{
    half4 color : SV_Target0;
    half4 depth : SV_Target1;
    half4 brightness : SV_Target2;
};

float RecoveredStageFogFactor(float4 clipPosition)
{
    float eyeDepth = max(LinearEyeDepth(
        clipPosition.z / clipPosition.w, _ZBufferParams), 0.0);
    return saturate(-eyeDepth * _SekaiFogFactor.y + _SekaiFogFactor.x);
}

RecoveredStageVaryings RecoveredStageVert(RecoveredStageAttributes input)
{
    RecoveredStageVaryings output;
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.clipPosition = position.positionCS;
    output.positionWS = position.positionWS;
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.flipBookOrientation = SekaiFlipBookOrientation(
        normalWS.y,
        _FlipBookUpperDotMaskOpacity_Stage,
        _FlipBookLowerDotMaskOpacity_Stage,
        _FlipBookMaskThreshold_Stage,
        _FlipBookMaskFallOff_Stage);
    output.uv = input.uv;
    output.lightUv = input.uv1;
    output.color = input.color;
    output.fog = RecoveredStageFogFactor(position.positionCS);
    return output;
}

RecoveredStageMrt RecoveredStagePack(
    RecoveredStageVaryings input,
    float4 color,
    float3 brightness)
{
    RecoveredStageMrt output;
    output.color = color;
    float eyeDepth = max(LinearEyeDepth(
        input.clipPosition.z / input.clipPosition.w, _ZBufferParams), 1e-5);
    float coc = clamp((1.0 - _CoCParams.x * rcp(eyeDepth)) * _CoCParams.y, -1.0, 1.0);
    output.depth = half4((coc + 1.0) * 0.5, 0.0, 0.0, 1.0);
    output.brightness = half4(brightness, 1.0);
    return output;
}

float4 RecoveredStageBase(RecoveredStageVaryings input)
{
    float theta = radians(_RotateTheta);
    float cosine = cos(theta);
    float sine = sin(theta);
    float2 centered = input.uv - _RotateOffset;
    float2 rotated = float2(
        dot(centered, float2(cosine, sine)),
        dot(centered, float2(-sine, cosine))) + _RotateOffset;
    float2 mainUv = rotated * _MainTex_ST.xy + _MainTex_ST.zw;
    float4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUv);
    return main * _Color * _BaseColor * input.color + _EmissionColor;
}

float3 RecoveredStageSpotLight(float3 color, float3 positionWS)
{
    if (_SekaiGlobalSpotLightEnabled <= 0.5) return color;
    float3 delta = _SekaiGlobalSpotLightPos.xyz - positionWS;
    float factor = saturate((dot(delta, delta) - _SekaiGlobalSpotLightRadiusFar)
        / min(_SekaiGlobalSpotLightRadiusNear - _SekaiGlobalSpotLightRadiusFar, -1e-5));
    factor = factor * factor * (3.0 - 2.0 * factor);
    return lerp(color, color * _SekaiGlobalSpotLightColor.rgb, factor);
}

float3 RecoveredStageFog(float3 color, float fog)
{
    return lerp(_SekaiFogColor.rgb, color,
        lerp(1.0, fog, saturate(_SekaiFogColor.a)));
}

float3 RecoveredStageAmbient(float3 color)
{
    float3 low = 4.0 * color * _SekaiAmbientLightColor.rgb;
    float3 high = 1.0 - 4.0 * (1.0 - 2.0 * color)
        * (1.0 - _SekaiAmbientLightColor.rgb);
    float3 overlaid = lerp(low, high,
        step(float3(0.25, 0.25, 0.25), color));
    return overlaid * (_SekaiLightIntensity * _SekaiAllLightIntensity);
}

float3 RecoveredStageStandardAmbient(float3 color)
{
    float3 low = 2.0 * color * _SekaiAmbientLightColor.rgb;
    float3 high = 1.0 - 2.0 * (1.0 - color)
        * (1.0 - _SekaiAmbientLightColor.rgb);
    return lerp(low, high, step(float3(0.5, 0.5, 0.5), color))
        * (_SekaiLightIntensity * _SekaiAllLightIntensity);
}

RecoveredStageMrt RecoveredStageColorMapFrag(RecoveredStageVaryings input)
{
    float2 mainUv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    float2 colorUv = input.lightUv * _ColorTex_ST.xy + _ColorTex_ST.zw;
    float4 surface = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUv)
        * SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, colorUv);
    surface.rgb = RecoveredStageStandardAmbient(surface.rgb);
    surface.rgb = RecoveredStageSpotLight(surface.rgb, input.positionWS);
    surface.rgb = RecoveredStageFog(surface.rgb, input.fog);
    return RecoveredStagePack(input, saturate(surface), 0.0);
}

RecoveredStageMrt RecoveredStageTextureFrag(RecoveredStageVaryings input)
{
    float2 mainUv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    float4 surface = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUv);
    surface.rgb = RecoveredStageStandardAmbient(surface.rgb);
    surface.rgb = RecoveredStageSpotLight(surface.rgb, input.positionWS);
    float fogWeight = (1.0 - input.fog) * saturate(_SekaiFogColor.a)
        * surface.a
        * max(surface.r, max(surface.g, surface.b));
    surface.rgb = lerp(surface.rgb, _SekaiFogColor.rgb, saturate(fogWeight));
    return RecoveredStagePack(input, saturate(surface), 0.0);
}

RecoveredStageMrt RecoveredStageOpaqueFrag(RecoveredStageVaryings input)
{
    float4 color = RecoveredStageBase(input);
    float2 lightUv = input.lightUv * _LightMapTex_ST.xy + _LightMapTex_ST.zw;
    float3 lightMap = SAMPLE_TEXTURE2D(
        _LightMapTex, sampler_LightMapTex, lightUv).rgb * _LightColor.rgb;
    color.rgb *= lightMap;
    if (_AlphaClipOn > 0.5) clip(color.a - _Cutoff);
    color.rgb = RecoveredStageAmbient(color.rgb);
    color.rgb = RecoveredStageSpotLight(color.rgb, input.positionWS);
    color.rgb = SekaiApplyFlipBookProjector(
        color.rgb,
        input.positionWS,
        input.flipBookOrientation,
        _FlipBookColor_Stage,
        _FlipBookScale_Stage,
        _FlipBookUVScroll_Stage);
    color.rgb = RecoveredStageFog(color.rgb, input.fog);
    color.a = saturate(color.a * 2.0);
    return RecoveredStagePack(input, color, color.rgb * max(_Intensity, 0.0));
}

RecoveredStageMrt RecoveredStageLightMapFragInternal(
    RecoveredStageVaryings input,
    bool alphaClip)
{
    float theta = radians(_RotateTheta);
    float cosine = cos(theta);
    float sine = sin(theta);
    float2 centered = input.uv - _RotateOffset;
    float2 rotated = float2(
        dot(centered, float2(cosine, sine)),
        dot(centered, float2(-sine, cosine))) + _RotateOffset;
    float2 mainUv = rotated * _MainTex_ST.xy + _MainTex_ST.zw;
    float2 lightUv = input.lightUv * _LightMapTex_ST.xy + _LightMapTex_ST.zw;
    float4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUv);
    if (alphaClip) clip(main.a - _Cutoff);
    float4 surface = main * input.color * SAMPLE_TEXTURE2D(
        _LightMapTex, sampler_LightMapTex, lightUv);
    float4 color = float4(
        RecoveredStageAmbient(surface.rgb),
        saturate(2.0 * surface.a));
    color.rgb = RecoveredStageSpotLight(color.rgb, input.positionWS);
    color.rgb = SekaiApplyFlipBookProjector(
        color.rgb,
        input.positionWS,
        input.flipBookOrientation,
        _FlipBookColor_Stage,
        _FlipBookScale_Stage,
        _FlipBookUVScroll_Stage);
    color.rgb = RecoveredStageFog(color.rgb, input.fog);
    color.rgb = saturate(color.rgb);
    return RecoveredStagePack(input, color, 0.0);
}

RecoveredStageMrt RecoveredStageLightMapFrag(RecoveredStageVaryings input)
{
    return RecoveredStageLightMapFragInternal(input, false);
}

RecoveredStageMrt RecoveredStageLightMapCutoutFrag(RecoveredStageVaryings input)
{
    return RecoveredStageLightMapFragInternal(input, true);
}

RecoveredStageMrt RecoveredStageLightMapTransparentFrag(RecoveredStageVaryings input)
{
    float2 mainUv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    float2 lightUv = input.lightUv * _LightMapTex_ST.xy + _LightMapTex_ST.zw;
    float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUv)
        * SAMPLE_TEXTURE2D(_LightMapTex, sampler_LightMapTex, lightUv)
        * (2.0 * input.color);
    color.rgb = RecoveredStageStandardAmbient(color.rgb);
    color.rgb = RecoveredStageSpotLight(color.rgb, input.positionWS);
    color.rgb = RecoveredStageFog(color.rgb, input.fog);
    return RecoveredStagePack(input, saturate(color), 0.0);
}

RecoveredStageMrt RecoveredStageTransparentFrag(RecoveredStageVaryings input)
{
    float4 color = RecoveredStageBase(input);
    if (_AlphaClipOn > 0.5) clip(color.a - _Cutoff);
    color *= _SekaiAllLightIntensity * max(_SekaiGlowLightIntensity, 1e-5);
    color.rgb = RecoveredStageSpotLight(color.rgb, input.positionWS);
    color.rgb = RecoveredStageFog(color.rgb, input.fog);
    return RecoveredStagePack(input, color, color.rgb * max(_Intensity, 0.0));
}

RecoveredStageMrt RecoveredStageEmissionFrag(RecoveredStageVaryings input)
{
    float theta = radians(_RotateTheta);
    float cosine = cos(theta);
    float sine = sin(theta);
    float2 centered = input.uv - _RotateOffset;
    float2 rotated = float2(
        dot(centered, float2(cosine, sine)),
        dot(centered, float2(-sine, cosine))) + _RotateOffset;
    float2 mainUv = rotated * _MainTex_ST.xy + _MainTex_ST.zw;
    float2 lightUv = input.lightUv * _LightMapTex_ST.xy + _LightMapTex_ST.zw;
    float2 emissionUv = input.uv * _EmissionTex_ST.xy + _EmissionTex_ST.zw;
    float4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUv);
    float4 surface = main * input.color * SAMPLE_TEXTURE2D(
        _LightMapTex, sampler_LightMapTex, lightUv);
    float4 color = float4(
        RecoveredStageAmbient(surface.rgb),
        saturate(2.0 * surface.a));
    float combinedLight = _SekaiLightIntensity * _SekaiAllLightIntensity;
    float emissionWeight = saturate(
        (combinedLight - _LightIntensityMin)
        / max(_LightIntensityMax - _LightIntensityMin, 1e-5));
    color.rgb += (1.0 - emissionWeight) * SAMPLE_TEXTURE2D(
        _EmissionTex, sampler_EmissionTex, emissionUv).rgb;
    color.rgb = RecoveredStageSpotLight(color.rgb, input.positionWS);
    color.rgb = RecoveredStageFog(color.rgb, input.fog);
    return RecoveredStagePack(input, saturate(color), 0.0);
}

RecoveredStageMrt RecoveredStageAdditiveFrag(RecoveredStageVaryings input)
{
    float4 color = RecoveredStageBase(input);
    float2 colorUv = input.uv * _ColorTex_ST.xy + _ColorTex_ST.zw;
    color *= SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, colorUv);
    color.rgb *= max(_Intensity, 1.0)
        * _SekaiAllLightIntensity * max(_SekaiGlowLightIntensity, 1e-5);
    color.rgb = RecoveredStageSpotLight(color.rgb, input.positionWS);
    return RecoveredStagePack(input, color,
        color.rgb * max(_BloomWrite * _BloomScale, 0.0));
}

RecoveredStageMrt RecoveredStageMonitorFrag(RecoveredStageVaryings input)
{
    float2 mainUv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    float2 bgUv = input.uv * _BgTex_ST.xy + _BgTex_ST.zw;
    float2 subUv = input.uv * _SubTex_ST.xy + _SubTex_ST.zw;
    float4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUv);
    float4 background = SAMPLE_TEXTURE2D(_BgTex, sampler_BgTex, bgUv);
    float4 sub = SAMPLE_TEXTURE2D(_SubTex, sampler_SubTex, subUv);
    float4 color = lerp(background, main, main.a);
    color = lerp(color, sub, sub.a * saturate(_SheetValue.z));
    color = lerp(color, _FadeColor, saturate(_Fade));
    color *= _BaseColor * _Color * max(_Brightness, 0.0);
    return RecoveredStagePack(input, color, color.rgb * max(_Intensity, 0.0));
}

#endif
