Shader "Custom/URP_SimpleTex2DArray_Unlit_UV2Slice"
{
    Properties
    {
        _MainTex ("Array (Texture2DArray)", 2DArray) = "" {}
        _Slice   ("Fallback Slice", Float) = 0     // used if UV2 is not provided
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Slice;   // fallback/global slice
            CBUFFER_END

            TEXTURE2D_ARRAY(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;  // base UVs for sampling
                float4 uv2        : TEXCOORD1;  // we'll put slice in uv2.z (or uv2.x)
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float  slice       : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                // Prefer UV2.z if present, else fallback to UV2.x, else fallback property
                float s = IN.uv2.z;
                // If you only authored UV2 as Vector2, s will default to 0, so fall back:
                if (s == 0) s = IN.uv2.x;
                // If still zero and you need a distinct layer, fallback to material _Slice:
                if (s == 0) s = _Slice;

                // If you want integer slices, round to nearest:
                OUT.slice = round(s);

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, IN.uv, IN.slice);
                return c * _Tint;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
