using UnityEngine;

public class InstanceRenderer : MonoBehaviour
{
    [SerializeField] private InstanceDataAsset instanceData;

    private ComputeBuffer argsBuffer;
    private ComputeBuffer matrixBuffer;
    private ComputeBuffer cullResultBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    private Bounds renderBounds;

    private int instanceCount;
    private int instanceMatrixBufferId;

    void Start()
    {
        if (instanceData == null || instanceData.matrices == null ||
            instanceData.mesh == null || instanceData.material == null)
        {
            Debug.LogError("InstanceDataAsset is not properly configured!");
            return;
        }

        InitializeBuffers();
        InitializeMaterial();
    }

    void InitializeBuffers()
    {
        instanceCount = instanceData.matrices.Length;
        matrixBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16);
        matrixBuffer.SetData(instanceData.matrices);
        cullResultBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16, ComputeBufferType.Append);

        args[0] = instanceData.mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = instanceData.mesh.GetIndexStart(0);
        args[3] = instanceData.mesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    void InitializeMaterial()
    {
        instanceMatrixBufferId = Shader.PropertyToID("instanceMatrix");
        instanceData.material.SetBuffer(instanceMatrixBufferId, matrixBuffer);
    }
    
    void Update()
    {
        if (instanceData != null && instanceData.mesh != null && instanceData.material != null)
        {
            Graphics.DrawMeshInstancedIndirect(
                instanceData.mesh,
                0,
                instanceData.material,
                renderBounds,
                argsBuffer
            );
        }
    }

    void OnDisable()
    {
        argsBuffer?.Release();
        argsBuffer = null;
        
        matrixBuffer?.Release();
        matrixBuffer = null;
        
        cullResultBuffer?.Release();
        cullResultBuffer = null;
    }
}