// Wick/Atmosphere: cheap cards for the vault's air (docs/m10-2-lighting.md, layer 7). _Shape 0 is a
// low fog patch (a soft, slowly breathing ellipse, alpha-blended); 1 is a light shaft (bright at the
// top, fading down, soft at the sides, additive). Output is premultiplied: fog blends One /
// OneMinusSrcAlpha, shafts One / One. Colour and strength come from _EmissionColor
// (set per renderer from the LightingProfile). Fog and shafts are part of the room, so the visibility
// mask dims them like the room: they never reveal a dark room. No texture, no lighting.
Shader "Wick/Atmosphere"
{
    Properties
    {
        [HDR] _EmissionColor("Colour (a = strength)", Color) = (0.36, 0.3, 0.55, 0.25)
        [Enum(Fog, 0, Shaft, 1)] _Shape("Shape", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WickVisibility.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                half _Shape;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                half a;
                if (_Shape < 0.5)
                {
                    // Fog: a soft patch that breathes slowly, each one out of step with the others.
                    float phase = i.positionWS.x * 1.3 + i.positionWS.z * 0.7;
                    float breathe = 0.85 + 0.15 * sin(_Time.y * 0.5 + phase);
                    a = saturate(1 - length(c * float2(1, 1.3)) * 2 / breathe);
                    a *= a;
                }
                else
                {
                    // Shaft: brightest where it enters at the top, gone by the floor, soft-edged.
                    half across = saturate(1 - abs(c.x) * 2);
                    a = across * across * saturate(i.uv.y) * saturate(i.uv.y * 4);
                }

                // Floor-level brightness of the tile under it: dark rooms keep their fog dark.
                half vis = WickVisBrightness(float3(i.positionWS.x, 0, i.positionWS.z), half3(0, 1, 0), 0);
                a *= _EmissionColor.a;
                return half4(_EmissionColor.rgb * vis * a, a);
            }
            ENDHLSL
        }
    }
}
