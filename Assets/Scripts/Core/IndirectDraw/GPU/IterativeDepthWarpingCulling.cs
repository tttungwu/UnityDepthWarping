// #define DEBUGPRINT

using System;
using System.IO;
using System.Reflection;
using Features;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Core.IndirectDraw.GPU
{
    public class IterativeDepthWarpingCulling : CullingMethod
    {
        [SerializeField] private ComputeShader idwCullingShader;
        
        // hyperpparameter
        [SerializeField] private int skipFrameCount = 1;
        [Header("backward search")]
        [SerializeField] private int seedNum = 8;
        [SerializeField] private int maxBoundIter = 3;
        [SerializeField] private int maxSearchIter = 3;
        [SerializeField] private float threshold = 0.5f;
        
        [SerializeField] private bool printCullingInfo;
        [SerializeField] private bool saveDepthInfo;
        
        private Camera _camera;
        private Mesh _mesh;
        private Matrix4x4[] _objectsToWorldMatrix;
        private Matrix4x4 _prevProjectionMatrix, _prevViewMatrix, _curProjectionMatrix, _curViewMatrix;
        private int _objectNum;
        private int _yMapMipmapLevel, _yMapMipmapWidth;
        private int _predictCount = 0, _referenceCount = 0;
        private DepthSaveFeature _depthSaveFeature;
        private RenderTexture _prevDepthTexture;
        private RenderTexture _forwardWarpingDepthTexture;
        private RenderTexture _backwardWarpingDepthTexture;
        private RenderTexture _motionVectorsTexture;
        private ComputeBuffer _matrixBuffer;
        
        private int _motionVectorKernel;
        private int _minmaxMipmapKernel;
        private int _backwardKernel;
        private int _maxMipmapKernel;
        private int _computeVisibilityKernel;
        
        private static readonly int ForwardWarpingDepthTextureShaderPropertyID = Shader.PropertyToID("forward_warping_depth_texture");
        private static readonly int MotionVectorShaderPropertyID = Shader.PropertyToID("motion_vector");
        private static readonly int PrevDepthTextureShaderPropertyID = Shader.PropertyToID("prev_depth_texture");
        private static readonly int PrevProjectionMatrixShaderPropertyID = Shader.PropertyToID("prev_projection_matrix");
        private static readonly int InversedPrevProjectionViewMatrixShaderPropertyID = Shader.PropertyToID("inversed_prev_projection_view_matrix");
        private static readonly int CurrentProjectionViewMatrixShaderPropertyID = Shader.PropertyToID("current_projection_view_matrix");
        private static readonly int FarClipPlaneShaderPropertyID = Shader.PropertyToID("far_clip_plane");
        private static readonly int NearClipPlaneShaderPropertyID = Shader.PropertyToID("near_clip_plane");
        private static readonly int MinmaxMipmapLayerSrcShaderPropertyID = Shader.PropertyToID("minmax_mipmap_layer_src");
        private static readonly int MinmaxMipmapLayerDstShaderPropertyID = Shader.PropertyToID("minmax_mipmap_layer_dst");
        private static readonly int BackwardWarpingDepthTextureShaderPropertyID = Shader.PropertyToID("backward_warping_depth_texture");
        private static readonly int MotionVectorAndPredictedDepthTextureShaderPropertyID = Shader.PropertyToID("motion_vector_and_predicted_depth_texture");
        private static readonly int MipmapMotionVectorsTextureShaderPropertyID = Shader.PropertyToID("mipmap_motion_vectors_texture");
        private static readonly int SamplerLinearClampShaderPropertyID = Shader.PropertyToID("sampler_linear_clamp");
        private static readonly int MaxMipmapLevelShaderPropertyID = Shader.PropertyToID("max_mipmap_level");
        private static readonly int ScreenWidthShaderPropertyID = Shader.PropertyToID("screen_width");
        private static readonly int ScreenHeightShaderPropertyID = Shader.PropertyToID("screen_height");
        private static readonly int MaxBoundIterShaderPropertyID = Shader.PropertyToID("max_bound_iter");
        private static readonly int SeedNumShaderPropertyID = Shader.PropertyToID("seed_num");
        private static readonly int MaxSearchIterShaderPropertyID = Shader.PropertyToID("max_search_iter");
        private static readonly int ThresholdShaderPropertyID = Shader.PropertyToID("threshold");
        private static readonly int PrevYMapBufferShaderPropertyID = Shader.PropertyToID("prev_yMap_buffer");
        private static readonly int CurYMapBufferShaderPropertyID = Shader.PropertyToID("cur_yMap_buffer");
        private static readonly int YMapWidthShaderPropertyID = Shader.PropertyToID("yMap_width");
        private static readonly int YMapHeightShaderPropertyID = Shader.PropertyToID("yMap_height");
        private static readonly int ObjectNumShaderPropertyID = Shader.PropertyToID("object_num");
        private static readonly int YMapMipmapMaxLevelShaderPropertyID = Shader.PropertyToID("yMap_mipmap_max_level");
        private static readonly int BoundsMinShaderPropertyID = Shader.PropertyToID("bounds_min");
        private static readonly int BoundsMaxShaderPropertyID = Shader.PropertyToID("bounds_max");
        private static readonly int YMapMipmapBufferShaderPropertyID = Shader.PropertyToID("yMap_mipmap_buffer");
        private static readonly int ModelMatrixBufferShaderPropertyID = Shader.PropertyToID("model_matrix_buffer");
        private static readonly int CullResultBufferShaderPropertyID = Shader.PropertyToID("cull_result_buffer");
        private static readonly int DebugTextureShaderPropertyID = Shader.PropertyToID("debug_texture");

#if DEBUGPRINT
        private int fileCount = 0;
        private RenderTexture _debugTexture;
        private static readonly int DebugTextureShaderPropertyID = Shader.PropertyToID("DebugTexture");
#endif
        
        public override void Init(Camera cam, Mesh mesh, Matrix4x4[] matrices)
        {
            _camera = cam;
            _mesh = mesh;
            _objectsToWorldMatrix = matrices;
            _objectNum = matrices.Length;
            
            _yMapMipmapWidth = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
            _yMapMipmapLevel = Mathf.FloorToInt(Mathf.Log(_yMapMipmapWidth) / Mathf.Log(2));
            _backwardWarpingDepthTexture = new RenderTexture(_yMapMipmapWidth, _yMapMipmapWidth, 0, RenderTextureFormat.RFloat);
            _backwardWarpingDepthTexture.enableRandomWrite = true;
            _backwardWarpingDepthTexture.useMipMap = true;
            _backwardWarpingDepthTexture.autoGenerateMips = false;
            _backwardWarpingDepthTexture.Create();
            RenderTexture.active = _backwardWarpingDepthTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = null;
            
            _forwardWarpingDepthTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            _forwardWarpingDepthTexture.enableRandomWrite = true;
            _forwardWarpingDepthTexture.Create();
            _motionVectorsTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            _motionVectorsTexture.enableRandomWrite = true;
            _motionVectorsTexture.useMipMap = true;
            _motionVectorsTexture.autoGenerateMips = false;
            _motionVectorsTexture.Create();
#if DEBUGPRINT
            _debugTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            _debugTexture.enableRandomWrite = true;
            _debugTexture.Create();
#endif
            _matrixBuffer = new ComputeBuffer(matrices.Length, sizeof(float) * 16);
            _matrixBuffer.SetData(matrices);
            
            _motionVectorKernel = idwCullingShader.FindKernel("get_motion_vector");
            _minmaxMipmapKernel = idwCullingShader.FindKernel("generate_min_max_mipmap");
            _backwardKernel = idwCullingShader.FindKernel("backward_search");
            _maxMipmapKernel = idwCullingShader.FindKernel("generate_max_mipmap");
            _computeVisibilityKernel = idwCullingShader.FindKernel("compute_visibility");
            
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
                _curViewMatrix = _camera.worldToCameraMatrix;
                _curProjectionMatrix = _camera.projectionMatrix;
                _prevDepthTexture = _depthSaveFeature.GetDepthTexture();
                // get motion vector and forward depth
                idwCullingShader.SetTexture(_motionVectorKernel, ForwardWarpingDepthTextureShaderPropertyID, _forwardWarpingDepthTexture);
                idwCullingShader.SetTexture(_motionVectorKernel, MotionVectorShaderPropertyID, _motionVectorsTexture);
                idwCullingShader.SetTexture(_motionVectorKernel, PrevDepthTextureShaderPropertyID, _prevDepthTexture);
                idwCullingShader.SetMatrix(PrevProjectionMatrixShaderPropertyID, _prevProjectionMatrix);
                idwCullingShader.SetMatrix(InversedPrevProjectionViewMatrixShaderPropertyID, (_prevProjectionMatrix * _prevViewMatrix).inverse);
                idwCullingShader.SetMatrix(CurrentProjectionViewMatrixShaderPropertyID, _curProjectionMatrix * _curViewMatrix);
                idwCullingShader.SetFloat(FarClipPlaneShaderPropertyID, _camera.farClipPlane);
                idwCullingShader.SetFloat(NearClipPlaneShaderPropertyID, _camera.nearClipPlane);
                idwCullingShader.Dispatch(_motionVectorKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                // get motion vector mipmap
                int currentWidth = Screen.width;
                int currentHeight = Screen.height;
                int level = 0;
                while (currentWidth > 1 || currentHeight > 1)
                {
                    int nextWidth = Mathf.Max(1, currentWidth / 2);
                    int nextHeight = Mathf.Max(1, currentHeight / 2);
                    idwCullingShader.SetTexture(_minmaxMipmapKernel, MinmaxMipmapLayerSrcShaderPropertyID, _motionVectorsTexture, level);
                    idwCullingShader.SetTexture(_minmaxMipmapKernel, MinmaxMipmapLayerDstShaderPropertyID, _motionVectorsTexture, level + 1);
                    idwCullingShader.Dispatch(_minmaxMipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                    currentWidth = nextWidth;
                    currentHeight = nextHeight;
                    ++ level;
                }
                // backward search
                idwCullingShader.SetTexture(_backwardKernel, MotionVectorAndPredictedDepthTextureShaderPropertyID, _forwardWarpingDepthTexture);
                idwCullingShader.SetTexture(_backwardKernel, BackwardWarpingDepthTextureShaderPropertyID, _backwardWarpingDepthTexture);
                idwCullingShader.SetTexture(_backwardKernel, MipmapMotionVectorsTextureShaderPropertyID, _motionVectorsTexture);
                idwCullingShader.SetInt(MaxMipmapLevelShaderPropertyID, level);
                idwCullingShader.SetInt(ScreenWidthShaderPropertyID, Screen.width);
                idwCullingShader.SetInt(ScreenHeightShaderPropertyID, Screen.height);
                idwCullingShader.SetInt(MaxBoundIterShaderPropertyID, maxBoundIter);
                idwCullingShader.SetInt(SeedNumShaderPropertyID, seedNum);
                idwCullingShader.SetInt(MaxSearchIterShaderPropertyID, maxSearchIter);
                idwCullingShader.SetFloat(ThresholdShaderPropertyID, threshold);
                idwCullingShader.Dispatch(_backwardKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                // generate yMaps
                for (int layer = 0, curWidth = Screen.width, curHeight = Screen.height; layer < _yMapMipmapLevel; ++layer)
                {
                    int nextWidth = (1 + curWidth) / 2;
                    int nextHeight = (1 + curHeight) / 2;
                    idwCullingShader.SetTexture(_maxMipmapKernel, PrevYMapBufferShaderPropertyID, _backwardWarpingDepthTexture, layer);
                    idwCullingShader.SetTexture(_maxMipmapKernel, CurYMapBufferShaderPropertyID, _backwardWarpingDepthTexture, layer + 1);
                    idwCullingShader.SetInt(YMapWidthShaderPropertyID, nextWidth);
                    idwCullingShader.SetInt(YMapHeightShaderPropertyID, nextHeight);
                    idwCullingShader.Dispatch(_maxMipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                    curWidth = nextWidth;
                    curHeight = nextHeight;
                }
                // cull object
                idwCullingShader.SetBuffer(_computeVisibilityKernel, CullResultBufferShaderPropertyID, cullResultBuffer);
                idwCullingShader.SetBuffer(_computeVisibilityKernel, ModelMatrixBufferShaderPropertyID, _matrixBuffer);
                idwCullingShader.SetTexture(_computeVisibilityKernel, YMapMipmapBufferShaderPropertyID, _backwardWarpingDepthTexture);
                idwCullingShader.SetMatrix(CurrentProjectionViewMatrixShaderPropertyID, _camera.projectionMatrix * _camera.worldToCameraMatrix);
                idwCullingShader.SetInt(ObjectNumShaderPropertyID, _objectNum);
                idwCullingShader.SetVector(BoundsMaxShaderPropertyID, _mesh.bounds.max);
                idwCullingShader.SetVector(BoundsMinShaderPropertyID, _mesh.bounds.min);
                idwCullingShader.SetInt(ScreenWidthShaderPropertyID, Screen.width);
                idwCullingShader.SetInt(ScreenHeightShaderPropertyID, Screen.height);
                idwCullingShader.SetInt(YMapMipmapMaxLevelShaderPropertyID, _yMapMipmapLevel);
                idwCullingShader.Dispatch(_computeVisibilityKernel, (_objectNum + 63) / 64, 1, 1);

                if (printCullingInfo)
                {
                    ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
                    ComputeBuffer.CopyCount(cullResultBuffer, countBuffer, 0);
                    uint[] countData = new uint[1];
                    countBuffer.GetData(countData);
                    countBuffer.Release();
                    uint actualCount = countData[0];
                    Debug.Log($"IDW Culled from {_objectNum} to {actualCount} : {_objectNum - actualCount}");
                }

                if (saveDepthInfo)
                {
                    SaveRenderTextureToBin(_backwardWarpingDepthTexture,
                        "Assets/Record/Predict/depthData" + _predictCount + ".bin", true, false);
                    ++_predictCount;
                    SaveRenderTextureToBin(_prevDepthTexture,
                        "Assets/Record/Reference/depthData" + _referenceCount + ".bin", false, true);
                    ++_referenceCount;
                }

#if DEBUGPRINT
                SaveRenderTextureToFile(_debugTexture, 0, "Assets/Debug/DepthData" + fileCount + ".txt");
                ++fileCount;
#endif
            }

            _prevViewMatrix = _curViewMatrix;
            _prevProjectionMatrix = _curProjectionMatrix;
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
        
        private float GetScreenDepth(float depth)
        {
            float farClipPlane = _camera.farClipPlane, nearClipPlane = _camera.nearClipPlane;
            float z = (farClipPlane * nearClipPlane) / (nearClipPlane + depth * (farClipPlane - nearClipPlane));
            float ndcZ = -_prevProjectionMatrix[2, 2] + _prevProjectionMatrix[2, 3] / z;
            return (ndcZ + 1.0f) * 0.5f;
        }
        
        private void SaveRenderTextureToBin(RenderTexture texture, string filePath, bool clamp = false, bool convertToScreen = false)
        {
            if (texture == null)
            {
                Debug.LogError("RenderTexture is not initialized!");
                return;
            }
            int clampedWidth = clamp ? Mathf.Min(texture.width, 1920) : texture.width;
            int clampedHeight = clamp ? Mathf.Min(texture.height, 1080) : texture.height;
            Texture2D tempTexture = new Texture2D(clampedWidth, clampedHeight, TextureFormat.RFloat, false);
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = texture;
            tempTexture.ReadPixels(new Rect(0, 0, clampedWidth, clampedHeight), 0, 0);
            tempTexture.Apply();
            float[] floatData = tempTexture.GetRawTextureData<float>().ToArray();
            if (convertToScreen)
            {
                for (int i = 0; i < floatData.Length; i++)
                {
                    floatData[i] = GetScreenDepth(floatData[i]);
                }
            }
            byte[] bytes = new byte[floatData.Length * sizeof(float)];
            Buffer.BlockCopy(floatData, 0, bytes, 0, bytes.Length);
            try
            {
                File.WriteAllBytes(filePath, bytes);
                Debug.Log($"Successfully saved RenderTexture to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save RenderTexture: {e.Message}");
            }
            RenderTexture.active = currentRT;
            Destroy(tempTexture);
        }
        
#if DEBUGPRINT
        private void SaveRenderTextureToFile(RenderTexture texture, int mipmapLevel = 0, string filePath = "Assets/Debug/DepthData.txt")
        {
            if (!texture)
            {
                Debug.LogError("RenderTexture is null!");
                return;
            }
            int width = texture.width >> mipmapLevel;
            int height = texture.height >> mipmapLevel;
            if (width < 1) width = 1;
            if (height < 1) height = 1;
            RenderTexture tempRT = new RenderTexture(width, height, 0, texture.format);
            tempRT.enableRandomWrite = true;
            tempRT.Create();
            Graphics.CopyTexture(texture, 0, mipmapLevel, tempRT, 0, 0);
            Texture2D tempTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = tempRT;
            tempTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tempTexture.Apply();
            RenderTexture.active = previousActive;
            Color[] pixels = tempTexture.GetPixels();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"RenderTexture Size at Mipmap Level {mipmapLevel}: {width}x{height}");
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    sb.AppendLine($"[{x}, {y}]: {pixels[index]}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Depth data saved to {filePath} for mipmap level {mipmapLevel}");

            Destroy(tempTexture);
            Destroy(tempRT);
        }

        private void SaveFloatsToFile(float[] ans, string filePath = "Assets/Debug/DepthData.txt")
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"RenderTexture Size: {_prevDepthTexture.width}x{_prevDepthTexture.height}");
            for (int y = 0; y < _prevDepthTexture.height; y++)
            {
                for (int x = 0; x < _prevDepthTexture.width; x++)
                {
                    int index = y * _prevDepthTexture.width + x;
                    sb.AppendLine($"[{x}, {y}]: {ans[index]}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Depth data saved to {filePath}");
        }
        
        private void SaveIntsToFile(int[] ans, string filePath = "Assets/Debug/DepthData.txt")
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"ObjectNum: {ans.Length}");
            for (int index = 0; index < ans.Length; index ++)
                sb.AppendLine($"[{index}]: {ans[index]}");

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Depth data saved to {filePath}");
        }
        
        private void SaveColorsToFile(Color[] ans, string filePath = "Assets/Debug/DepthData.txt")
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"RenderTexture Size: {_prevDepthTexture.width}x{_prevDepthTexture.height}");
            for (int y = 0; y < _prevDepthTexture.height; y++)
            {
                for (int x = 0; x < _prevDepthTexture.width; x++)
                {
                    int index = y * _prevDepthTexture.width + x;
                    sb.AppendLine($"[{x}, {y}]: {ans[index]}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Depth data saved to {filePath}");
        }
#endif
    }
}