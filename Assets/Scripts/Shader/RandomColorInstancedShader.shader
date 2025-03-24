Shader "Random/RandomColorInstancedShader"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "LightMode"="UniversalForward" }
        Pass
        {
            Name "ForwardUnlit"
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float4x4> instanceMatrix;
            
                void setup()
                {
                    unity_ObjectToWorld = instanceMatrix[unity_InstanceID];
                }
            #endif

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                
                float4 worldPos = mul(unity_ObjectToWorld, IN.vertex);
                OUT.pos = mul(UNITY_MATRIX_VP, worldPos);
                OUT.uv = IN.uv;

                float3 color = float3(
                    frac((unity_ObjectToWorld._11 + unity_ObjectToWorld._12 + unity_ObjectToWorld._13 + unity_ObjectToWorld._14) / 4.0f),
                    frac((unity_ObjectToWorld._21 + unity_ObjectToWorld._22 + unity_ObjectToWorld._23 + unity_ObjectToWorld._24) / 4.0f),
                    frac((unity_ObjectToWorld._31 + unity_ObjectToWorld._32 + unity_ObjectToWorld._33 + unity_ObjectToWorld._34) / 4.0f)
                );
                OUT.color = float4(color, 1.0f);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float4x4> instanceMatrix;
                
                void setup()
                {
                    unity_ObjectToWorld = instanceMatrix[unity_InstanceID];
                }
            #endif

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 worldPos = mul(unity_ObjectToWorld, IN.vertex);
                OUT.pos = mul(UNITY_MATRIX_VP, worldPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}