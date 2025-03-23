using UnityEngine;

namespace Core.IndirectDraw
{
    public class InstanceRenderer : MonoBehaviour
    {
        [SerializeField] private Camera camera;
        [SerializeField] private InstanceDataAsset instanceData;
        [SerializeField] private CullingMethod[] cullingMethods;
        
        private ComputeBuffer _argsBuffer;
        private ComputeBuffer _matrixBuffer;
        private ComputeBuffer _cullResultBuffer;
        private uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };

        private Bounds _renderBounds;

        private int _instanceCount;
        private int _instanceMatrixBufferId;

        void Start()
        {
            if (!(instanceData && instanceData.mesh && instanceData.material && instanceData.matrices != null))
            {
                Debug.LogError("InstanceDataAsset is not properly configured!");
                return;
            }
            
            InitializeBuffers();
            InitializeBounds();
            InitializeMaterials();
            InitializeCullingMethods();
        }

        void InitializeBuffers()
        {
            if (!(instanceData && instanceData.mesh && instanceData.material && instanceData.matrices != null))
            {
                Debug.LogError("InstanceDataAsset is not properly configured!");
                return;
            }
            
            _instanceCount = instanceData.matrices.Length;
            _matrixBuffer = new ComputeBuffer(_instanceCount, sizeof(float) * 16);
            _matrixBuffer.SetData(instanceData.matrices);
            _cullResultBuffer = new ComputeBuffer(_instanceCount, sizeof(float) * 16, ComputeBufferType.Append);

            _args[0] = instanceData.mesh.GetIndexCount(0);
            _args[1] = (uint)_instanceCount;
            _args[2] = instanceData.mesh.GetIndexStart(0);
            _args[3] = instanceData.mesh.GetBaseVertex(0);
            _args[4] = 0;
            _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(_args);
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

            _renderBounds = new Bounds();
            foreach (Matrix4x4 matrix in instanceData.matrices)
            {
                foreach (Vector3 localCorner in localCorners)
                {
                    Vector3 worldCorner = matrix.MultiplyPoint3x4(localCorner);
                    _renderBounds.Encapsulate(worldCorner);
                }
            }
        }

        void InitializeMaterials()
        {
            _instanceMatrixBufferId = Shader.PropertyToID("instanceMatrix");
            instanceData.material.SetBuffer(_instanceMatrixBufferId, _matrixBuffer);
        }

        void InitializeCullingMethods()
        {
            foreach (CullingMethod cullingMethod in cullingMethods)
            {
                cullingMethod.Init(camera, instanceData.mesh, instanceData.matrices);
            }
        }
        
        void Update()
        {
            if (!(instanceData && instanceData.mesh && instanceData.material && instanceData.matrices != null)) return;
            
            _cullResultBuffer.SetCounterValue(0);
            OcclussionCulling();
            // instanceData.material.SetBuffer(_instanceMatrixBufferId, _cullResultBuffer);
            // ComputeBuffer.CopyCount(_cullResultBuffer, _argsBuffer, sizeof(uint));
            Graphics.DrawMeshInstancedIndirect(
                instanceData.mesh,
                0,
                instanceData.material,
                _renderBounds,
                _argsBuffer
            );
        }

        void OcclussionCulling()
        {
            Matrix4x4[] cullResultMatrix = null;
            foreach (var cullingMethod in cullingMethods)
            {
                cullingMethod.Cull(cullResultMatrix);
                cullResultMatrix = cullingMethod.GetVisibleMatrices();
            }
            //
            // // test frustum culling
            // _args[1] = (uint)cullResultMatrix.Length;
            // _argsBuffer.SetData(_args);
            // _matrixBuffer.SetData(cullResultMatrix);
            // Debug.Log(_args[1]);
        }

        void OnDestroy()
        {
            _argsBuffer?.Release();
            _argsBuffer = null;
            
            _matrixBuffer?.Release();
            _matrixBuffer = null;
            
            _cullResultBuffer?.Release();
            _cullResultBuffer = null;
        }
    }
}