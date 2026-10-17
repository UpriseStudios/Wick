// Placeholder vault material: simple Lambert lighting (ambient + main + additional lights,
// so Wick's point light warms nearby surfaces), multiplied by the visibility mask.
//
// The mask is Wick.Sim's lit-tile set, written to _WickVisMask (R8, one texel per tile) by
// VisibilityMask. This shader never decides visibility; it only reads that texel exactly
// (LOAD, no filtering) for the tile under each fragment. Unlit tiles are drawn at _WickVisDark.
Shader "Wick/VaultLit"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        [Toggle] _IgnoreMask("Ignore visibility mask", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _IgnoreMask;
            CBUFFER_END

            // Set globally by VisibilityMask.
            TEXTURE2D(_WickVisMask);
            float4 _WickVisGrid; // x = width, y = height, z = 1 when a mask is active
            half _WickVisDark;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // Tile (x, y) is centred on world (x, 0, -y); see SimPose. Nudging inward along the
            // normal keeps a wall face (which sits exactly on a tile boundary) on its own tile.
            half SampleVisibility(float3 positionWS, float3 normalWS)
            {
                if (_WickVisGrid.z < 0.5 || _IgnoreMask > 0.5)
                {
                    return 1;
                }

                float3 p = positionWS - normalWS * 0.01;
                int2 tile = int2(floor(p.x + 0.5), floor(-p.z + 0.5));
                if (tile.x < 0 || tile.y < 0 || tile.x >= (int)_WickVisGrid.x || tile.y >= (int)_WickVisGrid.y)
                {
                    return _WickVisDark;
                }

                half lit = LOAD_TEXTURE2D(_WickVisMask, tile).r;
                return lerp(_WickVisDark, (half)1, lit);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 light = SampleSH(normalWS);

                Light mainLight = GetMainLight();
                light += mainLight.color * mainLight.distanceAttenuation * saturate(dot(normalWS, mainLight.direction));

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, input.positionWS);
                    light += l.color * l.distanceAttenuation * saturate(dot(normalWS, l.direction));
                LIGHT_LOOP_END
                #endif

                half3 color = _BaseColor.rgb * light + _EmissionColor.rgb;
                color *= SampleVisibility(input.positionWS, normalWS);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
