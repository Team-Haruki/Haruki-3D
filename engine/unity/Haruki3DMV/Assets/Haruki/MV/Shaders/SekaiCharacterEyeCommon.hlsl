#ifndef HARUKI_SEKAI_CHARACTER_EYE_COMMON_INCLUDED
#define HARUKI_SEKAI_CHARACTER_EYE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
TEXTURE2D(_DistortionTex); SAMPLER(sampler_DistortionTex);
TEXTURE2D(_EyeFlipbookTex); SAMPLER(sampler_EyeFlipbookTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _EyeFlipbookTex_ST;
float4 _SheetValue;
float4 _TintColor;
float4 _EmissionColor;
float4 _PartsAmbientColor;
float4 _CharacterLightFactor;
float4 _FaceFront;
float _Threshold;
float _LightInfluence;
float _LightInfluenceForEyeHighlight;
float _ApplyDistortionTex;
float _DistortionTexTilingX;
float _DistortionTexTilingY;
float _DistortionScrollSpeed;
float _DistortionScrollX;
float _DistortionScrollY;
float _DistortionFPS;
float _DistortionIntensity;
float _DistortionIntensityX;
float _DistortionIntensityY;
float _DistortionOffsetX;
float _DistortionOffsetY;
float _EyeFlipbookCurrentFrame;
float _EyeFlipbookEnabled;
float _EyeFlipbookOffsetX;
float _EyeFlipbookOffsetY;
float _EyelashTransparent;
float _EyelashFaceCameraEdge1;
float _EyelashFaceCameraEdge2;
float _CharacterLightIntensity;
float _CharacterId;
float _FormationId;
CBUFFER_END

float4 _CoCParams;
float4 _SekaiFogColor;
float4 _SekaiFogFactor;
float _SekaiGlobalEyeTime;
float _SekaiAllLightIntensity;
float4 _SekaiCharacterAmbientLightColorArray[12];
float _SekaiCharacterAmbientLightIntensityArray[12];
float4 _SekaiGlobalSpotLightPos;
float4 _SekaiGlobalSpotLightColor;
float _SekaiGlobalSpotLightRadiusNear;
float _SekaiGlobalSpotLightRadiusFar;
float _SekaiGlobalSpotLightEnabled;

struct SekaiEyeAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct SekaiEyeVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 distortionUv : TEXCOORD1;
    float2 flipbookUv : TEXCOORD2;
    float3 positionWS : TEXCOORD3;
    float4 clipPosition : TEXCOORD4;
    float fog : TEXCOORD5;
};

struct SekaiEyeMrt
{
    half4 color : SV_Target0;
    half4 depth : SV_Target1;
    half4 brightness : SV_Target2;
};

float SekaiEyeFogFactor(float4 clipPosition)
{
    float eyeDepth = max(LinearEyeDepth(
        clipPosition.z / clipPosition.w, _ZBufferParams), 0.0);
    return saturate(-eyeDepth * _SekaiFogFactor.y + _SekaiFogFactor.x);
}

SekaiEyeVaryings SekaiEyeVert(SekaiEyeAttributes input)
{
    SekaiEyeVaryings output;
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.clipPosition = position.positionCS;
    output.positionWS = position.positionWS;
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    output.distortionUv = input.uv * float2(
        _DistortionTexTilingX, _DistortionTexTilingY);
    output.fog = SekaiEyeFogFactor(position.positionCS);

    float frame = floor(max(_EyeFlipbookCurrentFrame, 0.0));
    float2 sheet = max(_SheetValue.xy, float2(1e-5, 1e-5));
    float frameProduct = frame * sheet.x;
    float2 frameCell = frac(float2(frameProduct, frameProduct));
    frameCell.y = floor(frameProduct) * sheet.y;
    float2 flipbookUv;
    flipbookUv.y = input.uv.y * sheet.y - frameCell.y + _EyeFlipbookOffsetY;
    flipbookUv.x = (abs(input.uv.x * 2.0 - 1.0) + _EyeFlipbookOffsetX)
        * sheet.x + frameCell.x;
    output.flipbookUv = flipbookUv * _EyeFlipbookTex_ST.xy
        + _EyeFlipbookTex_ST.zw;
    return output;
}

float2 SekaiEyeDistortedUv(float2 uv, float2 distortionUv)
{
    if (_ApplyDistortionTex < 0.5) return uv;
    // Exact captured Eye-Base/Eye-Highlight distortion sequence.  The offset
    // belongs inside the signed distortion field; adding it to the final UV
    // shifts the whole eye (notably Eye-Base's official 0.57 default).
    float fps = max(abs(_DistortionFPS), 1e-5);
    float steppedTime = floor(_DistortionFPS * _SekaiGlobalEyeTime) / fps;
    float2 scroll = steppedTime * float2(
        _DistortionScrollX, _DistortionScrollY) * _DistortionScrollSpeed;
    float2 distortion = SAMPLE_TEXTURE2D(
        _DistortionTex, sampler_DistortionTex, distortionUv - scroll).rg;
    distortion = distortion * 2.0
        + float2(_DistortionOffsetX, _DistortionOffsetY) - 1.0;
    return uv + distortion * _DistortionIntensity
        * float2(_DistortionIntensityX, _DistortionIntensityY);
}

float3 SekaiEyeOverlay(float3 baseColor, float3 blendColor)
{
    float3 low = 2.0 * baseColor * blendColor;
    float3 high = 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor);
    return lerp(low, high, step(float3(0.5, 0.5, 0.5), baseColor));
}

int SekaiEyeFormationIndex()
{
    float selected = _CharacterId >= 0.0 ? _CharacterId : _FormationId;
    return (int)clamp(floor(selected + 0.5), 0.0, 11.0);
}

float3 SekaiEyeApplyLighting(float3 color, float influence, float3 positionWS)
{
    int formation = SekaiEyeFormationIndex();
    float3 ambientColor = _SekaiCharacterAmbientLightColorArray[formation].rgb;
    float ambientIntensity = _SekaiCharacterAmbientLightIntensityArray[formation];
    float hasRuntimeAmbient = step(1e-5, max(
        max(ambientColor.r, ambientColor.g), max(ambientColor.b, ambientIntensity)));
    ambientColor = lerp(_CharacterLightFactor.rgb, ambientColor, hasRuntimeAmbient);
    ambientIntensity = lerp(_CharacterLightIntensity, ambientIntensity, hasRuntimeAmbient);

    float3 overlaid = SekaiEyeOverlay(color, ambientColor);
    float allIntensity = ambientIntensity * _SekaiAllLightIntensity;
    float3 multiplied = overlaid * allIntensity * _PartsAmbientColor.rgb;
    float3 screened = 1.0 - 2.0
        * (1.0 - overlaid * allIntensity) * (1.0 - _PartsAmbientColor.rgb);
    float3 lit = lerp(screened, multiplied, saturate(_PartsAmbientColor.a));

    if (_SekaiGlobalSpotLightEnabled > 0.5)
    {
        float distanceSquared = dot(
            _SekaiGlobalSpotLightPos.xyz - positionWS,
            _SekaiGlobalSpotLightPos.xyz - positionWS);
        float spot = saturate((distanceSquared - _SekaiGlobalSpotLightRadiusNear)
            / max(_SekaiGlobalSpotLightRadiusFar - _SekaiGlobalSpotLightRadiusNear, 1e-5));
        spot = spot * spot * (3.0 - 2.0 * spot);
        lit = lerp(lit, lit * _SekaiGlobalSpotLightColor.rgb, spot);
    }
    return lerp(color, lit, saturate(influence));
}

SekaiEyeMrt SekaiPackEye(float4 clipPosition, float4 color, float3 brightness)
{
    SekaiEyeMrt output;
    output.color = color;
    float eyeDepth = max(LinearEyeDepth(
        clipPosition.z / clipPosition.w, _ZBufferParams), 1e-5);
    float coc = clamp((1.0 - _CoCParams.x * rcp(eyeDepth)) * _CoCParams.y, -1.0, 1.0);
    output.depth = half4((coc + 1.0) * 0.5, 0.0, 0.0, 1.0);
    output.brightness = half4(brightness, 1.0);
    return output;
}

SekaiEyeMrt SekaiEyeBaseFrag(SekaiEyeVaryings input)
{
    float4 sample = SAMPLE_TEXTURE2D(
        _MainTex, sampler_MainTex,
        SekaiEyeDistortedUv(input.uv, input.distortionUv));
    float4 tinted = sample * _TintColor;
    if (_EyeFlipbookEnabled >= 0.5)
    {
        float4 flipbook = SAMPLE_TEXTURE2D(
            _EyeFlipbookTex, sampler_EyeFlipbookTex, input.flipbookUv);
        tinted.rgb = lerp(tinted.rgb, flipbook.rgb, flipbook.a);
    }
    float3 color = SekaiEyeApplyLighting(
        tinted.rgb, _LightInfluence, input.positionWS);
    color = lerp(_SekaiFogColor.rgb, color,
        lerp(1.0, input.fog, saturate(_SekaiFogColor.a)));
    return SekaiPackEye(input.clipPosition,
        float4(color, tinted.a), float3(0.0, 0.0, 0.0));
}

SekaiEyeMrt SekaiEyeHighlightFrag(SekaiEyeVaryings input)
{
    float4 sample = SAMPLE_TEXTURE2D(
        _MainTex, sampler_MainTex,
        SekaiEyeDistortedUv(input.uv, input.distortionUv));
    clip(sample.r - _Threshold);
    float3 color = SekaiEyeApplyLighting(
        sample.rgb, _LightInfluenceForEyeHighlight, input.positionWS);
    return SekaiPackEye(input.clipPosition,
        float4(color, sample.r), _EmissionColor.rgb);
}

#endif
