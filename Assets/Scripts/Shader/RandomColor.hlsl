// #include <UnityShaderVariables.cginc>

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	StructuredBuffer<float4x4> instanceMatrix;
#endif
#include <UnityShaderVariables.cginc>

void ConfigureProcedural ()
{
	#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
		unity_ObjectToWorld = instanceMatrix[unity_InstanceID];
	#endif
}

float4 GetColor ()
{
	// float3 color = float3(
	// 	frac((unity_ObjectToWorld._11 + unity_ObjectToWorld._12 + unity_ObjectToWorld._13 + unity_ObjectToWorld._14) / 4.0f),
	// 	frac((unity_ObjectToWorld._21 + unity_ObjectToWorld._22 + unity_ObjectToWorld._23 + unity_ObjectToWorld._24) / 4.0f),
	// 	frac((unity_ObjectToWorld._31 + unity_ObjectToWorld._32 + unity_ObjectToWorld._33 + unity_ObjectToWorld._34) / 4.0f)
	// );
	// return float4(color, 1.0);
	return float4(1.0f, 0.0f, 0.0f, 1.0f);
}

void ShaderGraphFunction_float (float3 In, out float3 Out, out float4 InstanceColor)
{
	Out = In;
	InstanceColor = GetColor();
}

void ShaderGraphFunction_half (half3 In, out half3 Out, out half4 InstanceColor)
{
	Out = In;
	InstanceColor = GetColor();
}