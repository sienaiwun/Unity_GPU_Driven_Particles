Shader "Hidden/Custom/HizBlit"
{
    SubShader
    {
        Pass
        {
            Name "Blit"
            Cull Off
        ZTest Always
            ZWrite Off
            HLSLPROGRAM
        
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                half4 positionCS    : SV_POSITION;
                half2 uv            : TEXCOORD0;
            };

            Texture2D _CameraDepthTexture;
            SamplerState sampler_CameraDepthTexture;
            Texture2D _CameraDepthAttachment;
            SamplerState sampler_CameraDepthAttachment;
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                if (_ProjectionParams.x < 0)
                    output.uv.y = 1 - output.uv.y;
                return output;
            }

            float Fragment(Varyings input) : SV_Target
            {
                #define SAMPLE(uv) SAMPLE_DEPTH_TEXTURE(_CameraDepthAttachment, sampler_CameraDepthAttachment, uv)
                float camDepth = SAMPLE(input.uv);// _CameraDepthAttachment.Sample(sampler_CameraDepthAttachment, input.uv).r;
                return input.uv.y;
            }
            ENDHLSL
        }


        Pass
        {
            Name "Gather"
            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex Vertex
            #pragma fragment FragmentGather
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                half4 positionCS    : SV_POSITION;
                half2 uv            : TEXCOORD0;
            };

            Texture2D _MipSourceTex;
            SamplerState sampler_MipSourceTex;

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float FragmentGather(Varyings input) : SV_Target
            {
                float4 r = _MipSourceTex.GatherRed(sampler_MipSourceTex, input.uv);
                float minimum = min(min(min(r.x, r.y), r.z), r.w);
                return  minimum;
            }
            ENDHLSL
        }
    }
}
