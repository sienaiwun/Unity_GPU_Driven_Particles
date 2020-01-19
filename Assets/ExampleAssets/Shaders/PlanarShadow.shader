Shader "Custom/PlanarShadow"
{
    Properties
    {   _Height("_Height", Range(-10,10)) = 0
        _Color ("ShadowColor", Color) = (1,1,1,1)
        _planerLightDir("_planerLightDir", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" }
        Pass
        {
           Tags { "LightMode" = "LightweightForward"}
            Cull Front
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
           #pragma vertex vert
           #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _planerLightDir;
            float _Height;
            CBUFFER_END
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            struct VSInput
            {
                float4 vertex : POSITION;
                float4 tangent : TANGENT;
                float3 normal : NORMAL;
                float4 texcoord : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
            };
            v2f vert(VSInput v)
            {
                v2f o;
                float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
                float3 shadowVertexPosWS = positionWS.xyz + (_Height - positionWS.y) / _planerLightDir.y * _planerLightDir.xyz;
                o.pos = TransformWorldToHClip(shadowVertexPosWS);
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
