Shader "Hidden/CP/PostEffect/BoxBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        // Pass 0/1 are recovered byte-for-byte in behavior from the paired
        // JP 6.7.0 programs shader-0317-3432.spv and shader-0318-3432.spv.
        // The local labels are descriptive; the authoritative contract is
        // their indices in SekaiPostProcessPass.BoxBlur.
        HLSLINCLUDE
        #pragma target 3.0
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _BlitTexture_TexelSize;

        half3 SamplePair(float2 uv, float2 delta, float offset, half weight)
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

        half4 BoxBlur(float2 uv, float2 axis)
        {
            float2 delta = _BlitTexture_TexelSize.xy * axis;
            half3 color = SamplePair(uv, delta, 0.65, 0.204h);
            color += SamplePair(uv, delta, 2.43, 0.198h);
            color += SamplePair(uv, delta, 4.37, 0.098h);
            return half4(color, 1.0h);
        }

        half4 FragHorizontal(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return BoxBlur(input.texcoord, float2(1.0, 0.0));
        }

        half4 FragVertical(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return BoxBlur(input.texcoord, float2(0.0, 1.0));
        }
        ENDHLSL

        Pass
        {
            Name "Horizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHorizontal
            ENDHLSL
        }

        Pass
        {
            Name "Vertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVertical
            ENDHLSL
        }
    }
}
