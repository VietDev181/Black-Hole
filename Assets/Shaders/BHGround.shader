// Procedural ground. Three styles (city / graveyard / dungeon), the hole is cut out
// of it with discard, plus lip shading, gravity ripples and a glowing map border.
Shader "BlackHole/Ground"
{
    Properties
    {
        _ColA ("Color A", Color) = (0.16,0.17,0.21,1)
        _ColB ("Color B", Color) = (0.55,0.57,0.62,1)
        _ColC ("Color C", Color) = (1,0.8,0.3,1)
        _Style ("Style (0 city, 1 grave, 2 dungeon)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "Ground"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "BlackHoleCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColA;
                half4 _ColB;
                half4 _ColC;
                half _Style;
            CBUFFER_END

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

            half3 CityGround(float2 p)
            {
                float cell = 18.0;
                float roadW = 6.0;
                float2 q = abs(frac(p / cell + 0.5) - 0.5) * cell;
                float dR = min(q.x, q.y);
                bool isVert = q.x < q.y;

                float grain = Noise2(p * 9.0) * 0.5 + Noise2(p * 31.0) * 0.5;
                half3 asphalt = _ColA.rgb * (0.85 + 0.3 * grain);

                float2 tf = abs(frac(p * 0.5) - 0.5);
                float tile = smoothstep(0.46, 0.5, max(tf.x, tf.y));
                half3 block = _ColB.rgb * (0.8 + 0.25 * Noise2(floor(p * 0.5) * 1.7) + 0.12 * grain);
                block = lerp(block, block * 0.7, tile);

                float hr = roadW * 0.5;
                float curb = smoothstep(hr, hr + 0.12, dR);
                half3 col = lerp(asphalt, block, curb);

                float curbLine = smoothstep(0.12, 0.0, abs(dR - hr - 0.06));
                col = lerp(col, _ColB.rgb * 1.25, curbLine * 0.8);

                float along = isVert ? p.y : p.x;
                float dash = step(0.5, frac(along * 0.35));
                col = lerp(col, _ColC.rgb, smoothstep(0.09, 0.05, dR) * dash);

                float el = smoothstep(0.05, 0.02, abs(dR - (hr - 0.35)));
                col = lerp(col, _ColB.rgb * 1.1, el * 0.6);
                return col;
            }

            half3 GraveGround(float2 p)
            {
                float n = Fbm(p * 0.28);
                float n2 = Fbm(p * 1.3 + 11.0);
                half3 col = lerp(_ColA.rgb, _ColB.rgb, smoothstep(0.3, 0.75, n));
                col *= 0.8 + 0.4 * n2;
                float specks = step(0.82, Noise2(p * 7.0));
                col = lerp(col, _ColC.rgb * 0.45, specks * 0.18 * n2);
                float path = smoothstep(0.03, 0.0, abs(Fbm(p * 0.12 + 3.0) - 0.5) - 0.02);
                col = lerp(col, _ColB.rgb * 1.4, path * 0.35);
                float mist = Fbm(p * 0.11 + float2(_Time.y * 0.06, _Time.y * 0.03));
                col += half3(0.35, 0.45, 0.6) * smoothstep(0.45, 0.9, mist) * 0.18;
                return col;
            }

            half3 DungeonGround(float2 p)
            {
                float ts = 2.5;
                float2 g = p / ts;
                float2 id = floor(g);
                float2 f = frac(g);
                float2 e = min(f, 1.0 - f) * ts;
                float edgeD = min(e.x, e.y);
                float grout = smoothstep(0.0, 0.1, edgeD);
                float rnd = Hash21(id);
                half3 tileCol = lerp(_ColA.rgb, _ColB.rgb, rnd * 0.9);
                tileCol *= 0.85 + 0.3 * Noise2(p * 6.0 + id);
                tileCol += smoothstep(0.35, 0.1, edgeD) * 0.12;
                float c = abs(Noise2(p * 1.1 + id * 3.1) - 0.5);
                tileCol *= 1.0 - 0.35 * smoothstep(0.03, 0.0, c) * step(0.6, rnd);
                half3 col = lerp(_ColA.rgb * 0.35, tileCol, grout);
                float pool = Noise2(p * 0.15);
                col += _ColC.rgb * smoothstep(0.65, 0.95, pool) * 0.08;
                return col;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 wp = IN.positionWS;
                float2 p = wp.xz;
                float R = _BH_Hole.w;
                float dh = distance(p, _BH_Hole.xz);
                if (dh < R * 0.985) discard;

                float2 ed = _BH_Map.xy - abs(p);
                float edge = min(ed.x, ed.y);
                if (edge < 0.0) discard;

                half3 col;
                if (_Style < 0.5) col = CityGround(p);
                else if (_Style < 1.5) col = GraveGround(p);
                else col = DungeonGround(p);

                // simple lighting for a flat surface
                half3 lightTerm = _BH_AmbSky.rgb * 0.75 + _BH_LightColor.rgb * saturate(normalize(_BH_LightDir.xyz).y) * 0.75 + 0.14;
                col *= lightTerm;

                // hole lip: dark gravity well + animated ripples
                float lipD = max(dh - R, 0.0);
                float lip = exp(-lipD / (R * 0.55 + 0.2));
                col *= 1.0 - 0.6 * lip;
                float rip = sin(dh * 6.0 - _Time.y * 5.0) * 0.5 + 0.5;
                col += _BH_HoleColor.rgb * lip * lip * (0.25 + 0.2 * rip);

                // glowing border of the map
                float bw = saturate(1.0 - edge / 0.35);
                col = lerp(col, _BH_Border.rgb * 1.4, bw);
                col += _BH_Border.rgb * exp(-edge * 2.0) * 0.22;
                col *= lerp(0.55, 1.0, smoothstep(0.0, 4.0, edge));

                col = ApplyFog(col, wp);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
