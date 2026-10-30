// UI text with a vertical gradient and an animated shine sweep.
// Works with the legacy dynamic font atlas (alpha channel holds the glyphs).
Shader "BlackHole/UIText"
{
    Properties
    {
        [HideInInspector] _MainTex ("Font Texture", 2D) = "white" {}
        _ColTop ("Top", Color) = (1,1,1,1)
        _ColBot ("Bottom", Color) = (0.8,0.75,1,1)
        _TextH ("Text Height", Float) = 100
        _TextW ("Text Width", Float) = 300
        _Shine ("Shine", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _ColTop;
            fixed4 _ColBot;
            float _TextH;
            float _TextW;
            float _Shine;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 lp : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.lp = v.vertex.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a;
                float gy = saturate(i.lp.y / max(_TextH, 1.0) + 0.5);
                fixed3 g = lerp(_ColBot.rgb, _ColTop.rgb, gy);
                float gx = i.lp.x / max(_TextW, 1.0) + 0.5;
                float sweep = exp(-pow((gx - frac(_Time.y * 0.25) * 1.8 + 0.4) * 8.0, 2.0));
                g += sweep * 0.4 * _Shine;
                return fixed4(g * i.color.rgb, a * i.color.a);
            }
            ENDCG
        }
    }
}
