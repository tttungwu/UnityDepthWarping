

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
StructuredBuffer<float4x4> instanceMatrices;
#endif

void ConfigureProcedural ()
{
	#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	unity_ObjectToWorld = instanceMatrices[unity_InstanceID];
	#endif
}


float4 GetFractalColor ()
{
	return float4(1.0f, 0.0f, 0.0f, 1.0f);
}

void ShaderGraphFunction_float (float3 In, out float3 Out, out float4 InstanceColor)
{
	Out = In;
	InstanceColor = GetFractalColor();
}

void ShaderGraphFunction_half (half3 In, out half3 Out, out half4 InstanceColor)
{
	Out = In;
	InstanceColor = GetFractalColor();
}