// Every panel / button / bar / star / joystick in the UI is drawn by this shader:
// SDF rounded rectangle (or 5-point star), vertical gradient, gloss, animated shine, border, fill clip.
Shader "BlackHole/UIPanel"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _ColTop ("Top", Color) = (1,1,1,1)
        _ColBot ("Bottom", Color) = (1,1,1,1)
        _Border ("Border", Color) = (1,1,1,0)
        _BorderW ("Border Width", Float) = 3
        _Radius ("Radius", Float) = 20
        _Size ("Size", Vector) = (100,100,0,0)
        _Shine ("Shine", Float) = 0
        _Fill ("Fill", Range(0,1)) = 1
        _Shape ("Shape (0 rect, 1 star)", Float) = 0
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

            fixed4 _ColTop;
            fixed4 _ColBot;
            fixed4 _Border;
            float _BorderW;
            float _Radius;
            float4 _Size;
            float _Shine;
            float _Fill;
            float _Shape;

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float sdStar5(float2 p, float r, float rf)
            {
                const float2 k1 = float2(0.809016994375, -0.587785252292);
                const float2 k2 = float2(-k1.x, k1.y);
                p.x = abs(p.x);
                p -= 2.0 * max(dot(k1, p), 0.0) * k1;
                p -= 2.0 * max(dot(k2, p), 0.0) * k2;
                p.x = abs(p.x);
                p.y -= r;
                float2 ba = rf * float2(-k1.y, k1.x) - float2(0, 1);
                float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
                return length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 sz = _Size.xy;
                float2 pp = (i.uv - 0.5) * sz;
                float d;
                if (_Shape < 0.5)
                {
                    float rad = min(_Radius, min(sz.x, sz.y) * 0.5);
                    float2 q = abs(pp) - (sz * 0.5 - rad);
                    d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - rad;
                }
                else
                {
                    d = sdStar5(pp, min(sz.x, sz.y) * 0.46, 0.45);
                }
                d = max(d, (i.uv.x - _Fill) * sz.x);
                float inner = saturate(0.5 - d);

                fixed4 c = lerp(_ColBot, _ColTop, i.uv.y);
                c.rgb += smoothstep(0.55, 1.0, i.uv.y) * 0.10;

                float band = frac(_Time.y * 0.4);
                float x = i.uv.x * 0.8 + i.uv.y * 0.35;
                float sh = exp(-pow((x - band * 1.6 + 0.2) * 7.0, 2.0));
                c.rgb += sh * 0.28 * _Shine;

                float bm = inner * saturate(d + _BorderW + 0.5);
                c.rgb = lerp(c.rgb, _Border.rgb, bm * _Border.a);

                c.a *= inner * i.color.a;
                c.rgb *= i.color.rgb;
                return c;
            }
            ENDCG
        }
    }
}
