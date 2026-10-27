// Wick/ShadowBlob: a soft contact shadow under a character, guardian or prop (docs/m10-2-lighting.md:
// no realtime shadows on mobile). A quad on the floor that darkens what's under it by multiplying,
// strongest in the middle. Hidden with its owner when the owner's tile is dark.
Shader "Wick/ShadowBlob"
{
    Properties
    {
        _Strength("Strength", Range(0, 1)) = 0.55
        _Falloff("Falloff", Range(0.5, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ShadowBlob"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Strength;
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
                half shade = pow(1 - d, _Falloff) * _Strength;
                return half4((1 - shade).xxx, 1);
            }
            ENDHLSL
        }
    }
}
