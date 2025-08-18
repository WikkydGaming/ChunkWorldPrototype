Shader "Custom/URP_VertexColorUnlit"
{
    Properties { _Tint("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;      // <- vertex color
            };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
            };

            float4 _Tint;

            Varyings vert (Attributes v) {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _Tint;
                return o;
            }

            half4 frag (Varyings i) : SV_Target {
                return i.color;
            }
            ENDHLSL
        }
    }
}
