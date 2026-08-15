Shader "Sekai/Live/MusicItem/Toon"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _ShadowTex ("Shadow", 2D) = "white" {}
        _ValueTex ("Value", 2D) = "gray" {}
        _PartsAmbientColor ("Parts Ambient", Color) = (1,1,1,1)
        _ShadowTexWeight ("Shadow Texture Weight", Range(0,1)) = 1
        _ShadowWidth ("Shadow Width", Range(0,1)) = 0
        _FadeMode ("Fade Mode", Float) = 0
        _RimThreshold ("Rim Threshold", Range(0.01,1)) = 0.2
        _SpecularPower ("Specular Power", Range(0,5)) = 0
        _FinalSat ("Final Saturation", Float) = 0.95
        _Brightness ("Brightness", Float) = 1
        _FormationId ("Formation Id", Float) = 0
        _CharacterId ("Character Id", Float) = 0
        _Transparency ("Transparency", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        UsePass "Sekai/Live/Character/Toon-v3/Base"
        UsePass "Sekai/Live/Character/Toon-v3/Outline"
    }
}
