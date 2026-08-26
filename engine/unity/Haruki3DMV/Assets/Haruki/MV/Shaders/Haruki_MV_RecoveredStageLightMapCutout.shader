Shader "Haruki/MV/RecoveredStageLightMapCutout"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _LightMapTex ("Light Map", 2D) = "white" {}
        _RotateTheta ("UV Rotate Theta", Float) = 0
        _RotateOffset ("UV Rotate Offset", Float) = 0
        _Cutoff ("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Name "Base"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RecoveredStageVert
            #pragma fragment RecoveredStageLightMapCutoutFrag
            #pragma multi_compile _ _USE_OVERLAY_FLIPBOOK
            #include "RecoveredStageCommon.hlsl"
            ENDHLSL
        }
    }
}
