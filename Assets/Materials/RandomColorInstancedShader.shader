Shader "Unlit/RandomColorInstancedShader"
{
   Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "LightMode"="UniversalForward" }
        Pass
        {
            Name "ForwardUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float4x4> instanceMatrix;
            #endif

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;
                
                float4x4 instanceTransform;
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    instanceTransform = instanceMatrix[instanceID];
                #else
                    instanceTransform = UNITY_MATRIX_M;
                #endif

                float4 worldPos = mul(instanceTransform, IN.vertex);
                OUT.pos = TransformWorldToHClip(worldPos.xyz);
                OUT.uv = IN.uv;

                float3 color;
                #ifdef UNITY_INSTANCING_ENABLED
                    color = float3(
                        frac((instanceTransform._11 + instanceTransform._12 + instanceTransform._13 + instanceTransform._14) / 4.0f),
                        frac((instanceTransform._21 + instanceTransform._22 + instanceTransform._23 + instanceTransform._24) / 4.0f),
                        frac((instanceTransform._31 + instanceTransform._32 + instanceTransform._33 + instanceTransform._34) / 4.0f)
                    );
                #else
                    color = _BaseColor.rgb;
                #endif

                OUT.color = float4(color, 1.0f);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}