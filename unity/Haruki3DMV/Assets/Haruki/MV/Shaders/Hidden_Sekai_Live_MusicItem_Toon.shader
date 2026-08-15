Shader "Hidden/Sekai/Live/MusicItem/Toon"
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
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "TransparentBase"
            Tags { "LightMode"="SekaiTransparentBase" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SekaiCharacterVert
            #pragma fragment SekaiMusicItemFrag
            #pragma shader_feature_local _ _LAMBERT
            #pragma multi_compile _ _USE_OVERLAY_FLIPBOOK
            #include "SekaiCharacterCommon.hlsl"

            SekaiCharacterMrt SekaiMusicItemFrag(SekaiCharacterVaryings input)
            {
                float specularMask;
                float3 brightness;
                float3 color = SekaiEvaluateToon(
                    input, specularMask, brightness);
                color = lerp(
                    _SekaiFogColor.rgb,
                    color,
                    lerp(1.0, input.fog, saturate(_SekaiFogColor.a)));
                float alpha = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, input.uv).a
                    * (1.0 - saturate(_Transparency));
                return SekaiPackMrt(input.clipPosition, color, alpha, brightness);
            }
            ENDHLSL
        }
    }
}
