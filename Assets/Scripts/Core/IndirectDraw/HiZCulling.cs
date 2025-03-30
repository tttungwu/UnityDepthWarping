using System.Reflection;
using Features;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Core.IndirectDraw
{
    public class HiZCulling : CullingMethod
    {
        [SerializeField] private int skipFrameCount = 5;
        [SerializeField] private bool printCullingInfo = false;
        
        private Camera _camera;
        private float _nearClipPlane, _farClipPlane;
        private Mesh _mesh;
        
        private DepthSaveFeature _depthSaveFeature;
        private RenderTexture _prevDepthTexture;
        private RenderTexture _depthTexture;
        private ComputeBuffer _matrixBuffer;

        private int _MipmapWidth, _MipmapLevel;
        private int _objectNum, _allNum;
        
        public ComputeShader HiZComputeShader;
        private int _convertDepthToNDCKernel;
        private int _maxMipmapKernel;
        private int _computeVisibilityKernel;
        
        private static readonly int FarClipPlaneShaderPropertyID = Shader.PropertyToID("FarClipPlane");
        private static readonly int NearClipPlaneShaderPropertyID = Shader.PropertyToID("NearClipPlane");
        private static readonly int ProjectionMatrixShaderPropertyID = Shader.PropertyToID("ProjectionMatrix");
        private static readonly int PrevMipmapBufferPropertyID = Shader.PropertyToID("PrevMipmapBuffer");
        private static readonly int CurMipmapBufferPropertyID = Shader.PropertyToID("CurMipmapBuffer");
        private static readonly int MipmapWidthShaderPropertyID = Shader.PropertyToID("MipmapWidth");
        private static readonly int MipmapHeightShaderPropertyID = Shader.PropertyToID("MipmapHeight");
        private static readonly int ObjectNumShaderPropertyID = Shader.PropertyToID("ObjectNum");
        private static readonly int WidthShaderPropertyID = Shader.PropertyToID("Width");
        private static readonly int HeightShaderPropertyID = Shader.PropertyToID("Height");
        private static readonly int BoundsMinShaderPropertyID = Shader.PropertyToID("BoundsMin");
        private static readonly int BoundsMaxShaderPropertyID = Shader.PropertyToID("BoundsMax");
        private static readonly int CurrentProjectionViewMatrixShaderPropertyID = Shader.PropertyToID("CurrentProjectionViewMatrix");
        private static readonly int MipmapBufferShaderPropertyID = Shader.PropertyToID("MipmapBuffer");
        private static readonly int ModelMatrixBufferShaderPropertyID = Shader.PropertyToID("ModelMatrixBuffer");
        private static readonly int CullResultBufferShaderPropertyID = Shader.PropertyToID("CullResultBuffer");
        
        public override void Init(Camera cam, Mesh mesh, Matrix4x4[] matrices)
        {
            _mesh = mesh;
            _camera = cam;
            _camera.depthTextureMode = DepthTextureMode.Depth;
            _nearClipPlane = cam.nearClipPlane;
            _farClipPlane = cam.farClipPlane;
            _allNum = matrices.Length;
            var proInfo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (proInfo != null)
            {
                var rendererDataList = (ScriptableRendererData[])proInfo.GetValue(UniversalRenderPipeline.asset);
                foreach (var rendererData in rendererDataList)
                {
                    foreach (var feature in rendererData.rendererFeatures)
                    {
                        if (feature is DepthSaveFeature)
                        {
                            _depthSaveFeature = (DepthSaveFeature)feature;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("Depth save feature not found");
            }
            
            _MipmapWidth = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
            _MipmapLevel = Mathf.FloorToInt(Mathf.Log(_MipmapWidth) / Mathf.Log(2));
            _depthTexture = new RenderTexture(_MipmapWidth, _MipmapWidth, 0, RenderTextureFormat.RFloat);
            _depthTexture.enableRandomWrite = true;
            _depthTexture.useMipMap = true;
            _depthTexture.autoGenerateMips = false;
            _depthTexture.Create();
            
            _convertDepthToNDCKernel = HiZComputeShader.FindKernel("ConvertDepthToNDC");
            _maxMipmapKernel = HiZComputeShader.FindKernel("GenerateMaxMipmap");;
            _computeVisibilityKernel = HiZComputeShader.FindKernel("ComputeVisibility");
            
            _matrixBuffer = new ComputeBuffer(matrices.Length, sizeof(float) * 16);
        }
        
        public override void Cull(Matrix4x4[] matrices = null, ComputeBuffer cullResultBuffer = null)
        {
            if (skipFrameCount > 0) --skipFrameCount;
            else
            {
                if (matrices.Length == 0) return;
                _prevDepthTexture = _depthSaveFeature.GetDepthTexture();
                Debug.Log(_prevDepthTexture);
                
                // convert depth to NDC
                HiZComputeShader.SetTexture(_convertDepthToNDCKernel, PrevMipmapBufferPropertyID, _prevDepthTexture);
                HiZComputeShader.SetTexture(_convertDepthToNDCKernel, CurMipmapBufferPropertyID, _depthTexture);
                HiZComputeShader.SetMatrix(ProjectionMatrixShaderPropertyID, _camera.projectionMatrix);
                HiZComputeShader.SetFloat(FarClipPlaneShaderPropertyID, _farClipPlane);
                HiZComputeShader.SetFloat(NearClipPlaneShaderPropertyID, _nearClipPlane);
                HiZComputeShader.Dispatch(_convertDepthToNDCKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                
                // generate max mipmap
                for (int layer = 0, curWidth = Screen.width, curHeight = Screen.height; layer < _MipmapLevel; ++layer)
                {
                    int nextWidth = (1 + curWidth) / 2;
                    int nextHeight = (1 + curHeight) / 2;
                    HiZComputeShader.SetTexture(_maxMipmapKernel, PrevMipmapBufferPropertyID, _depthTexture, layer);
                    HiZComputeShader.SetTexture(_maxMipmapKernel, CurMipmapBufferPropertyID, _depthTexture, layer + 1);
                    HiZComputeShader.SetInt(MipmapWidthShaderPropertyID, nextWidth);
                    HiZComputeShader.SetInt(MipmapHeightShaderPropertyID, nextHeight);
                    HiZComputeShader.Dispatch(_maxMipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                    curWidth = nextWidth;
                    curHeight = nextHeight;
                }
                // set data
                _objectNum = matrices.Length;
                _matrixBuffer.SetData(matrices);
                // cull object
                HiZComputeShader.SetBuffer(_computeVisibilityKernel, CullResultBufferShaderPropertyID, cullResultBuffer);
                HiZComputeShader.SetBuffer(_computeVisibilityKernel, ModelMatrixBufferShaderPropertyID, _matrixBuffer);
                HiZComputeShader.SetTexture(_computeVisibilityKernel, MipmapBufferShaderPropertyID, _depthTexture);
                HiZComputeShader.SetMatrix(CurrentProjectionViewMatrixShaderPropertyID, _camera.projectionMatrix * _camera.worldToCameraMatrix);
                HiZComputeShader.SetInt(ObjectNumShaderPropertyID, _objectNum);
                HiZComputeShader.SetVector(BoundsMaxShaderPropertyID, _mesh.bounds.max);
                HiZComputeShader.SetVector(BoundsMinShaderPropertyID, _mesh.bounds.min);
                HiZComputeShader.SetInt(WidthShaderPropertyID, Screen.width);
                HiZComputeShader.SetInt(HeightShaderPropertyID, Screen.height);
                HiZComputeShader.Dispatch(_computeVisibilityKernel, (_objectNum + 63) / 64, 1, 1);
                
                if (printCullingInfo)
                {
                    ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
                    ComputeBuffer.CopyCount(cullResultBuffer, countBuffer, 0);
                    uint[] countData = new uint[1];
                    countBuffer.GetData(countData);
                    countBuffer.Release();
                    uint actualCount = countData[0];
                    Debug.Log($"Culled by VFC: {_allNum - _objectNum}");
                    Debug.Log($"Culled by WOC: {_objectNum - actualCount}");
                }
            }
        }
        
        void OnDestroy()
        {
            _matrixBuffer?.Release();
            _matrixBuffer = null;
        }
    }
}