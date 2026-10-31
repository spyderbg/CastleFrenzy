// 2D/billboard sprite shader reproducing Irrlicht's 2D materials.
// _Gradient = 1 reproduces EMT_A8I8_GRADIENT: rgb = vcol*tex + _Color2*(1-tex), a = tex.a*vcol.a.
Shader "CastleFrenzy/Sprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color2 ("Gradient color", Color) = (0,0,0,0)
        _Gradient ("Gradient", Float) = 0
        _VertexAlpha ("Alpha from vertex color only", Float) = 0
        _SrcBlend ("Src", Float) = 5
        _DstBlend ("Dst", Float) = 10
        _ZTest ("ZTest", Float) = 8
        _ZWrite ("ZWrite", Float) = 0
        _Cutoff ("Cutoff", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull Off
            Lighting Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color2;
            float _Gradient;
            float _VertexAlpha;
            float _Cutoff;
            fixed4 _Emissive;
            float _UseEmissive;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                fixed4 c;
                if (_Gradient > 0.5)
                {
                    c.rgb = i.color.rgb * t.rgb + _Color2.rgb * (1 - t.rgb);
                    c.a = t.a * i.color.a;
                }
                else
                {
                    c = t * i.color;
                    if (_VertexAlpha > 0.5) c.a = i.color.a;
                }
                if (_UseEmissive > 0.5) c.rgb = saturate(c.rgb * _Emissive.rgb);
                clip(c.a - _Cutoff);
                return c;
            }
            ENDCG
        }
    }
}
