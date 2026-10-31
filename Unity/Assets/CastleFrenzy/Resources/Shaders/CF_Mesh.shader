// 3D mesh shader reproducing the fixed-function lighting the game uses:
// the scene light has black diffuse, so lit color = emissive + materialAmbient * lightAmbient.
// Vertex positions may be GPU-skinned (_Skinned = 1) with up to 4 influences.
Shader "CastleFrenzy/Mesh"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SrcBlend ("Src", Float) = 1
        _DstBlend ("Dst", Float) = 0
        _ZTest ("ZTest", Float) = 4
        _ZWrite ("ZWrite", Float) = 1
        _Cull ("Cull", Float) = 0
        _Cutoff ("Cutoff", Float) = -1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Cutoff;
            float _Lighting;
            float _Skinned;
            fixed4 _Emissive;
            fixed4 _Ambient;
            fixed4 _Diffuse;
            fixed4 _LightAmbient;
            float4 _TexRow0;
            float4 _TexRow1;
            float4x4 _Bones[64];

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 boneIdx : TEXCOORD1;
                float4 boneW : TEXCOORD2;
                fixed4 color : COLOR;
            };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                float4 p = v.vertex;
                if (_Skinned > 0.5)
                {
                    float4 s = mul(_Bones[(int)v.boneIdx.x], p) * v.boneW.x;
                    s += mul(_Bones[(int)v.boneIdx.y], p) * v.boneW.y;
                    s += mul(_Bones[(int)v.boneIdx.z], p) * v.boneW.z;
                    s += mul(_Bones[(int)v.boneIdx.w], p) * v.boneW.w;
                    p = float4(s.xyz, 1);
                }
                o.pos = UnityObjectToClipPos(p);
                // Irrlicht texture matrix (translation in the third column), upper-left UV origin.
                float2 uv = float2(dot(_TexRow0.xyz, float3(v.uv, 1)), dot(_TexRow1.xyz, float3(v.uv, 1)));
                o.uv = float2(uv.x, 1 - uv.y);
                fixed4 c;
                if (_Lighting > 0.5)
                {
                    c.rgb = saturate(_Emissive.rgb + _Ambient.rgb * _LightAmbient.rgb);
                    c.a = _Diffuse.a * v.color.a;
                }
                else
                {
                    c = v.color * _Diffuse;
                }
                o.color = c;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                clip(c.a - _Cutoff);
                return c;
            }
            ENDCG
        }
    }
}
