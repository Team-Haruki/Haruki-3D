Shader "Hidden/Sekai/SubpixelMorphologicalAntialiasing"
{
    Properties
    {
        [HideInInspector] _StencilRef ("_StencilRef", Int) = 64
        [HideInInspector] _StencilMask ("_StencilMask", Int) = 64
    }

    HLSLINCLUDE

        // The 6.7.0 player exposes these exact local keywords. Its captured
        // SPIR-V is structurally identical to URP 14's three-pass SMAA bridge:
        // edge detection, blend-weight calculation, neighbourhood blending.
        #pragma multi_compile_local _SMAA_PRESET_LOW _SMAA_PRESET_MEDIUM _SMAA_PRESET_HIGH
        #pragma exclude_renderers gles

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Edge Detection"

            Stencil
            {
                WriteMask [_StencilMask]
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
                #pragma vertex VertEdge
                #pragma fragment FragEdge
                #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/SubpixelMorphologicalAntialiasingBridge.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Blend Weights Calculation"

            Stencil
            {
                WriteMask [_StencilMask]
                ReadMask [_StencilMask]
                Ref [_StencilRef]
                Comp Equal
                Pass Replace
            }

            HLSLPROGRAM
                #pragma vertex VertBlend
                #pragma fragment FragBlend
                #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/SubpixelMorphologicalAntialiasingBridge.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Neighborhood Blending"

            HLSLPROGRAM
                #pragma vertex VertNeighbor
                #pragma fragment FragNeighbor
                #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/SubpixelMorphologicalAntialiasingBridge.hlsl"
            ENDHLSL
        }
    }
}
