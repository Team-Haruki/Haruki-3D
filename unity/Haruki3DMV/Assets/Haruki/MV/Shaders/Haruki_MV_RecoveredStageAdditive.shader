Shader "Haruki/MV/RecoveredStageAdditive"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _ColorTex ("Color", 2D) = "white" {}
        _LightMapTex ("Light Map", 2D) = "white" {}
        _BgTex ("Background", 2D) = "black" {}
        _SubTex ("Sub", 2D) = "black" {}
        _Color ("Color", Color) = (1,1,1,1)
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _LightColor ("Light Color", Color) = (1,1,1,1)
        _FadeColor ("Fade Color", Color) = (1,1,1,1)
        _SheetValue ("Sheet", Vector) = (1,1,0,0)
        _Intensity ("Intensity", Float) = 1
        _Brightness ("Brightness", Float) = 1
        _Fade ("Fade", Float) = 0
        _LocalTime ("Local Time", Float) = 0
        _Cutoff ("Cutoff", Float) = 0.5
        _AlphaClipOn ("Alpha Clip", Float) = 0
        _RotateTheta ("UV Rotate Theta", Float) = 0
        _RotateOffset ("UV Rotate Offset", Float) = 0
        _BloomWrite ("Bloom Write", Float) = 1
        _BloomScale ("Bloom Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Base"
            Tags { "LightMode"="SekaiTransparentBase" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha One
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RecoveredStageVert
            #pragma fragment RecoveredStageAdditiveFrag
            #include "RecoveredStageCommon.hlsl"
            ENDHLSL
        }
    }
}
