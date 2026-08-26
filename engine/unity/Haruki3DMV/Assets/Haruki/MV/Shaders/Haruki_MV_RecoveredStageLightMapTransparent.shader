Shader "Haruki/MV/RecoveredStageLightMapTransparent"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _LightMapTex ("Light Map", 2D) = "white" {}
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _ZWrite ("ZWrite", Float) = 0
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [HideInInspector] _BlendSrc ("Blend Src", Float) = 5
        [HideInInspector] _BlendDst ("Blend Dst", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Base"
            Tags { "LightMode"="SekaiTransparentBase" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_BlendSrc] [_BlendDst]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RecoveredStageVert
            #pragma fragment RecoveredStageLightMapTransparentFrag
            #include "RecoveredStageCommon.hlsl"
            ENDHLSL
        }
    }
}
