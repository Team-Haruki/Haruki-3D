Shader "Sekai/Live/Character/Eye-Highlight"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold ("Threshold", Range(0,1)) = 0.5
        _LightInfluence ("Light Influence", Range(0,1)) = 1
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _DistortionTex ("Distortion Map", 2D) = "black" {}
        _DistortionTexTilingX ("Distortion Tiling X", Float) = 1
        _DistortionTexTilingY ("Distortion Tiling Y", Float) = 1
        _DistortionScrollSpeed ("Distortion Speed", Float) = 1
        _DistortionScrollX ("Distortion Scroll X", Float) = 0.5
        _DistortionScrollY ("Distortion Scroll Y", Float) = 0.5
        _DistortionFPS ("Distortion FPS", Float) = 12
        _DistortionIntensity ("Distortion Intensity", Float) = 1
        _DistortionIntensityX ("Distortion Intensity X", Float) = 1
        _DistortionIntensityY ("Distortion Intensity Y", Float) = 1
        _DistortionOffsetX ("Distortion Offset X", Float) = 0
        _DistortionOffsetY ("Distortion Offset Y", Float) = 0
        [HideInInspector] _ApplyDistortionTex ("Apply Distortion", Float) = 0
        [HideInInspector] _TintColor ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _PartsAmbientColor ("Parts Ambient", Color) = (1,1,1,1)
        [HideInInspector] _CharacterLightFactor ("Character Light Factor", Color) = (1,1,1,1)
        [HideInInspector] _CharacterLightIntensity ("Character Light Intensity", Float) = 1
        [HideInInspector] _FaceFront ("Face Front", Vector) = (0,0,1,0)
        [HideInInspector] _EyelashTransparent ("Eyelash Transparent", Float) = 0
        [HideInInspector] _EyelashFaceCameraEdge1 ("Eyelash Edge 1", Float) = 0.5
        [HideInInspector] _EyelashFaceCameraEdge2 ("Eyelash Edge 2", Float) = 0
        [HideInInspector] _LightInfluenceForEyeHighlight ("Highlight Light", Float) = 1
        [HideInInspector] _CharacterId ("Character Id", Float) = -1
        [HideInInspector] _FormationId ("Formation Id", Float) = 0
        [HideInInspector] _Stencil ("Stencil", Float) = 1
        [HideInInspector] _ReadMask ("Read Mask", Float) = 255
        [HideInInspector] _WriteMask ("Write Mask", Float) = 255
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilOp ("Stencil Op", Float) = 2
        [HideInInspector] _StencilFail ("Stencil Fail", Float) = 0
        [HideInInspector] _StencilZFail ("Stencil ZFail", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Name "Base"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            Blend One OneMinusSrcAlpha
            Stencil { Ref [_Stencil] ReadMask [_ReadMask] WriteMask [_WriteMask] Comp [_StencilComp] Pass [_StencilOp] Fail [_StencilFail] ZFail [_StencilZFail] }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SekaiEyeVert
            #pragma fragment SekaiEyeHighlightFrag
            #include "SekaiCharacterEyeCommon.hlsl"
            ENDHLSL
        }
    }
}
