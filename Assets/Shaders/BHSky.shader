// Deep-space skybox: the island floats in a nebula with twinkling stars.
// The camera looks down, so everything is projected onto a plane below the island.
Shader "BlackHole/Sky"
{
    Properties
    {
        _SpaceCol ("Space", Color) = (0.02,0.01,0.05,1)
        _NebulaA ("Nebula A", Color) = (0.25,0.1,0.5,1)
        _NebulaB ("Nebula B", Color) = (0.9,0.3,0.6,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "BlackHoleCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SpaceCol;
                half4 _NebulaA;
                half4 _NebulaB;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.dir = IN.positionOS.xyz;
                return o;
            }

            half3 Stars(float2 pl, float scale, float density, float t)
            {
                float2 sp = pl * scale;
                float2 id = floor(sp);
                float h = Hash21(id);
                float2 off = float2(Hash21(id + 3.1), Hash21(id + 9.7));
                float sd = length(frac(sp) - off * 0.8 - 0.1);
                float s = step(density, h) * smoothstep(0.13, 0.0, sd);
                s *= 0.55 + 0.45 * sin(t * (2.0 + h * 6.0) + h * 50.0);
                half3 tint = lerp(half3(0.8, 0.9, 1.0), half3(1.0, 0.85, 0.7), Hash21(id + 5.5));
                return s * tint;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);
                float t = _Time.y;
                float2 pl = d.xz / max(abs(d.y), 0.08);

                float n1 = Fbm(pl * 0.9 + float2(t * 0.01, 0.0));
                float n2 = Fbm(pl * 2.1 + 7.0 - t * 0.015);
                half3 col = _SpaceCol.rgb;
                col += _NebulaA.rgb * pow(n1, 2.2) * 1.9;
                col += _NebulaB.rgb * pow(n2, 2.6) * 1.5;

                col += Stars(pl, 22.0, 0.93, t) * 1.2;
                col += Stars(pl + 13.0, 55.0, 0.9, t * 0.7) * 0.5;

                col += _NebulaB.rgb * 0.45 * pow(saturate(1.0 - abs(d.y)), 6.0);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
