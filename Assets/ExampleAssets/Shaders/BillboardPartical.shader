Shader "Custom/Billboard Particles"
{
    Properties
    {
        _MainTex("Particle Sprite", 2D) = "white" {}
        _SizeMul("Size Multiplier", Float) = 1
    }

        SubShader
        {
            Pass
            {
                Cull Off
                Lighting Off
                Zwrite Off

            Blend SrcAlpha OneMinusSrcAlpha
            //Blend One OneMinusSrcAlpha
           // Blend One One
            //Blend OneMinusDstColor One

            LOD 200

            Tags
            {
                "RenderType" = "Transparent"
                "Queue" = "Transparent"
                "IgnoreProjector" = "True"
            }

            CGPROGRAM

            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ENABLE_SORTINT

            #include "UnityCG.cginc"
            #include "common.hlsli"
            uniform sampler2D _MainTex;

            StructuredBuffer<float3> quad;
            StructuredBuffer<uint> indexBuffer;

            struct v2f
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
                float4 col : COLOR;
            };


            StructuredBuffer<Particle> particles;
            v2f vert(uint id : SV_VertexID, uint inst : SV_InstanceID)
            {
                v2f o;
                float2 	Tex = float2(uint2(id, id << 1) & 2);
                float2 Pos = float4(lerp(float2(-1, 1), float2(1, -1), Tex), 0, 1);

                float3 local_positon = quad[id];
#if defined(ENABLE_SORTINT)
                Particle particle = particles[indexBuffer[inst] & 0x0000ffff];
#else
                Particle particle = particles[inst];
#endif
               
               
                o.pos = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_V, float4(local_positon.xyz*particle.size + particle.position, 1.0f)));;

                o.uv = local_positon ;

                o.col =  particles[inst].color*particles[inst].alive;

                return o;
            }

            fixed4 frag(v2f i) : COLOR
            {
                return i.col*tex2D(_MainTex, i.uv);
            }

            ENDCG
        }
        }
}