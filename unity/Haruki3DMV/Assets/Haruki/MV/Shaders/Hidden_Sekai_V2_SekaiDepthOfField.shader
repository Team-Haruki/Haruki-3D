Shader "Hidden/Sekai/V2/SekaiDepthOfField"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        // Recovered from the JP 6.7.0 Vulkan programs shader-0316..0326.
        // Pass identity and ordering are independently fixed by
        // SekaiDofPassId and UpdateSekaiDof in libil2cpp.so.
        HLSLINCLUDE
        #pragma target 3.0
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_DepthTexture);
        TEXTURE2D_X(_BlurBgMidTex);
        TEXTURE2D_X(_BlurFgMidTex);
        TEXTURE2D_X(_BlurFgLowTex);

        float4 _Offsets;

        struct DofVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            float4 offset1 : TEXCOORD1;
            float4 offset2 : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct DofDownsampleVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            float2 offset1 : TEXCOORD1;
            float2 offset2 : TEXCOORD2;
            float2 diagonalBase : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        DofVaryings VertDofBlur(Attributes input)
        {
            Varyings baseOutput = Vert(input);
            DofVaryings output;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = baseOutput.positionCS;
            output.texcoord = baseOutput.texcoord;
            output.offset1 = baseOutput.texcoord.xyxy
                + _Offsets.xyxy * float4(1.0, 1.0, -1.0, -1.0);
            output.offset2 = baseOutput.texcoord.xyxy
                + _Offsets.xyxy * float4(2.0, 2.0, -2.0, -2.0);
            return output;
        }

        DofDownsampleVaryings VertDofDownsample(Attributes input)
        {
            Varyings baseOutput = Vert(input);
            DofDownsampleVaryings output;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = baseOutput.positionCS;
            output.texcoord = baseOutput.texcoord;
            output.offset1 = baseOutput.texcoord - _Offsets.xy;
            output.offset2 = baseOutput.texcoord + _Offsets.xy * float2(1.0, -1.0);
            output.diagonalBase = float2(0.0, _Offsets.y * 2.0);
            return output;
        }

        half4 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture,
                sampler_LinearClamp,
                uv,
                _BlitMipLevel);
        }

        half4 FragAlphaCoc(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 source = SampleSource(input.texcoord);
            source.a = SAMPLE_TEXTURE2D_X_LOD(
                _DepthTexture,
                sampler_LinearClamp,
                input.texcoord,
                _BlitMipLevel).r;
            return source;
        }

        struct DofMrtOutput
        {
            half4 background : SV_Target0;
            half4 foreground : SV_Target1;
        };

        DofMrtOutput FragMidDownsample(DofDownsampleVaryings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 c0 = SampleSource(input.diagonalBase + input.offset1);
            half4 c1 = SampleSource(input.diagonalBase + input.offset2);
            half4 c2 = SampleSource(input.offset1);
            half4 c3 = SampleSource(input.offset2);
            half4 center = SampleSource(input.texcoord);

            half3 rgb = (c0.rgb + c1.rgb + c2.rgb + c3.rgb + center.rgb) * 0.2h;
            half bgAlpha = max(
                max(c0.a * 2.0h - 1.0h, c1.a * 2.0h - 1.0h),
                max(c2.a * 2.0h - 1.0h, c3.a * 2.0h - 1.0h));
            half fgAlpha = max(
                max((1.0h - c0.a) * 2.0h - 1.0h, (1.0h - c1.a) * 2.0h - 1.0h),
                max((1.0h - c2.a) * 2.0h - 1.0h, (1.0h - c3.a) * 2.0h - 1.0h));

            DofMrtOutput output;
            output.background = half4(rgb, bgAlpha);
            output.foreground = half4(rgb, fgAlpha);
            return output;
        }

        half4 FragBlurBackground(DofVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 center = SampleSource(input.texcoord);
            half3 rgb =
                SampleSource(input.offset1.xy).rgb
                + SampleSource(input.offset1.zw).rgb
                + center.rgb
                + SampleSource(input.offset2.xy).rgb
                + SampleSource(input.offset2.zw).rgb;
            return half4(rgb * 0.2h, center.a);
        }

        half4 FragDownsampleConserveCoc(DofDownsampleVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 c0 = SampleSource(input.diagonalBase + input.offset1);
            half4 c1 = SampleSource(input.diagonalBase + input.offset2);
            half4 c2 = SampleSource(input.offset1);
            half4 c3 = SampleSource(input.offset2);
            half4 center = SampleSource(input.texcoord);
            half3 rgb = (c0.rgb + c1.rgb + c2.rgb + c3.rgb + center.rgb) * 0.2h;
            half alpha = max(max(c0.a, c1.a), max(max(c2.a, c3.a), center.a));
            return half4(rgb, alpha);
        }

        half4 FragBlurMidForeground(DofVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 color = SampleSource(input.texcoord);
            color += SampleSource(input.offset1.xy) * 0.75h;
            color += SampleSource(input.offset1.zw) * 0.75h;
            color += SampleSource(input.offset2.xy) * 0.5h;
            color += SampleSource(input.offset2.zw) * 0.5h;
            color *= 0.2857142984867096h;
            half originalAlpha = SAMPLE_TEXTURE2D_X_LOD(
                _BlurFgMidTex,
                sampler_LinearClamp,
                input.texcoord,
                _BlitMipLevel).a;
            color.a = max(color.a, originalAlpha);
            return color;
        }

        half4 FragBlurLowForeground(DofVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 color =
                SampleSource(input.texcoord)
                + SampleSource(input.offset1.xy)
                + SampleSource(input.offset1.zw)
                + SampleSource(input.offset2.xy)
                + SampleSource(input.offset2.zw);
            color *= 0.2h;
            half originalAlpha = SAMPLE_TEXTURE2D_X_LOD(
                _BlurFgLowTex,
                sampler_LinearClamp,
                input.texcoord,
                _BlitMipLevel).a;
            color.a = max(color.a, originalAlpha);
            return color;
        }

        half4 FragApplySourceBackgroundForeground(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 source = SampleSource(input.texcoord);
            half backgroundWeight = max(source.a * 2.0h - 1.0h, 0.0h);
            half4 background = SAMPLE_TEXTURE2D_X_LOD(
                _BlurBgMidTex,
                sampler_LinearClamp,
                input.texcoord,
                _BlitMipLevel);
            half4 color = lerp(source, background, backgroundWeight);

            half foregroundWeight = max((1.0h - source.a) * 2.0h - 1.0h, 0.0h);
            half4 foreground = SAMPLE_TEXTURE2D_X_LOD(
                _BlurFgMidTex,
                sampler_LinearClamp,
                input.texcoord,
                _BlitMipLevel);
            foreground.a = min(max(foregroundWeight, foreground.a * 1.15h), 1.0h);
            return lerp(color, foreground, foreground.a);
        }

        half4 FragApplySourceBackground(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 source = SampleSource(input.texcoord);
            half backgroundWeight = max(source.a * 2.0h - 1.0h, 0.0h);
            half4 background = SAMPLE_TEXTURE2D_X_LOD(
                _BlurBgMidTex,
                sampler_LinearClamp,
                input.texcoord,
                _BlitMipLevel);
            return lerp(source, background, backgroundWeight);
        }
        ENDHLSL

        Pass
        {
            Name "Alpha_CoC"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAlphaCoc
            ENDHLSL
        }

        Pass
        {
            Name "Mid_DownSample_MRT"
            HLSLPROGRAM
            #pragma vertex VertDofDownsample
            #pragma fragment FragMidDownsample
            ENDHLSL
        }

        Pass
        {
            Name "Blur_Mid_Bg"
            HLSLPROGRAM
            #pragma vertex VertDofBlur
            #pragma fragment FragBlurBackground
            ENDHLSL
        }

        Pass
        {
            Name "Blur_Low_Bg"
            HLSLPROGRAM
            #pragma vertex VertDofBlur
            #pragma fragment FragBlurBackground
            ENDHLSL
        }

        Pass
        {
            Name "Downsample_With_Coc_Conserve"
            HLSLPROGRAM
            #pragma vertex VertDofDownsample
            #pragma fragment FragDownsampleConserveCoc
            ENDHLSL
        }

        Pass
        {
            Name "Blur_Mid_Fg"
            HLSLPROGRAM
            #pragma vertex VertDofBlur
            #pragma fragment FragBlurMidForeground
            ENDHLSL
        }

        Pass
        {
            Name "Blur_Low_Fg"
            HLSLPROGRAM
            #pragma vertex VertDofBlur
            #pragma fragment FragBlurLowForeground
            ENDHLSL
        }

        Pass
        {
            Name "Apply_Source_Bg_Fg"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragApplySourceBackgroundForeground
            ENDHLSL
        }

        Pass
        {
            Name "Apply_Source_Bg"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragApplySourceBackground
            ENDHLSL
        }
    }
}
