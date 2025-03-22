Shader "Unlit/InstanceRandomColorMatrixURP"
{
    Properties
    {
        // 添加一个默认颜色属性，以便非实例化时使用
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
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

            // 定义默认颜色
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            // Unity 内置的实例化矩阵（仅在 DrawMeshInstanced 时有效）
            UNITY_INSTANCING_BUFFER_START(Props)
                // 可选：如果需要通过 MaterialPropertyBlock 传递额外数据，可以在这里定义
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;
                
                float4x4 instanceMatrix;
                UNITY_SETUP_INSTANCE_ID(IN);
                instanceMatrix = UNITY_MATRIX_M;

                // 计算世界空间位置
                float4 worldPos = mul(instanceMatrix, IN.vertex);
                OUT.pos = TransformWorldToHClip(worldPos.xyz);
                OUT.uv = IN.uv;

                // 计算颜色
                float3 color;
                #ifdef UNITY_INSTANCING_ENABLED
                    color = float3(
                        frac((instanceMatrix._11 + instanceMatrix._12 + instanceMatrix._13 + instanceMatrix._14) / 4.0f),
                        frac((instanceMatrix._21 + instanceMatrix._22 + instanceMatrix._23 + instanceMatrix._24) / 4.0f),
                        frac((instanceMatrix._31 + instanceMatrix._32 + instanceMatrix._33 + instanceMatrix._34) / 4.0f)
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