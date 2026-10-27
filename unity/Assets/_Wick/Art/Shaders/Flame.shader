// Wick/Flame: Wick's flame and brazier fires (docs/asset-production.md, Stage 6).
//
// Additive: a Flame Core yellow centre over Candle Flame orange, fading softly at the edges and
// towards the tip (UV v runs base 0 to tip 1). The vertex shader sways the flame with layered
// sine noise and time, more the higher up the vertex and the fewer flickers Wick has left:
// _Flickers 3 is calm, 1 is frantic. WickView sets _Flickers and _Flare per frame.
//
// It reads the same visibility mask as Wick/StylisedLit, so a brazier in the dark stays hidden
// exactly when its tile does. No variants.
Shader "Wick/Flame"
{
    Properties
    {
        _CoreColor("Core (Flame Core)", Color) = (1, 0.886, 0.604, 1)
        _OuterColor("Outer (Candle Flame)", Color) = (1, 0.624, 0.110, 1)
        _Intensity("Intensity", Range(0, 4)) = 1.6
        _CoreSize("Core size", Range(0.05, 1)) = 0.45
        _SwaySpeed("Sway speed", Range(0, 10)) = 3
        _SwayAmount("Sway amount (m)", Range(0, 0.1)) = 0.025
        _Flickers("Flickers (3 calm, 1 frantic)", Range(0, 3)) = 3
        _Flare("Flare (joy, 0 to 1)", Range(0, 1)) = 0
        [Toggle] _IgnoreMask("Ignore visibility mask", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Flame"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WickVisibility.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _OuterColor;
                half _Intensity;
                half _CoreSize;
                half _SwaySpeed;
                half _SwayAmount;
                half _Flickers;
                half _Flare;
                half _IgnoreMask;
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
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            // 0 when calm (3 flickers), 1 when frantic (1 flicker).
            half Frenzy()
            {
                return saturate((3 - _Flickers) * 0.5);
            }

            Varyings Vert(Attributes input)
            {
                float height = saturate(input.uv.y);
                float3 objectWS = TransformObjectToWorld(float3(0, 0, 0));
                float t = _Time.y * _SwaySpeed * (1 + Frenzy() * 2.5);
                float phase = objectWS.x * 1.7 + objectWS.z * 2.3; // flames in a room don't sway in step
                float2 sway = float2(sin(t + phase) + 0.5 * sin(t * 2.3 + phase * 1.3), cos(t * 0.8 + phase) + 0.4 * sin(t * 3.1 + phase));
                float amount = _SwayAmount * (1 + Frenzy() * 2) * height * height;

                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz) + float3(sway.x, 0, sway.y) * amount;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = saturate(dot(normalWS, viewWS));       // 1 at the centre of the silhouette
                half height = saturate(input.uv.y);

                half core = smoothstep(1 - _CoreSize, 1, facing) * (1 - height * 0.6);
                half3 colour = lerp(_OuterColor.rgb, _CoreColor.rgb, core);
                half edge = smoothstep(0, 0.5, facing) * (1 - smoothstep(0.6, 1, height)); // soft rim and tip
                half brightness = _Intensity * (1 + _Flare * 0.8) * lerp(1, 0.75, Frenzy()) * step(0.5, _Flickers);

                half3 c = WickEmissive(colour * edge * brightness);
                // A fire on an unseen tile is as hidden as the tile; only a flat trace of it remains.
                c *= WickVisBrightness(input.positionWS, half3(0, 1, 0), _IgnoreMask);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
