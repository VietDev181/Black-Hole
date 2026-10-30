// The black hole itself: a quad lying on the ground, centred on the hole.
// Inside the radius: spiralling void with parallax stars. Outside: accretion ring,
// hot spots and a dark halo that eats into the ground. Items sunk below the plane
// are hidden by this quad, which is what makes them "disappear into the hole".
Shader "BlackHole/Hole"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "Hole"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "BlackHoleCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float2 p = (IN.positionWS.xz - _BH_Hole.xz) / max(_BH_Hole.w, 0.001);
                float r = length(p);
                float a = atan2(p.y, p.x);

                half3 accent = _BH_HoleColor.rgb;
                half3 hot = _BH_HoleColor2.rgb;

                // ---- interior ----
                float inside = 1.0 - smoothstep(0.985, 1.0, r);
                float swirl = a + log(max(r, 0.03)) * 3.0 - t * 1.4;
                float arms = 0.5 + 0.5 * sin(swirl * 3.0);
                float rimK = smoothstep(0.15, 1.0, r);
                half3 inner = accent * arms * rimK * rimK * 0.55;

                float2 rp = Rot2(p, t * 0.6 / (r + 0.25));
                float2 g = rp * 15.0;
                float2 gid = floor(g);
                float h = Hash21(gid);
                float star = step(0.955, h) * smoothstep(0.5, 0.0, length(frac(g) - 0.5));
                star *= 0.6 + 0.4 * sin(t * (3.0 + h * 5.0) + h * 40.0);
                inner += star * smoothstep(0.12, 0.85, r) * half3(0.85, 0.9, 1.0);

                float core = smoothstep(0.55, 0.0, r);
                inner *= 1.0 - core * 0.95;                       // black centre

                // ---- accretion ring ----
                float ring = exp(-pow((r - 1.02) * 7.5, 2.0));
                float spot = 0.55 + 0.45 * sin(a * 2.0 - t * 2.2) * sin(a * 3.0 + t * 1.3 + 1.0);
                half3 ringCol = lerp(accent, hot, saturate(spot * ring * 1.3)) * ring * (1.2 + 0.9 * spot);
                float halo = exp(-(r - 1.0) * 3.2) * step(1.0, r);
                half3 haloCol = accent * halo * 0.55;

                float fadeOut = 1.0 - smoothstep(1.7, 2.15, r);
                half3 glow = (ringCol + haloCol) * fadeOut;

                float shadowA = halo * 0.55 * fadeOut;             // darkens the ground near the lip
                float alpha = saturate(inside + (1.0 - inside) * shadowA);

                half3 rgb = inner * inside + glow;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
