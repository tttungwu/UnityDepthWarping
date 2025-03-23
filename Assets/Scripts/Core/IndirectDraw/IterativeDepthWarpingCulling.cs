// #define DEBUGPRINT
// #define EVALUATE

using System.Collections.Generic;
using System.Reflection;
using Features;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Core.IndirectDraw
{
    public class IterativeDepthWarpingCulling : CullingMethod
    {
        private Camera _camera;
        private float _nearClipPlane, _farClipPlane;
        private Mesh _mesh;
        
        private DepthSaveFeature _depthSaveFeature;
        private RenderTexture _prevDepthTexture;
        private RenderTexture _forwardWarpingDepthTexture;
        private RenderTexture _backwardWarpingDepthTexture;
        private RenderTexture _motionVectorsTexture;
        private ComputeBuffer _boundingBoxesBuffer;
        private ComputeBuffer _visibilityBuffer;
        private int _yMapMipmapLevel, _yMapMipmapWidth;
        private int _objectNum;
        private Matrix4x4 _prevProjectionMatrix, _prevViewMatrix, _curProjectionMatrix, _curViewMatrix;
        
        public ComputeShader _IDWComputeShader;
        private int _motionVectorKernel;
        private int _minmaxMipmapKernel;
        private int _backwardKernel;
        private int _maxMipmapKernel;
        private int _computeVisibilityKernel;
        
        // hyperpparameter
        public int skipFrameCount = 5;
        [Header("backward search")]
        public int seedNum = 8;
        public int maxBoundIter = 3;
        public int maxSearchIter = 3;
        public float threshold = 0.5f;
        
        private static readonly int FarClipPlaneShaderPropertyID = Shader.PropertyToID("FarClipPlane");
        private static readonly int NearClipPlaneShaderPropertyID = Shader.PropertyToID("NearClipPlane");
        private static readonly int ForwardWarpingDepthTextureShaderPropertyID = Shader.PropertyToID("ForwardWarpingDepthTexture");
        private static readonly int MotionVectorShaderPropertyID = Shader.PropertyToID("MotionVector");
        private static readonly int PrevDepthTextureShaderPropertyID = Shader.PropertyToID("PrevDepthTexture");
        private static readonly int PrevProjectionMatrixShaderPropertyID = Shader.PropertyToID("PrevProjectionMatrix");
        private static readonly int InversedPrevProjectionViewMatrixShaderPropertyID = Shader.PropertyToID("InversedPrevProjectionViewMatrix");
        private static readonly int CurrentProjectionViewMatrixShaderPropertyID = Shader.PropertyToID("CurrentProjectionViewMatrix");
        private static readonly int MinmaxMipmapLayerSrcShaderPropertyID = Shader.PropertyToID("MinmaxMipmapLayerSrc");
        private static readonly int MinmaxMipmapLayerDstShaderPropertyID = Shader.PropertyToID("MinmaxMipmapLayerDst");
        private static readonly int BackwardWarpingDepthTextureShaderPropertyID = Shader.PropertyToID("BackwardWarpingDepthTexture");
        private static readonly int MotionVectorAndPredictedDepthTextureShaderPropertyID = Shader.PropertyToID("MotionVectorAndPredictedDepthTexture");
        private static readonly int MipmapMotionVectorsTextureShaderPropertyID = Shader.PropertyToID("MipmapMotionVectorsTexture");
        private static readonly int MaxMipmapLevelShaderPropertyID = Shader.PropertyToID("MaxMipmapLevel");
        private static readonly int WidthShaderPropertyID = Shader.PropertyToID("Width");
        private static readonly int HeightShaderPropertyID = Shader.PropertyToID("Height");
        private static readonly int MaxBoundIterShaderPropertyID = Shader.PropertyToID("MaxBoundIter");
        private static readonly int SeedNumShaderPropertyID = Shader.PropertyToID("SeedNum");
        private static readonly int MaxSearchIterShaderPropertyID = Shader.PropertyToID("MaxSearchIter");
        private static readonly int ThresholdShaderPropertyID = Shader.PropertyToID("Threshold");
        private static readonly int PrevYMapBufferShaderPropertyID = Shader.PropertyToID("PrevYMapBuffer");
        private static readonly int CurYMapBufferShaderPropertyID = Shader.PropertyToID("CurYMapBuffer");
        private static readonly int YMapWidthShaderPropertyID = Shader.PropertyToID("YMapWidth");
        private static readonly int YMapHeightShaderPropertyID = Shader.PropertyToID("YMapHeight");
        private static readonly int BoundingBoxesShaderPropertyID = Shader.PropertyToID("BoundingBoxes");
        private static readonly int VisibilityShaderPropertyID = Shader.PropertyToID("Visibility");
        private static readonly int ObjectNumShaderPropertyID = Shader.PropertyToID("ObjectNum");
        private static readonly int YMapMipmapMaxLevelShaderPropertyID = Shader.PropertyToID("YMapMipmapMaxLevel");
        private static readonly int YMapMipmapBufferShaderPropertyID = Shader.PropertyToID("YMapMipmapBuffer");
        
#if DEBUGPRINT
        private int fileCount = 0;
#endif
#if EVALUATE
        private int predictCount = 0;
        private int referenceCount = 0;
#endif
        
        public override void Init(Camera cam, Mesh mesh, Matrix4x4[] matrices)
        {
            _camera = cam;
            _camera.depthTextureMode = DepthTextureMode.Depth;
            _nearClipPlane = _camera.nearClipPlane;
            _farClipPlane = _camera.farClipPlane;
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
            
            _motionVectorKernel = _IDWComputeShader.FindKernel("GetMotionVector");
            _minmaxMipmapKernel = _IDWComputeShader.FindKernel("GenerateMinMaxMipmap");
            _backwardKernel = _IDWComputeShader.FindKernel("BackwardSearch");
            _maxMipmapKernel = _IDWComputeShader.FindKernel("GenerateMaxMipmap");
            _computeVisibilityKernel = _IDWComputeShader.FindKernel("ComputeVisibility");
        }
        
        public override void Cull(Matrix4x4[] matrices)
        {
            if (skipFrameCount > 0) --skipFrameCount;
            else
            {
                _curViewMatrix = _camera.worldToCameraMatrix;
                _curProjectionMatrix = _camera.projectionMatrix;
                _prevDepthTexture = _depthSaveFeature.GetDepthTexture();
                // get motion vector and forward depth
                _IDWComputeShader.SetTexture(_motionVectorKernel, ForwardWarpingDepthTextureShaderPropertyID, _forwardWarpingDepthTexture);
                _IDWComputeShader.SetTexture(_motionVectorKernel, MotionVectorShaderPropertyID, _motionVectorsTexture);
                _IDWComputeShader.SetTexture(_motionVectorKernel, PrevDepthTextureShaderPropertyID, _prevDepthTexture);
                _IDWComputeShader.SetMatrix(PrevProjectionMatrixShaderPropertyID, _prevProjectionMatrix);
                _IDWComputeShader.SetMatrix(InversedPrevProjectionViewMatrixShaderPropertyID, (_prevProjectionMatrix * _prevViewMatrix).inverse);
                _IDWComputeShader.SetMatrix(CurrentProjectionViewMatrixShaderPropertyID, _curProjectionMatrix * _curViewMatrix);
                _IDWComputeShader.SetFloat(FarClipPlaneShaderPropertyID, _farClipPlane);
                _IDWComputeShader.SetFloat(NearClipPlaneShaderPropertyID, _nearClipPlane);
                _IDWComputeShader.Dispatch(_motionVectorKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                // get motion vector mipmap
                int currentWidth = Screen.width;
                int currentHeight = Screen.height;
                int level = 0;
                while (currentWidth > 1 || currentHeight > 1)
                {
                    int nextWidth = Mathf.Max(1, currentWidth / 2);
                    int nextHeight = Mathf.Max(1, currentHeight / 2);
                    _IDWComputeShader.SetTexture(_minmaxMipmapKernel, MinmaxMipmapLayerSrcShaderPropertyID, _motionVectorsTexture, level);
                    _IDWComputeShader.SetTexture(_minmaxMipmapKernel, MinmaxMipmapLayerDstShaderPropertyID, _motionVectorsTexture, level + 1);
                    _IDWComputeShader.Dispatch(_minmaxMipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                    currentWidth = nextWidth;
                    currentHeight = nextHeight;
                    ++ level;
                }
                // backward search
                _IDWComputeShader.SetTexture(_backwardKernel, MotionVectorAndPredictedDepthTextureShaderPropertyID, _forwardWarpingDepthTexture);
                _IDWComputeShader.SetTexture(_backwardKernel, BackwardWarpingDepthTextureShaderPropertyID, _backwardWarpingDepthTexture);
                _IDWComputeShader.SetTexture(_backwardKernel, MipmapMotionVectorsTextureShaderPropertyID, _motionVectorsTexture);
                _IDWComputeShader.SetInt(MaxMipmapLevelShaderPropertyID, level);
                _IDWComputeShader.SetInt(WidthShaderPropertyID, Screen.width);
                _IDWComputeShader.SetInt(HeightShaderPropertyID, Screen.height);
                _IDWComputeShader.SetInt(MaxBoundIterShaderPropertyID, maxBoundIter);
                _IDWComputeShader.SetInt(SeedNumShaderPropertyID, seedNum);
                _IDWComputeShader.SetInt(MaxSearchIterShaderPropertyID, maxSearchIter);
                _IDWComputeShader.SetFloat(ThresholdShaderPropertyID, threshold);
                _IDWComputeShader.Dispatch(_backwardKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                // generate yMaps
                for (int layer = 0, curWidth = Screen.width, curHeight = Screen.height; layer < _yMapMipmapLevel; ++layer)
                {
                    int nextWidth = (1 + curWidth) / 2;
                    int nextHeight = (1 + curHeight) / 2;
                    _IDWComputeShader.SetTexture(_maxMipmapKernel, PrevYMapBufferShaderPropertyID, _backwardWarpingDepthTexture, layer);
                    _IDWComputeShader.SetTexture(_maxMipmapKernel, CurYMapBufferShaderPropertyID, _backwardWarpingDepthTexture, layer + 1);
                    _IDWComputeShader.SetInt(YMapWidthShaderPropertyID, nextWidth);
                    _IDWComputeShader.SetInt(YMapHeightShaderPropertyID, nextHeight);
                    _IDWComputeShader.Dispatch(_maxMipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                    curWidth = nextWidth;
                    curHeight = nextHeight;
                }
                // cull object
                _IDWComputeShader.SetBuffer(_computeVisibilityKernel, BoundingBoxesShaderPropertyID, _boundingBoxesBuffer);
                _IDWComputeShader.SetBuffer(_computeVisibilityKernel, VisibilityShaderPropertyID, _visibilityBuffer);
                _IDWComputeShader.SetTexture(_computeVisibilityKernel, YMapMipmapBufferShaderPropertyID, _backwardWarpingDepthTexture);
                _IDWComputeShader.SetMatrix(CurrentProjectionViewMatrixShaderPropertyID, _camera.projectionMatrix * _camera.worldToCameraMatrix);
                _IDWComputeShader.SetInt(ObjectNumShaderPropertyID, _objectNum);
                _IDWComputeShader.SetInt(WidthShaderPropertyID, Screen.width);
                _IDWComputeShader.SetInt(HeightShaderPropertyID, Screen.height);
                _IDWComputeShader.SetInt(YMapMipmapMaxLevelShaderPropertyID, _yMapMipmapLevel);
                _IDWComputeShader.Dispatch(_computeVisibilityKernel, (_objectNum + 63) / 64, 1, 1);

#if EVALUATE
                // evaluate
                SaveRenderTextureToBin(_backwardWarpingDepthTexture,
                    "Assets/Record/Predict/depthData" + predictCount + ".bin", true, false);
                ++predictCount;
                SaveRenderTextureToBin(_prevDepthTexture,
                    "Assets/Record/Reference/depthData" + referenceCount + ".bin", false, true);
                ++referenceCount;
#endif
            }

            _prevViewMatrix = _curViewMatrix;
            _prevProjectionMatrix = _curProjectionMatrix;
        }
        
#if EVALUATE
        private float GetScreenDepth(float depth)
        {
            float z = (_farClipPlane * _nearClipPlane) / (_nearClipPlane + depth * (_farClipPlane - _nearClipPlane));
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
#endif
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