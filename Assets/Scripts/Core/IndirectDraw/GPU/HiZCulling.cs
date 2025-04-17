using System.Reflection;
using Features;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Core.IndirectDraw.GPU
{
    public class HiZCulling : CullingMethod
    {
        [SerializeField] private ComputeShader hizCullingShader;
        [SerializeField] private int skipFrameCount = 1;
        [SerializeField] private bool printCullingInfo;
        
        private Camera _camera;
        private Mesh _mesh;
        private Matrix4x4[] _objectsToWorldMatrix;
        private int _objectNum;
        private int _mipmapWidth, _mipmapLevel;
        private DepthSaveFeature _depthSaveFeature;
        private RenderTexture _prevDepthTexture;
        private RenderTexture _depthTexture;
        private ComputeBuffer _matrixBuffer;
        private int _convertDepthToNDCKernel;
        private int _maxMipmapKernel;
        private int _computeVisibilityKernel;
        
        private static readonly int DepthTextureShaderPropertyID = Shader.PropertyToID("depth_texture");
        private static readonly int FarClipPlaneShaderPropertyID = Shader.PropertyToID("far_clip_plane");
        private static readonly int NearClipPlaneShaderPropertyID = Shader.PropertyToID("near_clip_plane");
        private static readonly int ProjectionMatrixShaderPropertyID = Shader.PropertyToID("projection_matrix");
        private static readonly int PrevMipmapBufferShaderPropertyID = Shader.PropertyToID("prev_mipmap_buffer");
        private static readonly int CurMipmapBufferShaderPropertyID = Shader.PropertyToID("cur_mipmap_buffer");
        private static readonly int MipmapWidthShaderPropertyID = Shader.PropertyToID("mipmap_width");
        private static readonly int MipmapHeightShaderPropertyID = Shader.PropertyToID("mipmap_height");
        private static readonly int ObjectNumShaderPropertyID = Shader.PropertyToID("object_num");
        private static readonly int ScreenWidthShaderPropertyID = Shader.PropertyToID("screen_width");
        private static readonly int ScreenHeightShaderPropertyID = Shader.PropertyToID("screen_height");
        private static readonly int BoundsMinShaderPropertyID = Shader.PropertyToID("bounds_min");
        private static readonly int BoundsMaxShaderPropertyID = Shader.PropertyToID("bounds_max");
        private static readonly int CurrentProjectionViewMatrixShaderPropertyID = Shader.PropertyToID("current_projection_view_matrix");
        private static readonly int MipmapBufferShaderPropertyID = Shader.PropertyToID("mipmap_buffer");
        private static readonly int ModelMatrixBufferShaderPropertyID = Shader.PropertyToID("model_matrix_buffer");
        private static readonly int CullResultBufferShaderPropertyID = Shader.PropertyToID("cull_result_buffer");
        
        
        public override void Init(Camera cam, Mesh mesh, Matrix4x4[] matrices)
        {
            _camera = cam;
            _mesh = mesh;
            _objectsToWorldMatrix = matrices;
            _objectNum = matrices.Length;
            
            _mipmapWidth = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
            _mipmapLevel = Mathf.FloorToInt(Mathf.Log(_mipmapWidth) / Mathf.Log(2));
            _depthTexture = new RenderTexture(_mipmapWidth, _mipmapWidth, 0, RenderTextureFormat.RFloat);
            _depthTexture.enableRandomWrite = true;
            _depthTexture.useMipMap = true;
            _depthTexture.autoGenerateMips = false;
            _depthTexture.Create();
            _matrixBuffer = new ComputeBuffer(matrices.Length, sizeof(float) * 16);
            _matrixBuffer.SetData(matrices);
            
            _convertDepthToNDCKernel = hizCullingShader.FindKernel("convert_depth_to_ndc");
            _maxMipmapKernel = hizCullingShader.FindKernel("generate_max_mipmap");
            _computeVisibilityKernel = hizCullingShader.FindKernel("compute_visibility");
            
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
        }
        
        public override void Cull(ComputeBuffer cullResultBuffer, Matrix4x4[] matrices = null)
        {
            if (skipFrameCount > 0) --skipFrameCount;
            else
            {
                if (matrices is { Length: 0 }) return;
                _prevDepthTexture = _depthSaveFeature.GetDepthTexture();
                
                // convert depth to NDC
                hizCullingShader.SetTexture(_convertDepthToNDCKernel, DepthTextureShaderPropertyID, _prevDepthTexture);
                hizCullingShader.SetTexture(_convertDepthToNDCKernel, CurMipmapBufferShaderPropertyID, _depthTexture);
                hizCullingShader.SetMatrix(ProjectionMatrixShaderPropertyID, _camera.projectionMatrix);
                hizCullingShader.SetFloat(FarClipPlaneShaderPropertyID, _camera.farClipPlane);
                hizCullingShader.SetFloat(NearClipPlaneShaderPropertyID, _camera.nearClipPlane);
                hizCullingShader.Dispatch(_convertDepthToNDCKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                
                // generate max mipmap
                for (int layer = 0, curWidth = Screen.width, curHeight = Screen.height; layer < _mipmapLevel; ++layer)
                {
                    int nextWidth = (1 + curWidth) / 2;
                    int nextHeight = (1 + curHeight) / 2;
                    hizCullingShader.SetTexture(_maxMipmapKernel, PrevMipmapBufferShaderPropertyID, _depthTexture, layer);
                    hizCullingShader.SetTexture(_maxMipmapKernel, CurMipmapBufferShaderPropertyID, _depthTexture, layer + 1);
                    hizCullingShader.SetInt(MipmapWidthShaderPropertyID, nextWidth);
                    hizCullingShader.SetInt(MipmapHeightShaderPropertyID, nextHeight);
                    hizCullingShader.Dispatch(_maxMipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                    curWidth = nextWidth;
                    curHeight = nextHeight;
                }
                
                // cull object
                hizCullingShader.SetBuffer(_computeVisibilityKernel, CullResultBufferShaderPropertyID, cullResultBuffer);
                hizCullingShader.SetBuffer(_computeVisibilityKernel, ModelMatrixBufferShaderPropertyID, _matrixBuffer);
                hizCullingShader.SetTexture(_computeVisibilityKernel, MipmapBufferShaderPropertyID, _depthTexture);
                hizCullingShader.SetMatrix(CurrentProjectionViewMatrixShaderPropertyID, _camera.projectionMatrix * _camera.worldToCameraMatrix);
                hizCullingShader.SetInt(ObjectNumShaderPropertyID, _objectNum);
                hizCullingShader.SetVector(BoundsMaxShaderPropertyID, _mesh.bounds.max);
                hizCullingShader.SetVector(BoundsMinShaderPropertyID, _mesh.bounds.min);
                hizCullingShader.SetInt(ScreenWidthShaderPropertyID, Screen.width);
                hizCullingShader.SetInt(ScreenHeightShaderPropertyID, Screen.height);
                hizCullingShader.Dispatch(_computeVisibilityKernel, (_objectNum + 63) / 64, 1, 1);
                
                if (printCullingInfo)
                {
                    var countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
                    ComputeBuffer.CopyCount(cullResultBuffer, countBuffer, 0);
                    var countData = new uint[1];
                    countBuffer.GetData(countData);
                    countBuffer.Release();
                    var actualCount = countData[0];
                    Debug.Log($"Hiz Culled from {_objectNum} to {actualCount} : {_objectNum - actualCount}");
                }
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
    }
}