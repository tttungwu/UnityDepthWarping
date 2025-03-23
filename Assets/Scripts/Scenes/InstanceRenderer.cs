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
        if (!(instanceData && instanceData.mesh && instanceData.material && instanceData.matrices != null))
        {
            Debug.LogError("InstanceDataAsset is not properly configured!");
            return;
        }

        InitializeBuffers();
        InitializeBounds();
        InitializeMaterial();
    }

    void InitializeBuffers()
    {
        if (!(instanceData && instanceData.mesh && instanceData.material && instanceData.matrices != null))
        {
            Debug.LogError("InstanceDataAsset is not properly configured!");
            return;
        }
        
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

    void InitializeBounds()
    {
        if (!(instanceData && instanceData.mesh && instanceData.material && instanceData.matrices != null))
        {
            Debug.LogError("InstanceDataAsset is not properly configured!");
            return;
        }

        Bounds localBounds = instanceData.mesh.bounds;
        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;

        Vector3[] localCorners = new Vector3[8]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        renderBounds = new Bounds();
        foreach (Matrix4x4 matrix in instanceData.matrices)
        {
            foreach (Vector3 localCorner in localCorners)
            {
                Vector3 worldCorner = matrix.MultiplyPoint3x4(localCorner);
                renderBounds.Encapsulate(worldCorner);
            }
        }
    }

    void InitializeMaterial()
    {
        instanceMatrixBufferId = Shader.PropertyToID("instanceMatrix");
        instanceData.material.SetBuffer(instanceMatrixBufferId, matrixBuffer);
    }
    
    void Update()
    {
        if (!(instanceData && instanceData.mesh && instanceData.material && instanceData.matrices != null)) return;
        
        Debug.Log(instanceData.mesh);
        Debug.Log(instanceData.material);
        Debug.Log(instanceData.matrices);
        Debug.Log(renderBounds);
        Graphics.DrawMeshInstancedIndirect(
            instanceData.mesh,
            0,
            instanceData.material,
            renderBounds,
            argsBuffer
        );
    }

    void OnDestroy()
    {
        argsBuffer?.Release();
        argsBuffer = null;
        
        matrixBuffer?.Release();
        matrixBuffer = null;
        
        cullResultBuffer?.Release();
        cullResultBuffer = null;
    }
}