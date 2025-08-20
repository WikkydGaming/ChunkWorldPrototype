Shader "Custom/URP_TEST2DArray"
{
    Properties
    {
        _MainTex ("Array (Texture2DArray)", 2DArray) = "" {}
        _Slice   ("Slice Index", Float) = 0
        _Tint    ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // (Core.hlsl already pulls in the sampling helpers; no extra includes needed)

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Slice;
            CBUFFER_END

            // Texture2DArray + sampler
            TEXTURE2D_ARRAY(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Correct macro usage: uv and slice are separate args
                half4 c = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, IN.uv, _Slice);
                return c * _Tint;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
