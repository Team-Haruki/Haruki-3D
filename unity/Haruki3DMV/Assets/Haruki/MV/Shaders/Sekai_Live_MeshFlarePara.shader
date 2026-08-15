Shader "Sekai/Live/MeshFlarePara"
{
    Properties
    {
        _MeshFlareParaMainTex ("MainTex", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)]
        _MeshFlareParaBlendSrc ("Blend Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]
        _MeshFlareParaBlendDst ("Blend Dst", Float) = 0
        _MeshFlareParaColor ("Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _MeshFlareParaZTest ("ZTest", Float) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "SekaiMeshFlarePara"
            Tags { "LightMode" = "SekaiMeshFlarePara" }
            Blend [_MeshFlareParaBlendSrc] [_MeshFlareParaBlendDst]
            Cull Back
            ZWrite Off
            ZTest [_MeshFlareParaZTest]
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _MULTIPLY_BLEND

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MeshFlareParaMainTex);
            SAMPLER(sampler_MeshFlareParaMainTex);
            float4 _MeshFlareParaMainTex_ST;
            half4 _MeshFlareParaColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MeshFlareParaMainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(
                    _MeshFlareParaMainTex,
                    sampler_MeshFlareParaMainTex,
                    input.uv);
#if defined(_MULTIPLY_BLEND)
                return half4(
                    1.0 + texel.a * (texel.rgb * _MeshFlareParaColor.rgb - 1.0),
                    1.0 + texel.a * (texel.a - 1.0));
#else
                return half4(texel.rgb * _MeshFlareParaColor.rgb, texel.a);
#endif
            }
            ENDHLSL
        }
    }
}
