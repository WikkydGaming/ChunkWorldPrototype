Shader "Custom/URP_TerrainArrayBlend2"
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
            float  _UVScale;

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0; // world-planar UVs
                float2 uv2        : TEXCOORD1; // x = idxA/255, y = idxB/255
                float4 color      : COLOR;     // a = blend weight (0..1)
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  idxA        : TEXCOORD3;
                float  idxB        : TEXCOORD4;
                float  wBlend      : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                float  fogFactor   : TEXCOORD7;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(ws);
                o.positionWS  = ws;
                o.normalWS    = TransformObjectToWorldNormal(v.normalOS);

                o.uv       = v.uv * _UVScale;
                o.idxA     = round(saturate(v.uv2.x) * 255.0);
                o.idxB     = round(saturate(v.uv2.y) * 255.0);
                o.wBlend   = saturate(v.color.a);

                o.shadowCoord = TransformWorldToShadowCoord(ws);
                o.fogFactor   = ComputeFogFactor(o.positionHCS.z);
                return o;
            }

            float3 SampleLayer(float2 uv, float layer)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_AlbedoArray, uv, layer).rgb;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);

                float3 colA = SampleLayer(i.uv, i.idxA);
                float3 colB = SampleLayer(i.uv, i.idxB);
                float3 albedo = lerp(colA, colB, i.wBlend) * _BaseColor.rgb;

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

                // small spec highlight
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
