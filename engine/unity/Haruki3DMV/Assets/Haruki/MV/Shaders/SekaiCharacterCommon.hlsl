#ifndef HARUKI_SEKAI_CHARACTER_COMMON_INCLUDED
#define HARUKI_SEKAI_CHARACTER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "SekaiFlipBookProjector.hlsl"

TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
TEXTURE2D(_ShadowTex);      SAMPLER(sampler_ShadowTex);
TEXTURE2D(_ValueTex);       SAMPLER(sampler_ValueTex);
TEXTURE2D(_FaceShadowTex);  SAMPLER(sampler_FaceShadowTex);
TEXTURE2D(_EyelashMaskTex); SAMPLER(sampler_EyelashMaskTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _DefaultSkinColor;
float4 _Shadow1SkinColor;
float4 _Shadow2SkinColor;
float4 _PartsAmbientColor;
float4 _HeadDotDirectionalLightValues;
float4 _HeadPosition;
float4 _FaceFront;
float _ShadowTexWeight;
float _ShadowWidth;
float _FadeMode;
float _UseLambert;
float _UseValueTex;
float _UseFaceShadowLimiter;
float _RangeLimit;
float _FaceSdfMirror;
float _FaceSdfBias;
float _UseSkinColor;
float _SkinMaskMode;
float _FaceSphereShadowEdge;
float _FaceSphereShadowSmoothness;
float _FaceSphereShadowWeight;
float _FinalSat;
float _Brightness;
float _HighlightRolloff;
float _RimThreshold;
float _SpecularPower;
float _HueSinAngle;
float _HueCosAngle;
float _Saturation;
float _Value;
float _Contrast;
float _CharacterId;
float _FormationId;
float _Transparency;
float _Cutoff;
float _UseAlphaClip;
float _OutlineOffset;
float _UseEyelash;
float _IsLeftEyeClose;
float _IsRightEyeClose;
float _EyelashTransparent;
float _EyelashFaceCameraEdge1;
float _EyelashFaceCameraEdge2;
CBUFFER_END

float4 _SekaiDirectionalLight;
float4 _DirectionalLightVector;
float4 _SekaiShadowColor;
float _SekaiShadowThreshold;
float _SekaiAllLightIntensity;
float _SekaiCharacterDirectionalOverride;
float _SekaiCharacterUseFaceShadowLimiter;
float _SekaiCharacterRangeLimit;
float _SekaiCharacterShadowTexWeight;
float _SekaiCharacterShadowWidth;
float _SekaiCharacterFadeMode;
float _SekaiCharacterHueSinAngle;
float _SekaiCharacterHueCosAngle;
float _SekaiCharacterSaturation;
float _SekaiCharacterValue;
float _SekaiCharacterContrast;
float4 _SekaiFogColor;
float4 _SekaiFogFactor;
float4 _CoCParams;
float4 _SekaiOutlineWidth;
float4 _SekaiOutlineFactor;
float4 _SekaiCharacterAmbientLightColorArray[12];
float _SekaiCharacterAmbientLightIntensityArray[12];
float4 _SekaiCharacterSpecularColorArray[12];
float4 _SekaiCharacterOutlineColorArray[12];
float _SekaiCharacterOutlineBlendingArray[12];
float4 _SekaiCharacterRimLightDirectionArray[12];
float4 _SekaiCharacterRimLightColorArray[12];
float4 _SekaiCharacterRimLightShadowColorArray[12];
float4 _SekaiCharacterRimLightFactorArray[12];
float _SekaiCharacterRimLightShadowSharpnessArray[12];
float4 _FlipBookColor_Character;
float _FlipBookScale_Character;
float2 _FlipBookUVScroll_Character;
float _FlipBookMaskThreshold_Character;
float _FlipBookMaskFallOff_Character;
float _FlipBookUpperDotMaskOpacity_Character;
float _FlipBookLowerDotMaskOpacity_Character;

struct SekaiCharacterAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float4 color : COLOR;
};

struct SekaiCharacterVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 faceUv : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float3 positionWS : TEXCOORD3;
    float4 color : TEXCOORD4;
    float fog : TEXCOORD5;
    float4 clipPosition : TEXCOORD6;
    float eyelashFade : TEXCOORD7;
    float flipBookOrientation : TEXCOORD8;
};

struct SekaiCharacterMrt
{
    half4 color : SV_Target0;
    half4 depth : SV_Target1;
    half4 brightness : SV_Target2;
};

float SekaiSmooth01(float x)
{
    x = saturate(x);
    return x * x * (3.0 - 2.0 * x);
}

float SekaiCharacterFogFactor(float4 clipPosition)
{
    float eyeDepth = max(LinearEyeDepth(
        clipPosition.z / clipPosition.w, _ZBufferParams), 0.0);
    return saturate(-eyeDepth * _SekaiFogFactor.y + _SekaiFogFactor.x);
}

float SekaiShadowBand(float rawLight, float threshold, float width, float fadeMode)
{
    float t = saturate(threshold);
    float w = saturate(width);
    float q = fadeMode < 0.5
        ? saturate((rawLight - t * (1.0 - w)) / max(t * w, 1e-5))
        : saturate((rawLight - t) / max((1.0 - t) * w, 1e-5));
    return 1.0 - SekaiSmooth01(q);
}

float SekaiFaceShadowBand(float sdf, float threshold, float width, float fadeMode)
{
    float w = saturate(width);
    float q = fadeMode < 0.5
        ? saturate((threshold - sdf) / max((1.0 - sdf) * w, 1e-5))
        : saturate((sdf - threshold) / max((1.0 - threshold) * w, 1e-5));
    return fadeMode < 0.5 ? SekaiSmooth01(q) : 1.0 - SekaiSmooth01(q);
}

float3 SekaiApplyHsvc(float3 color)
{
    float overrideWeight = saturate(_SekaiCharacterDirectionalOverride);
    float hueSin = lerp(_HueSinAngle, _SekaiCharacterHueSinAngle, overrideWeight);
    float hueCos = lerp(_HueCosAngle, _SekaiCharacterHueCosAngle, overrideWeight);
    float saturation = lerp(_Saturation, _SekaiCharacterSaturation, overrideWeight);
    float value = lerp(_Value, _SekaiCharacterValue, overrideWeight);
    float contrast = lerp(_Contrast, _SekaiCharacterContrast, overrideWeight);
    const float3 axis = float3(0.577350269, 0.577350269, 0.577350269);
    float3 rotated = color * hueCos
        + cross(axis, color) * hueSin
        + axis * dot(axis, color) * (1.0 - hueCos);
    rotated = (rotated - 0.5) * (contrast * 2.0) + (value * 2.0 - 0.5);
    float luma = dot(rotated, float3(0.22, 0.707, 0.071));
    return (rotated - luma) * (saturation * 2.0) + luma;
}

float3 SekaiOverlay(float3 baseColor, float3 blendColor)
{
    float3 low = 2.0 * baseColor * blendColor;
    float3 high = 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor);
    return lerp(low, high, step(float3(0.5, 0.5, 0.5), baseColor));
}

int SekaiFormationIndex()
{
    float selected = _CharacterId >= 0.0 ? _CharacterId : _FormationId;
    return (int)clamp(floor(selected + 0.5), 0.0, 11.0);
}

float3 SekaiSkinRamp(float skinValue, float3 globalShadow)
{
    float3 mid = _Shadow1SkinColor.rgb * globalShadow;
    float3 dark = _Shadow2SkinColor.rgb * globalShadow;
    float3 upper = lerp(mid, _DefaultSkinColor.rgb, saturate(skinValue * 2.0 - 1.0));
    return lerp(dark, upper, saturate(skinValue * 2.0));
}

float3 SekaiLightDirection()
{
    float3 direction = dot(_SekaiDirectionalLight.xyz, _SekaiDirectionalLight.xyz) > 1e-6
        ? _SekaiDirectionalLight.xyz
        : _DirectionalLightVector.xyz;
    return normalize(direction + float3(1e-7, 0.0, 0.0));
}

SekaiCharacterVaryings SekaiCharacterVert(SekaiCharacterAttributes input)
{
    SekaiCharacterVaryings output;
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.clipPosition = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.flipBookOrientation = SekaiFlipBookOrientation(
        output.normalWS.y,
        _FlipBookUpperDotMaskOpacity_Character,
        _FlipBookLowerDotMaskOpacity_Character,
        _FlipBookMaskThreshold_Character,
        _FlipBookMaskFallOff_Character);
    output.uv = TRANSFORM_TEX(input.uv0, _MainTex);
    output.faceUv = dot(abs(input.uv1), float2(1.0, 1.0)) > 1e-6
        ? input.uv1 : input.uv0;
    output.color = input.color;
    output.fog = SekaiCharacterFogFactor(position.positionCS);
    float eyeFactor = input.positionOS.y <= 0.0
        ? lerp(_IsLeftEyeClose, 1.0, input.color.a)
        : lerp(_IsRightEyeClose, 1.0, input.color.a);
    float3 viewDirection = normalize(UNITY_MATRIX_V[2].xyz);
    float3 faceFront = normalize(_FaceFront.xyz + float3(1e-7, 0.0, 0.0));
    float angleFade = smoothstep(
        _EyelashFaceCameraEdge2,
        _EyelashFaceCameraEdge1,
        dot(viewDirection, faceFront));
    output.eyelashFade = eyeFactor * angleFade * _EyelashTransparent;
    return output;
}

SekaiCharacterVaryings SekaiCharacterOutlineVert(SekaiCharacterAttributes input)
{
    float3 worldPosition = TransformObjectToWorld(input.positionOS.xyz);
    float distanceFactor = saturate(
        (distance(worldPosition, GetCameraPositionWS()) - _SekaiOutlineFactor.x)
        * _SekaiOutlineFactor.y);
    distanceFactor = min(distanceFactor * _SekaiOutlineFactor.z, 1.0);
    float width = lerp(_SekaiOutlineWidth.x, _SekaiOutlineWidth.y, distanceFactor);
    float3 direction = normalize(input.normalOS);
#if defined(_OUTLINE_SECOND_NORMAL)
    float3 bitangent = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
    direction = normalize(
        input.tangentOS.xyz * input.uv1.x
        + bitangent * input.uv1.y
        + input.normalOS * input.uv2.x);
#endif
    input.positionOS.xyz += direction * width * input.color.r;
    SekaiCharacterVaryings output = SekaiCharacterVert(input);
    float4 projectedCameraOrigin = TransformWorldToHClip(GetCameraPositionWS());
    output.positionCS += projectedCameraOrigin * (-0.01 * _OutlineOffset) * input.color.b;
    return output;
}

float3 SekaiEvaluateToon(
    SekaiCharacterVaryings input,
    out float specularMask,
    out float3 brightnessContribution)
{
    float4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    if (_UseAlphaClip > 0.5) clip(mainSample.a - _Cutoff);
    float3 shadowSample = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, input.uv).rgb;
    float4 valueSample = SAMPLE_TEXTURE2D(_ValueTex, sampler_ValueTex, input.uv);
    float3 normalWS = normalize(input.normalWS);
    float3 lightDirection = SekaiLightDirection();
    float halfLambert = dot(normalWS, lightDirection) * 0.5 + 0.5;
    float baseLight = _UseLambert > 0.5 ? halfLambert : 1.0;
    float valueBias = _UseValueTex > 0.5 ? valueSample.b : 0.5;
    float rawLight = saturate(baseLight + 2.0 * valueBias - 1.0);
    float overrideWeight = saturate(_SekaiCharacterDirectionalOverride);
    float shadowWidth = lerp(_ShadowWidth, _SekaiCharacterShadowWidth, overrideWeight);
    float fadeMode = lerp(_FadeMode, _SekaiCharacterFadeMode, overrideWeight);
    float shadowTexWeight = lerp(
        _ShadowTexWeight, _SekaiCharacterShadowTexWeight, overrideWeight);
    float shadowBand = SekaiShadowBand(
        rawLight, _SekaiShadowThreshold, shadowWidth, fadeMode);

#if defined(_HAIR_SHADOW)
    float geometryBand = SekaiShadowBand(
        saturate(halfLambert), _SekaiShadowThreshold, shadowWidth, fadeMode);
    float3 firstStage = lerp(
        SekaiApplyHsvc(mainSample.rgb),
        lerp(mainSample.rgb, shadowSample, saturate(shadowTexWeight)),
        geometryBand);
    shadowSample = firstStage * _SekaiShadowColor.rgb;
#endif

#if defined(_UseFaceSDF)
    float sdfDirect = SAMPLE_TEXTURE2D(
        _FaceShadowTex, sampler_FaceShadowTex, input.faceUv).r;
    float sdfMirrored = SAMPLE_TEXTURE2D(
        _FaceShadowTex, sampler_FaceShadowTex, float2(-input.faceUv.x, input.faceUv.y)).r;
    float sdf = _FaceSdfMirror * _HeadDotDirectionalLightValues.x <= 0.0
        ? sdfMirrored : sdfDirect;
    float useFaceLimiter = lerp(
        _UseFaceShadowLimiter, _SekaiCharacterUseFaceShadowLimiter, overrideWeight);
    float rangeLimit = lerp(_RangeLimit, _SekaiCharacterRangeLimit, overrideWeight);
    float threshold = useFaceLimiter > 0.5
        ? min(max((1.0 - abs(_HeadDotDirectionalLightValues.y * 2.0 - 1.0)) * 0.5, 0.0), rangeLimit)
        : _HeadDotDirectionalLightValues.y;
    threshold = saturate(threshold + _FaceSdfBias);
    shadowBand = max(shadowBand, SekaiFaceShadowBand(
        sdf, threshold, shadowWidth, fadeMode));
#endif

    if (_FaceSphereShadowWeight > 0.001 && dot(_HeadPosition.xyz, _HeadPosition.xyz) > 1e-6)
    {
        float3 fromHead = normalize(input.positionWS - _HeadPosition.xyz);
        float sphere = 1.0 - smoothstep(
            _FaceSphereShadowEdge - _FaceSphereShadowSmoothness,
            _FaceSphereShadowEdge + _FaceSphereShadowSmoothness,
            dot(fromHead, lightDirection));
        shadowBand = saturate(shadowBand + sphere * _FaceSphereShadowWeight);
    }

    float3 mainColor = SekaiApplyHsvc(mainSample.rgb);
    float3 weightedShadow = lerp(mainSample.rgb, shadowSample, saturate(shadowTexWeight));
    weightedShadow *= _SekaiShadowColor.rgb;
    float3 color = lerp(mainColor, weightedShadow, shadowBand);
    float skinMask = _UseSkinColor > 0.5
        ? step(0.5, valueSample.r)
        : 0.0;
    float skinValue = lerp(mainSample.r, shadowSample.r, shadowBand);
    color = lerp(color, SekaiSkinRamp(skinValue, _SekaiShadowColor.rgb), skinMask);

    int formation = SekaiFormationIndex();
    float3 ambientColor = _SekaiCharacterAmbientLightColorArray[formation].rgb;
    float ambientIntensity = _SekaiCharacterAmbientLightIntensityArray[formation];
    if (max(max(ambientColor.r, ambientColor.g), max(ambientColor.b, ambientIntensity)) < 1e-5)
    {
        ambientColor = float3(0.5, 0.5, 0.5);
        ambientIntensity = 1.0;
    }
    float3 overlaid = SekaiOverlay(color, ambientColor);
    float allIntensity = ambientIntensity * _SekaiAllLightIntensity;
    float3 multiplied = overlaid * allIntensity * _PartsAmbientColor.rgb;
    float3 screened = 1.0 - 2.0 * (1.0 - overlaid * allIntensity) * (1.0 - _PartsAmbientColor.rgb);
    color = lerp(screened, multiplied, saturate(_PartsAmbientColor.a));

    float3 viewDirection = normalize(GetCameraPositionWS() - input.positionWS);
    float3 halfDirection = normalize(lightDirection + viewDirection);
    float specular = _SpecularPower > 1e-4
        ? pow(saturate(dot(normalWS, halfDirection)), 10.0 / max(_SpecularPower, 1e-4))
        : 0.0;
    specularMask = saturate(valueSample.a) * specular;
    color += _SekaiCharacterSpecularColorArray[formation].rgb
        * _SekaiCharacterSpecularColorArray[formation].a * specularMask;

    // Captured Toon-v3 rim branch. COLOR.g is the continuous mesh rim mask;
    // Factor = (range, emission, edgeSmoothness, lightInfluence).
    float3 rimDirection = normalize(
        _SekaiCharacterRimLightDirectionArray[formation].xyz
        + float3(1e-7, 0.0, 0.0));
    float4 rimFactor = _SekaiCharacterRimLightFactorArray[formation];
    float normalDotView = saturate(dot(normalWS, viewDirection));
    float viewDotRim = saturate(dot(viewDirection, rimDirection));
    float viewFresnel = pow(
        1.0 - normalDotView,
        max(10.0 - clamp(rimFactor.x, 0.0, 10.0), 0.001));
    float directedRim = viewFresnel
        * lerp(1.0, viewDotRim, saturate(rimFactor.w));
    float sidedRim = dot(normalWS, rimDirection) < 0.05
        ? directedRim
        : directedRim * (1.0 - 2.0 * saturate(rimFactor.w));
    float rim = SekaiSmooth01(saturate(
        (sidedRim - _RimThreshold) / max(rimFactor.z, 1e-5)));

    float4 rimBase = _SekaiCharacterRimLightColorArray[formation];
    float4 rimShadow = _SekaiCharacterRimLightShadowColorArray[formation];
    float sharpness = saturate(
        _SekaiCharacterRimLightShadowSharpnessArray[formation]);
    float rimColorMix = SekaiSmooth01(saturate(
        (dot(normalWS, rimDirection) - (sharpness - 1.0))
        / max(2.0 * (1.0 - sharpness), 1e-5)));
    float3 rimColor = lerp(rimBase.rgb, rimShadow.rgb, rimColorMix);
    float rimScalar = rim * saturate(input.color.g) * max(rimBase.a, 0.0);
    float3 rimAdd = rimColor * rimScalar;
    color += rimAdd * (1.0 + max(rimFactor.y, 0.0));

    color = SekaiApplyFlipBookProjector(
        color,
        input.positionWS,
        input.flipBookOrientation,
        _FlipBookColor_Character,
        _FlipBookScale_Character,
        _FlipBookUVScroll_Character);

    float luma = dot(color, float3(0.299, 0.587, 0.114));
    color = lerp(luma.xxx, color, _FinalSat);
    color *= _Brightness;
    // Captured Character MRT Target2 is formed before fog. ValueTex.g is the
    // authored material contribution; rimFactor.y is the formation emission
    // multiplier applied only to the unscaled rim contribution.
    brightnessContribution = rimAdd * max(rimFactor.y, 0.0)
        + color * saturate(valueSample.g);
    return saturate(color);
}

SekaiCharacterMrt SekaiPackMrt(float4 clipPosition, float3 color, float alpha, float3 brightness)
{
    SekaiCharacterMrt output;
    output.color = half4(color, alpha);
    float eyeDepth = max(LinearEyeDepth(clipPosition.z / clipPosition.w, _ZBufferParams), 1e-5);
    float coc = clamp((1.0 - _CoCParams.x * rcp(eyeDepth)) * _CoCParams.y, -1.0, 1.0);
    output.depth = half4((coc + 1.0) * 0.5, 0.0, 0.0, 1.0);
    output.brightness = half4(brightness, 1.0);
    return output;
}

SekaiCharacterMrt SekaiCharacterFrag(SekaiCharacterVaryings input)
{
    clip(0.5 - _UseEyelash);
    float specularMask;
    float3 brightness;
    float3 color = SekaiEvaluateToon(input, specularMask, brightness);
    color = lerp(
        _SekaiFogColor.rgb,
        color,
        lerp(1.0, input.fog, saturate(_SekaiFogColor.a)));
    float alpha = lerp(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a,
        1.0, saturate(_Transparency));
    return SekaiPackMrt(input.clipPosition, color, alpha, brightness);
}

SekaiCharacterMrt SekaiCharacterOutlineFrag(SekaiCharacterVaryings input)
{
    clip(0.5 - _UseEyelash);
    float specularMask;
    float3 surfaceBrightness;
    float3 shaded = SekaiEvaluateToon(input, specularMask, surfaceBrightness);
    int formation = SekaiFormationIndex();
    float4 outline = _SekaiCharacterOutlineColorArray[formation];
    float3 outlineBase = outline.rgb * outline.a;
    float3 color = lerp(outlineBase, shaded,
        saturate(_SekaiCharacterOutlineBlendingArray[formation]));
    float3 brightness = lerp(
        surfaceBrightness,
        outlineBase,
        saturate(_SekaiCharacterOutlineBlendingArray[formation]));
    return SekaiPackMrt(input.clipPosition, color, 1.0, brightness);
}

SekaiCharacterMrt SekaiCharacterEyelashFrag(SekaiCharacterVaryings input)
{
    clip(_UseEyelash - 0.5);
    float specularMask;
    float3 brightness;
    float3 color = SekaiEvaluateToon(input, specularMask, brightness);
    float alpha = SAMPLE_TEXTURE2D(
        _EyelashMaskTex, sampler_EyelashMaskTex, input.uv).r
        * input.eyelashFade;
    clip(alpha - 0.001);
    return SekaiPackMrt(input.clipPosition, color, alpha, brightness * alpha);
}

#endif
