Shader "Haruki/MV/RecoveredStageTexture"
{
    Properties { _MainTex ("Main", 2D) = "white" {} }
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
            #pragma fragment RecoveredStageTextureFrag
            #include "RecoveredStageCommon.hlsl"
            ENDHLSL
        }
    }
}
