Shader "Haruki/MV/RecoveredStageColorMap"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _ColorTex ("Color Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Name "Base"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RecoveredStageVert
            #pragma fragment RecoveredStageColorMapFrag
            #include "RecoveredStageCommon.hlsl"
            ENDHLSL
        }
    }
}
