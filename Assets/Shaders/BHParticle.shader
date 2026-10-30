// Additive procedural particle. _Shape: 0 = soft glow, 1 = ring (shockwave), 2 = spark star.
Shader "BlackHole/Particle"
{
    Properties
    {
        _Shape ("Shape", Float) = 0
        _Intensity ("Intensity", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "Particle"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Shape;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.color = IN.color;
                o.uv = IN.uv;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 q = IN.uv * 2.0 - 1.0;
                float r = length(q);
                float a;
                if (_Shape < 0.5)
                {
                    a = pow(saturate(1.0 - r), 2.0);
                }
                else if (_Shape < 1.5)
                {
                    a = exp(-pow((r - 0.8) * 9.0, 2.0)) * (1.0 - smoothstep(0.88, 1.0, r));
                }
                else
                {
                    float cross1 = exp(-abs(q.x) * 14.0) * saturate(1.0 - abs(q.y));
                    float cross2 = exp(-abs(q.y) * 14.0) * saturate(1.0 - abs(q.x));
                    float core = pow(saturate(1.0 - r), 3.0);
                    a = saturate(cross1 + cross2 + core * 1.2);
                }
                half3 c = IN.color.rgb * (a * IN.color.a * _Intensity);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
