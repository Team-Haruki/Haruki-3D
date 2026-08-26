Shader "Haruki/MV/RecoveredStageLightMapEmission"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _LightMapTex ("Light Map", 2D) = "white" {}
        _EmissionTex ("Emission", 2D) = "black" {}
        _LightIntensityMin ("Emission Light Min", Float) = 0
        _LightIntensityMax ("Emission Light Max", Float) = 1
        _RotateTheta ("UV Rotate Theta", Float) = 0
        _RotateOffset ("UV Rotate Offset", Float) = 0
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
            #pragma fragment RecoveredStageEmissionFrag
            #include "RecoveredStageCommon.hlsl"
            ENDHLSL
        }
    }
}
