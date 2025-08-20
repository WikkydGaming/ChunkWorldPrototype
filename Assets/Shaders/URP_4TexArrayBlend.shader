Shader "Custom/URP_4TexArrayBlend"
{
    Properties
    {
        _TileScale ("Tile Scale", Float) = 1.0
        _TerrainArray ("Terrain Array", 2DArray) = "" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            ZWrite On Cull Back ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP base include
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---- Compatibility shim: define SAMPLE_TEXTURE2D_ARRAY if missing ----
            #ifndef SAMPLE_TEXTURE2D_ARRAY
                // TEXTURE2D_ARRAY declares a Texture2DArray, which supports .Sample()
                #define SAMPLE_TEXTURE2D_ARRAY(tex, samp, uv, slice) tex.Sample(samp, float3(uv, slice))
            #endif
            // ---------------------------------------------------------------------

            struct appdata
            {
                float3 positionOS : POSITION;
                float2 uv0        : TEXCOORD0; // base UV (world/grid)
                float4 uv2        : TEXCOORD1; // weights (w0..w3)
                float4 uv3        : TEXCOORD2; // indices (i0..i3) as floats
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 weights    : TEXCOORD1;
                float4 indices    : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float _TileScale;
            CBUFFER_END

            TEXTURE2D_ARRAY(_TerrainArray);
            SAMPLER(sampler_TerrainArray);

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(float4(v.positionOS, 1));
                o.uv         = v.uv0 * _TileScale;
                o.weights    = v.uv2;
                o.indices    = v.uv3;
                return o;
            }

            inline float4 SampleLayer(float2 uv, float slice)
            {
                // slice is float but represents an int layer
                return SAMPLE_TEXTURE2D_ARRAY(_TerrainArray, sampler_TerrainArray, uv, slice);
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 w = saturate(i.weights);
                w /= max(w.x + w.y + w.z + w.w, 1e-5);

                float s0 = round(i.indices.x);
                float s1 = round(i.indices.y);
                float s2 = round(i.indices.z);
                float s3 = round(i.indices.w);

                float4 c0 = SampleLayer(i.uv, s0);
                float4 c1 = SampleLayer(i.uv, s1);
                float4 c2 = SampleLayer(i.uv, s2);
                float4 c3 = SampleLayer(i.uv, s3);

                return c0*w.x + c1*w.y + c2*w.z + c3*w.w;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
