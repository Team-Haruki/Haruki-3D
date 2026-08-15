#ifndef HARUKI_SEKAI_FLIPBOOK_PROJECTOR_INCLUDED
#define HARUKI_SEKAI_FLIPBOOK_PROJECTOR_INCLUDED

TEXTURE2D(_FlipBookTex); SAMPLER(sampler_FlipBookTex);

float _GlobalFlipBookTime;
float4 _FlipBookCenterPosition;
float _FlipBookOpacity;
float _FlipBookFadeRadius;
float _FlipBookFadeFallOff;
float _FlipBookFrameCountX;
float _FlipBookFrameCountY;
float _FlipBookFPS;

float SekaiFlipBookOrientation(
    float normalY,
    float upperOpacity,
    float lowerOpacity,
    float threshold,
    float fallOff)
{
    float sideOpacity = normalY >= 0.0 ? upperOpacity : lowerOpacity;
    float value = (abs(normalY) * sideOpacity - threshold)
        / max(saturate(fallOff + 0.001), 1e-5);
    value = saturate(value);
    return value * value * (3.0 - 2.0 * value);
}

float3 SekaiApplyFlipBookProjector(
    float3 color,
    float3 positionWS,
    float orientation,
    float4 projectorColor,
    float uvScale,
    float2 uvScroll)
{
#if defined(_USE_OVERLAY_FLIPBOOK)
    float2 baseUv = frac(
        (positionWS.xz - _FlipBookCenterPosition.xz) * uvScale
        - _GlobalFlipBookTime * uvScroll);
    float2 frameCount = max(
        float2(_FlipBookFrameCountX, _FlipBookFrameCountY),
        float2(1.0, 1.0));
    float frameTotal = frameCount.x * frameCount.y;
    float framePhase = (_GlobalFlipBookTime * _FlipBookFPS + 1e-5) / frameTotal;
    float frame = floor(frameTotal * frac(framePhase));
    float row = floor(frame / frameCount.x);
    float2 cell = float2(
        frame - frameCount.x * row,
        frameCount.y - row - 1.0);
    float2 atlasUv = (baseUv + cell) / frameCount;
    float4 sampleValue = SAMPLE_TEXTURE2D(
        _FlipBookTex, sampler_FlipBookTex, atlasUv);
    float radial = 1.0 - smoothstep(
        _FlipBookFadeRadius,
        _FlipBookFadeRadius + _FlipBookFadeFallOff,
        distance(positionWS.xz, _FlipBookCenterPosition.xz));
    color += sampleValue.rgb * sampleValue.a
        * orientation
        * projectorColor.rgb * projectorColor.a
        * _FlipBookOpacity * radial;
#endif
    return color;
}

#endif
