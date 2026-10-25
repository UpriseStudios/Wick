// Wick/LightBlob: a soft additive circle on the floor that looks like light and costs almost nothing
// (docs/m10-2-lighting.md): braziers' pools, which can go out mid-raid so can't be baked, and the
// entrance's "way out" glow. Drawn on a quad whose UVs span the circle. Colour times strength comes
// from _EmissionColor (set per renderer by ReadableGlow), alpha scales it. Always readable, so it
// ignores the visibility mask. No texture, no lighting, no variants.
Shader "Wick/LightBlob"
{
    Properties
    {
        [HDR] _EmissionColor("Colour", Color) = (1, 0.55, 0.2, 0.6)
        _Falloff("Falloff", Range(0.5, 4)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "LightBlob"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                half _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half d = saturate(length(i.uv - 0.5) * 2);
                half a = pow(1 - d, _Falloff) * _EmissionColor.a;
                return half4(_EmissionColor.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
