using UnityEngine;

namespace Core.IndirectDraw.GPU
{
    public class FrustumCulling : CullingMethod
    {
        [SerializeField] private ComputeShader frustumCullingShader;
        [SerializeField] private bool printCullingInfo = false;

        private Camera _camera;
        private Mesh _mesh;
        private Matrix4x4[] _objectsToWorldMatrix;
        private int _objectNum;
        private int _computeVisibilityKernel;
        private ComputeBuffer _matrixBuffer;
        
        private static readonly int ObjectNumShaderPropertyID = Shader.PropertyToID("object_num");
        private static readonly int BoundsMinShaderPropertyID = Shader.PropertyToID("bounds_min");
        private static readonly int BoundsMaxShaderPropertyID = Shader.PropertyToID("bounds_max");
        private static readonly int CurrentProjectionViewMatrixShaderPropertyID = Shader.PropertyToID("current_projection_view_matrix");
        private static readonly int ModelMatrixBufferShaderPropertyID = Shader.PropertyToID("model_matrix_buffer");
        private static readonly int CullResultBufferShaderPropertyID = Shader.PropertyToID("cull_result_buffer");
        
        public override void Init(Camera cam, Mesh mesh, Matrix4x4[] matrices)
        {
            _camera = cam;
            _mesh = mesh;
            _objectsToWorldMatrix = matrices;
            _objectNum = matrices.Length;
            _computeVisibilityKernel = frustumCullingShader.FindKernel("compute_visibility");
            _matrixBuffer = new ComputeBuffer(matrices.Length, sizeof(float) * 16);
            _matrixBuffer.SetData(matrices);
        }
        
        public override void Cull(ComputeBuffer cullResultBuffer, Matrix4x4[] matrices = null)
        {
            var curViewMatrix = _camera.worldToCameraMatrix;
            var curProjectionMatrix = _camera.projectionMatrix;
            frustumCullingShader.SetBuffer(_computeVisibilityKernel, CullResultBufferShaderPropertyID, cullResultBuffer);
            frustumCullingShader.SetBuffer(_computeVisibilityKernel, ModelMatrixBufferShaderPropertyID, _matrixBuffer);
            frustumCullingShader.SetInt(ObjectNumShaderPropertyID, _objectNum);
            frustumCullingShader.SetVector(BoundsMinShaderPropertyID, _mesh.bounds.min);
            frustumCullingShader.SetVector(BoundsMaxShaderPropertyID, _mesh.bounds.max);
            frustumCullingShader.SetMatrix(CurrentProjectionViewMatrixShaderPropertyID, curProjectionMatrix * curViewMatrix);
            frustumCullingShader.Dispatch(_computeVisibilityKernel, (_objectNum + 63) / 64, 1, 1);
            
            if (printCullingInfo)
            {
                ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
                ComputeBuffer.CopyCount(cullResultBuffer, countBuffer, 0);
                uint[] countData = new uint[1];
                countBuffer.GetData(countData);
                countBuffer.Release();
                uint actualCount = countData[0];
                Debug.Log($"VFC Culled from {_objectNum} to {actualCount} : {_objectNum - actualCount}");
            }
        }
        
        public override Matrix4x4[] GetVisibleMatrices()
        {
            return null;
        }

        public override Matrix4x4[] GetInvisibleMatrices()
        {
            return null;
        }
        
        void OnDestroy()
        {
            _matrixBuffer?.Release();
            _matrixBuffer = null;
        }
    }
}