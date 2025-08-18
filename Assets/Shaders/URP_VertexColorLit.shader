Shader "Custom/URP_VertexColorLit"
{
    Properties
    {
        _BaseMap ("Base Map (optional)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Tint ("Vertex Color Tint", Color) = (1,1,1,1)
        _Gloss ("Spec-ish Boost (simple)", Range(0,1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        // ---------- Forward Lit ----------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP variants
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseColor;
            float4 _Tint;
            float  _Gloss;

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;    // vertex color
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 color       : COLOR;
                float4 shadowCoord : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(ws);
                o.positionWS  = ws;
                o.normalWS    = TransformObjectToWorldNormal(v.normalOS);
                o.uv          = v.uv;
                o.color       = v.color * _Tint;
                o.shadowCoord = TransformWorldToShadowCoord(ws);
                o.fogFactor   = ComputeFogFactor(o.positionHCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);

                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                float3 albedo = (tex.rgb * _BaseColor.rgb) * i.color.rgb;

                // Ambient (SH)
                float3 ambient = SampleSH(n);

                // Main light
                Light mainLight = GetMainLight(i.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float  ndl = saturate(dot(n, L));
                float3 direct = ndl * mainLight.color * mainLight.shadowAttenuation;

                // Additional lights
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    float ndl2 = saturate(dot(n, normalize(l.direction)));
                    direct += ndl2 * l.color * l.distanceAttenuation * l.shadowAttenuation;
                }
                #endif

                // Tiny spec-ish boost
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

        // ---------- Shadow Caster (with compatibility shim) ----------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // Core bits needed by Shadows
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // --- Compatibility shim: define LerpWhiteTo if missing ---
            // Some URP versions expect this to be present (normally from Color.hlsl).
            #ifndef LerpWhiteTo
            inline float3 LerpWhiteTo(float3 color, float t) { return lerp(float3(1.0,1.0,1.0), color, t); }
            #endif
            // --------------------------------------------------------

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings   { float4 positionHCS:SV_POSITION; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 n  = TransformObjectToWorldNormal(v.normalOS);
                o.positionHCS = GetShadowPositionHClip(ws, n);
                return o;
            }

            float4 frag (Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
