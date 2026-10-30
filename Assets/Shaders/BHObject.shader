// Stylised lit shader for every prop in the world.
// Handles: soft toon lighting, fake AO, fresnel rim, hole light spill,
// "too big" warning pulse and the spaghettification used while an item is swallowed.
Shader "BlackHole/Object"
{
    Properties
    {
        [MainTexture] _BaseMap ("Atlas", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _UseAtlas ("Use Atlas", Float) = 1
        [HDR] _Emissive ("Emissive", Color) = (0,0,0,0)
        _Pulse ("Emissive Pulse", Float) = 0
        _AOScale ("AO Scale", Float) = 0.8
        _AOBias ("AO Bias", Float) = 0.0
        _AOMin ("AO Min", Float) = 0.55
        _Fall ("Fall", Range(0,1)) = 0
        _ObjHeight ("Object Height (local)", Float) = 1
        _FallPivot ("Fall Pivot", Vector) = (0,0,0,0)
        _Warn ("Too Big Warning", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "BlackHoleCommon.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _UseAtlas;
                half4 _Emissive;
                half _Pulse;
                half _AOScale;
                half _AOBias;
                half _AOMin;
                half _Fall;
                half _ObjHeight;
                float4 _FallPivot;
                half _Warn;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half ao : TEXCOORD3;
                half hn : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(IN.normalOS);
                float pivotY = unity_ObjectToWorld._m13;
                o.ao = lerp(_AOMin, 1.0, saturate((wp.y - pivotY) * _AOScale + _AOBias));

                float hn = saturate(IN.positionOS.y / max(_ObjHeight, 0.001));
                if (_Fall > 0.0001)
                {
                    float k = _Fall;
                    float3 pivot = _FallPivot.xyz;
                    float3 rel = wp - pivot;
                    float ang = k * k * 5.5 * (1.25 - hn);
                    rel.xz = Rot2(rel.xz, ang);
                    nws.xz = Rot2(nws.xz, ang);
                    rel.xz *= lerp(1.0, 0.16, k * (1.0 - hn * 0.55));   // pinch like a funnel
                    rel.y *= 1.0 + 0.9 * k * k;                          // stretch
                    wp = pivot + rel;
                    float2 toHole = _BH_Hole.xz - pivot.xz;
                    wp.xz += toHole * (k * k * (1.0 - hn) * 0.3);
                }

                o.positionWS = wp;
                o.normalWS = normalize(nws);
                o.positionCS = TransformWorldToHClip(wp);
                o.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                o.hn = hn;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 albedo = _BaseColor.rgb;
                if (_UseAtlas > 0.5)
                    albedo *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;

                float3 N = normalize(IN.normalWS);
                float3 L = normalize(_BH_LightDir.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                float ndl = dot(N, L) * 0.5 + 0.5;
                half lit = smoothstep(0.22, 0.85, ndl);
                half3 hemi = lerp(_BH_AmbGround.rgb, _BH_AmbSky.rgb, N.y * 0.5 + 0.5);
                half3 col = albedo * (hemi + _BH_LightColor.rgb * lit) * IN.ao;

                half fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                col += _BH_RimColor.rgb * fres * 0.55;

                float3 H = normalize(L + V);
                col += _BH_LightColor.rgb * pow(saturate(dot(N, H)), 48.0) * 0.18 * lit;

                // light spilling out of the hole onto nearby objects
                float dh = distance(IN.positionWS.xz, _BH_Hole.xz);
                float nearK = saturate(1.0 - (dh - _BH_Hole.w) / (_BH_Hole.w * 0.9 + 0.5));
                col += _BH_HoleColor.rgb * nearK * nearK * (0.35 + 0.65 * (1.0 - IN.hn)) * 0.7;

                // objects too big for the hole pulse red
                half pulse = 0.5 + 0.5 * sin(_Time.y * 9.0);
                col = lerp(col, half3(1.0, 0.18, 0.28) * (0.55 + 0.6 * pulse) + col * 0.3, _Warn * saturate(fres * 0.9 + 0.18));

                col += _Emissive.rgb * (1.0 + _Pulse * (0.5 + 0.5 * sin(_Time.y * 6.0)) * 1.3);

                // fade to purple darkness while falling
                col = lerp(col, _BH_HoleColor.rgb * 0.25, _Fall * (1.0 - IN.hn) * 0.8);

                col = ApplyFog(col, IN.positionWS);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
