Shader "Custom/URP_TerrainArray"
{
    Properties
    {
        _AlbedoArray ("Albedo Array", 2DArray) = "" {}
        _BaseColor   ("Tint", Color) = (1,1,1,1)
        _Gloss       ("Spec-ish Boost", Range(0,1)) = 0.03
        _UVScale     ("UV Scale (tiling)", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D_ARRAY(_AlbedoArray); SAMPLER(sampler_AlbedoArray);
            float4 _BaseColor;
            float  _Gloss;
            float  _UVScale; // multiply incoming UVs

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0; // world-planar UV from CPU
                float2 uv2        : TEXCOORD1; // x = layerIndex/255
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  layerNorm   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(ws);
                o.positionWS  = ws;
                o.normalWS    = TransformObjectToWorldNormal(v.normalOS);

                o.uv          = v.uv * _UVScale;
                o.layerNorm   = v.uv2.x; // 0..1, encode index/255 from CPU

                o.shadowCoord = TransformWorldToShadowCoord(ws);
                o.fogFactor   = ComputeFogFactor(o.positionHCS.z);
                return o;
            }

            float3 SampleLayer(float2 uv, float layerF)
            {
                // decode integer index (0..255) from normalized
                float layer = round(saturate(layerF) * 255.0);
                return SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_AlbedoArray, uv, layer).rgb;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);

                float3 albedo = SampleLayer(i.uv, i.layerNorm) * _BaseColor.rgb;

                float3 ambient = SampleSH(n);

                Light mainLight = GetMainLight(i.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float  ndl = saturate(dot(n, L));
                float3 direct = ndl * mainLight.color * mainLight.shadowAttenuation;

            #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    float ndl2 = saturate(dot(n, normalize(l.direction)));
                    direct += ndl2 * l.color * l.distanceAttenuation * l.shadowAttenuation;
                }
            #endif

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 H = normalize(L + V);
                float  ndh = saturate(dot(n, H));
                float  spec = pow(ndh, 32.0) * _Gloss;

                float3 lit = albedo * (ambient + direct) + spec;
                lit = MixFog(lit, i.fogFactor);
                return float4(lit, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
