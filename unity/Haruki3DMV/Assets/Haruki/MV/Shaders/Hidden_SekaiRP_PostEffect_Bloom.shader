Shader "Hidden/SekaiRP/PostEffect/Bloom"
{
    Properties
    {
        [HideInInspector] _MainTex ("Source", 2D) = "black" {}
        [HideInInspector] _SampleOffset ("Sample Offset", Float) = 1
        [HideInInspector] _BloomIntensity ("Bloom Intensity", Float) = 0
        [HideInInspector] _BloomScatter ("Bloom Scatter", Float) = 1
        [HideInInspector] _BloomScatterWeight ("Bloom Scatter Weight", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        // The six programs are recovered from consecutive JP 6.7.0 Vulkan
        // pipelines shader-0315..0323. Their pass indices are independently
        // fixed by BloomExtension: SetupTexture uses 5, DrawBloomSheet uses 0,
        // DrawBlur uses 1/2, and DrawBloomTexture uses 4.
        HLSLINCLUDE
        #pragma target 3.0
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        float4 _MainTex_ST;
        float4 _BlitTexture_TexelSize;
        float _SampleOffset;
        float _BloomIntensity;
        float _BloomScatter;
        float _BloomScatterWeight;

        struct BloomMeshAttributes
        {
            float4 positionOS : POSITION;
            float2 texcoord : TEXCOORD0;
            float2 levelWeight : TEXCOORD1;
        };

        struct TextureToSheetVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            float weight : TEXCOORD1;
            nointerpolation int mipLevel : TEXCOORD2;
        };

        TextureToSheetVaryings VertTextureToSheet(BloomMeshAttributes input)
        {
            TextureToSheetVaryings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
            output.weight = input.levelWeight.y;
            output.mipLevel = (int)input.levelWeight.x;
            return output;
        }

        half4 FragTextureToSheet(TextureToSheetVaryings input) : SV_Target
        {
            half4 color = SAMPLE_TEXTURE2D_LOD(
                _MainTex,
                sampler_MainTex,
                input.texcoord,
                input.mipLevel);
            color.rgb *= input.weight;
            return color;
        }

        half3 SampleBloomPair(float2 uv, float2 delta, float offset, half weight)
        {
            half3 positive = SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture,
                sampler_LinearClamp,
                uv + delta * offset,
                _BlitMipLevel).rgb;
            half3 negative = SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture,
                sampler_LinearClamp,
                uv - delta * offset,
                _BlitMipLevel).rgb;
            return (positive + negative) * weight;
        }

        half4 BloomBlur(float2 uv, float2 axis)
        {
            float2 delta = _BlitTexture_TexelSize.xy * axis;
            half3 color = SampleBloomPair(uv, delta, 0.65, 0.204h);
            color += SampleBloomPair(uv, delta, 2.43, 0.198h);
            color += SampleBloomPair(uv, delta, 4.37, 0.098h);
            return half4(color, 1.0h);
        }

        half4 FragBlurHorizontal(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return BloomBlur(input.texcoord, float2(1.0, 0.0));
        }

        half4 FragBlurVertical(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return BloomBlur(input.texcoord, float2(0.0, 1.0));
        }

        struct SheetToTextureVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            float bloomWeight : TEXCOORD1;
            float alpha : TEXCOORD2;
        };

        SheetToTextureVaryings VertSheetToTexture(BloomMeshAttributes input)
        {
            SheetToTextureVaryings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
            output.bloomWeight = pow(_BloomScatter, input.levelWeight.x)
                * _BloomScatterWeight
                * _BloomIntensity;
            output.alpha = input.levelWeight.y;
            return output;
        }

        half4 SampleSheet(SheetToTextureVaryings input, half multiplier)
        {
            half4 color = SAMPLE_TEXTURE2D(
                _MainTex,
                sampler_MainTex,
                input.texcoord);
            color.rgb *= input.bloomWeight * (1.0h - input.alpha) * multiplier;
            color.a = input.alpha;
            return color;
        }

        half4 FragSheetToTexture(SheetToTextureVaryings input) : SV_Target
        {
            return SampleSheet(input, 1.0h);
        }

        half4 FragSheetToTextureDouble(SheetToTextureVaryings input) : SV_Target
        {
            return SampleSheet(input, 2.0h);
        }

        struct PrefilterVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            float2 sampleStep : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        PrefilterVaryings VertPrefilter(Attributes input)
        {
            Varyings baseOutput = Vert(input);
            PrefilterVaryings output;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = baseOutput.positionCS;
            output.sampleStep = _BlitTexture_TexelSize.xy * _SampleOffset;
            output.texcoord = baseOutput.texcoord - output.sampleStep * 3.0;
            return output;
        }

        half4 FragPrefilter(PrefilterVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 color = SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture,
                sampler_LinearClamp,
                input.texcoord,
                _BlitMipLevel) * 0.036h;
            input.texcoord += input.sampleStep;
            color += SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture, sampler_LinearClamp, input.texcoord, _BlitMipLevel) * 0.113h;
            input.texcoord += input.sampleStep;
            color += SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture, sampler_LinearClamp, input.texcoord, _BlitMipLevel) * 0.216h;
            input.texcoord += input.sampleStep;
            color += SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture, sampler_LinearClamp, input.texcoord, _BlitMipLevel) * 0.269h;
            input.texcoord += input.sampleStep;
            color += SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture, sampler_LinearClamp, input.texcoord, _BlitMipLevel) * 0.216h;
            input.texcoord += input.sampleStep;
            color += SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture, sampler_LinearClamp, input.texcoord, _BlitMipLevel) * 0.113h;
            input.texcoord += input.sampleStep;
            color += SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture, sampler_LinearClamp, input.texcoord, _BlitMipLevel) * 0.036h;
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Texture To Sheet"
            HLSLPROGRAM
            #pragma vertex VertTextureToSheet
            #pragma fragment FragTextureToSheet
            ENDHLSL
        }

        Pass
        {
            Name "Blur Horizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurHorizontal
            ENDHLSL
        }

        Pass
        {
            Name "Blur Vertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurVertical
            ENDHLSL
        }

        Pass
        {
            Name "Sheet To Texture"
            Blend One SrcAlpha
            HLSLPROGRAM
            #pragma vertex VertSheetToTexture
            #pragma fragment FragSheetToTexture
            ENDHLSL
        }

        Pass
        {
            Name "Sheet To Texture Double"
            Blend One SrcAlpha
            HLSLPROGRAM
            #pragma vertex VertSheetToTexture
            #pragma fragment FragSheetToTextureDouble
            ENDHLSL
        }

        Pass
        {
            Name "Source Prefilter"
            HLSLPROGRAM
            #pragma vertex VertPrefilter
            #pragma fragment FragPrefilter
            ENDHLSL
        }
    }
}
