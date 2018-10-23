Shader "Custom/TextureAnimPlayer-InstancingIndirect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PosTex("position texture", 2D) = "black"{}
        _NmlTex("normal texture", 2D) = "white"{}
        _Length ("animation length", Float) = 1
        [Toggle(ANIM_LOOP)] _Loop("loop", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100 Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile ___ ANIM_LOOP

            #include "UnityCG.cginc"

            #define ts _PosTex_TexelSize

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct PlayData
            {
                float CurrentKeyFrame;
                float4x4 LocalToWorld;
            };

            sampler2D _MainTex, _PosTex, _NmlTex;
            float4 _PosTex_TexelSize;
            float _Length;
            StructuredBuffer<PlayData> _PlayDataBuffer;
            
            v2f vert (appdata v, uint vid : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float t = _PlayDataBuffer[instanceID].CurrentKeyFrame / _Length;
#if ANIM_LOOP
                t = fmod(t, 1.0);
#else
                t = saturate(t);
#endif
                float x = (vid + 0.5) * ts.x;
                float y = t;
                float4 pos = tex2Dlod(_PosTex, float4(x, y, 0, 0));
                float3 normal = tex2Dlod(_NmlTex, float4(x, y, 0, 0));

                v2f o;
                o.vertex = UnityObjectToClipPos(mul(_PlayDataBuffer[instanceID].LocalToWorld, pos));
                o.normal = UnityObjectToWorldNormal(mul(_PlayDataBuffer[instanceID].LocalToWorld, normal));
                o.uv = v.uv;
                return o;
            }
            
            half4 frag (v2f i) : SV_Target
            {
                half diff = dot(i.normal, float3(0,1,0))*0.5 + 0.5;
                half4 col = tex2D(_MainTex, i.uv);
                return diff * col;
            }
            ENDCG
        }
    }
}
