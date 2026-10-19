// Wick/StylisedLit: characters, traps, guardians, props and tiles (docs/asset-production.md, Stage 6).
//
// The painted albedo carries the lighting; real lights only add warmth. Light arrives through a
// soft two-step ramp (lit / shadow), ambient comes from the scene's trilight gradient (vault
// blue-violet), and a warm Candle Flame rim separates shapes from the dark floor, strongest near
// Wick's point light. Mask texture: R = emissive, G = rim strength, B = spare. No specular.
//
// Visibility is Wick.Sim's: tiles outside Wick's light show a flat dark fraction of their colour
// (see WickVisibility.hlsl). This shader never computes visibility itself.
//
// Variants: _ADDITIONAL_LIGHTS (Wick's point light), _CLUSTER_LIGHT_LOOP (Forward+), and the
// local _NORMALMAP toggle, off by default.
Shader "Wick/StylisedLit"
{
    Properties
    {
        _BaseMap("Albedo (painted)", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _MaskMap("Mask (R emissive, G rim)", 2D) = "white" {}
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)

        [Header(Ramp)]
        _RampThreshold("Ramp threshold", Range(0, 1)) = 0.35
        _RampSoftness("Ramp softness", Range(0.001, 0.5)) = 0.12
        _ShadowStrength("Shadow side brightness", Range(0, 1)) = 0.45

        [Header(Rim)]
        _RimColor("Rim (Candle Flame)", Color) = (1, 0.624, 0.110, 1)
        _RimStrength("Rim strength", Range(0, 2)) = 0.35
        _RimPower("Rim width", Range(0.5, 8)) = 3
        _RimNearLight("Extra rim near Wick's light", Range(0, 4)) = 1.5

        [Header(Options)]
        [Toggle(_NORMALMAP)] _UseNormalMap("Normal map", Float) = 0
        [NoScaleOffset][Normal] _BumpMap("Normal map", 2D) = "bump" {}
        [Toggle] _IgnoreMask("Ignore visibility mask", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _RampThreshold;
            half _RampSoftness;
            half _ShadowStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _RimNearLight;
            half _IgnoreMask;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma shader_feature_local _NORMALMAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WickVisibility.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                #if defined(_NORMALMAP)
                float4 tangentWS : TEXCOORD3;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #if defined(_NORMALMAP)
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                #endif
                return output;
            }

            // Two-step ramp: the shadow side keeps _ShadowStrength of the light, so the warm pool wraps
            // round shapes softly and the painted shading still reads.
            half Ramp(half ndl)
            {
                return lerp(_ShadowStrength, 1, smoothstep(_RampThreshold - _RampSoftness, _RampThreshold + _RampSoftness, ndl));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                #if defined(_NORMALMAP)
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv));
                float3 bitangent = input.tangentWS.w * cross(normalWS, input.tangentWS.xyz);
                normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, normalWS)));
                #endif

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 light = SampleSH(normalWS); // the scene's trilight gradient (set per theme)

                Light mainLight = GetMainLight();
                light += mainLight.color * mainLight.distanceAttenuation * Ramp(dot(normalWS, mainLight.direction));

                half nearLight = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, input.positionWS);
                    light += l.color * l.distanceAttenuation * Ramp(dot(normalWS, l.direction));
                    nearLight += l.distanceAttenuation;
                LIGHT_LOOP_END
                #endif

                half rim = pow(1 - saturate(dot(normalWS, viewWS)), _RimPower) * mask.g * _RimStrength * (1 + saturate(nearLight) * _RimNearLight);
                half3 emission = _EmissionColor.rgb * mask.r;

                half3 lit = albedo * (light + _WickLitBoost) + _RimColor.rgb * rim + emission;
                half3 unlit = albedo * _WickVisDark;
                return half4(lerp(unlit, lit, WickVisibility(input.positionWS, normalWS, _IgnoreMask)), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
