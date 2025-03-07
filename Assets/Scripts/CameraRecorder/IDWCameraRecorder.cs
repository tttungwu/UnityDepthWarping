#define DEBUGPRINT
// #define EVALUATE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CameraRecorder
{
    [RequireComponent(typeof(Camera))]
    public class IDWCameraRecorder : MonoBehaviour
    {
        public float recordInterval = 0.1f;
        public bool playbackFromFile = false;
        public float playbackSpeed = 1.0f;
        public bool enableUI = false;

        private Camera _camera;
        private float _nearClipPlane, _farClipPlane;

        private float _recordTimer = 0.0f;
        private bool _isRecording = false;
        private bool _isReplaying = false;
        private int _currentReplayFrameIndex = 0;
        private float _lerpFactor = 0.0f;
        
        private List<Vector3> _positions = new ();
        private List<Quaternion> _rotations = new ();

        private bool _isFinished = false;
        
        private DateTime _timeStart;
        private const string SavingDir = "Assets/Prefabs/CameraRoamingPath/";
        public RoamingPathAsset playbackRoamingPathAsset;
        
        private DepthSaveFeature _depthSaveFeature;
        private RenderTexture _prevDepthTexture;
        private RenderTexture _forwardWarpingDepthTexture;
        private RenderTexture _backwardWarpingDepthTexture;
        private RenderTexture _motionVectorsTexture;
        private ComputeBuffer _boundingBoxesBuffer;
        private ComputeBuffer _visibilityBuffer;
        private int[] _visibilityOutcome;
        private int _objectNum;
        private int _yMapNBufferCount, _yMapNBufferWidth, _yMapNBufferHeight;
        private Matrix4x4 _prevProjectionMatrix, _prevViewMatrix;
        
#if DEBUGPRINT
        private int fileCount = 0;
#endif
#if EVALUATE
        private int predictCount = 0;
        private int referenceCount = 0;
#endif
        
        public ComputeShader _IDWComputeShader;
        private int _motionVectorKernel;
        private int _minmaxMipmapKernel;
        private int _backwardKernel;
        private int _maxMipmapKernel;
        private int _nBufferKernel;
        private int _computeVisibilityKernel;
        
        // hyperpparameter
        public int skipFrameCount = 200;
        [Header("backward search")]
        public int seedNum = 8;
        public int maxBoundIter = 3;
        public int maxSearchIter = 3;
        public float threshold = 0.1f;
        [Header("yMaps")]
        public int yMapMipmapCount = 3;
        
        private RenderTexture debugTexture;

        void Start()
        {
            _camera = GetComponent<Camera>();
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
            _forwardWarpingDepthTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            _forwardWarpingDepthTexture.enableRandomWrite = true;
            _forwardWarpingDepthTexture.Create();
            _backwardWarpingDepthTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat);
            _backwardWarpingDepthTexture.enableRandomWrite = true;
            _backwardWarpingDepthTexture.useMipMap = true;
            _backwardWarpingDepthTexture.autoGenerateMips = false;
            _backwardWarpingDepthTexture.Create();
            _motionVectorsTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            _motionVectorsTexture.enableRandomWrite = true;
            _motionVectorsTexture.useMipMap = true;
            _motionVectorsTexture.autoGenerateMips = false;
            _motionVectorsTexture.Create();
            
            _yMapNBufferWidth = Mathf.FloorToInt(Screen.width / Mathf.Pow(2, yMapMipmapCount));
            _yMapNBufferHeight = Mathf.FloorToInt(Screen.height / Mathf.Pow(2, yMapMipmapCount));
            _yMapNBufferCount = Mathf.FloorToInt(Mathf.Log(Mathf.Max(_yMapNBufferWidth, _yMapNBufferHeight)) / Mathf.Log(2));
            Debug.Log($"yMapNBufferWidth: {_yMapNBufferWidth}; yMapNBufferHeight: {_yMapNBufferHeight}; yMapNBufferCount: {_yMapNBufferCount}");
            Occludee[] occludees = FindObjectsOfType<Occludee>();
            _objectNum = occludees.Length;
            Vector3[] boundingBoxesData = new Vector3[_objectNum * 2];
            for (int i = 0; i < _objectNum; ++ i)
            {
                Bounds bounds = occludees[i].GetBounds();
                boundingBoxesData[i * 2] = bounds.min;
                boundingBoxesData[i * 2 + 1] = bounds.max;
            }
            _boundingBoxesBuffer = new ComputeBuffer(_objectNum, sizeof(float) * 3 * 2);
            _boundingBoxesBuffer.SetData(boundingBoxesData);
            _visibilityOutcome = new int[_objectNum];
            _visibilityBuffer = new ComputeBuffer(_objectNum, sizeof(int));
            _visibilityBuffer.SetData(_visibilityOutcome);
            Debug.Log($"bounding box buffer is created for {_objectNum}");
            
            debugTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            debugTexture.enableRandomWrite = true;
            debugTexture.Create();
            
            _motionVectorKernel = _IDWComputeShader.FindKernel("GetMotionVector");
            _minmaxMipmapKernel = _IDWComputeShader.FindKernel("GenerateMinMaxMipmap");
            _backwardKernel = _IDWComputeShader.FindKernel("BackwardSearch");
            _maxMipmapKernel = _IDWComputeShader.FindKernel("GenerateMaxMipmap");
            _nBufferKernel = _IDWComputeShader.FindKernel("GenerateNBuffer");
            _computeVisibilityKernel = _IDWComputeShader.FindKernel("ComputeVisibility");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartRecording();
            if (Input.GetKeyDown(KeyCode.P) || (!_isReplaying && !_isFinished && playbackFromFile && playbackRoamingPathAsset)) StartReplaying();
            if (Input.GetKeyDown(KeyCode.T)) Stop();

            if (_isRecording)
            {
                _recordTimer += Time.deltaTime;
                if (_recordTimer >= recordInterval)
                {
                    _positions.Add(transform.position);
                    _rotations.Add(transform.rotation);
                    _recordTimer = 0;
                }
            }
            else if (_isReplaying)
            {
                if (_currentReplayFrameIndex < _positions.Count)
                {
                    float lerpFactorIncrement = Time.deltaTime / recordInterval * playbackSpeed;
                    _lerpFactor += lerpFactorIncrement;
                    if (_lerpFactor > 1.0f)
                    {
                        _lerpFactor = 0.0f;
                        ++ _currentReplayFrameIndex;
                    }
                    if (_currentReplayFrameIndex == _positions.Count - 1)
                    {
                        Stop();
                        return;
                    }

                    if (skipFrameCount == 0)
                    {
                        _prevDepthTexture = _depthSaveFeature.GetDepthTexture();
                        _prevViewMatrix = _camera.worldToCameraMatrix;
                        _prevProjectionMatrix = _camera.projectionMatrix;
                    }
                    transform.position = Vector3.Lerp(_positions[_currentReplayFrameIndex], _positions[_currentReplayFrameIndex + 1], _lerpFactor);
                    transform.rotation = Quaternion.Slerp(_rotations[_currentReplayFrameIndex], _rotations[_currentReplayFrameIndex + 1], _lerpFactor);
                    if (skipFrameCount == 0)
                    {
                        // get motion vector and forward depth
                        _IDWComputeShader.SetTexture(_motionVectorKernel, "ForwardWarpingDepthTexture", _forwardWarpingDepthTexture);
                        _IDWComputeShader.SetTexture(_motionVectorKernel, "MotionVector", _motionVectorsTexture);
                        _IDWComputeShader.SetTexture(_motionVectorKernel, "PrevDepthTexture", _prevDepthTexture);
                        _IDWComputeShader.SetMatrix("PrevProjectionMatrix", _prevProjectionMatrix);
                        _IDWComputeShader.SetMatrix("InversedPrevProjectionViewMatrix", (_prevProjectionMatrix * _prevViewMatrix).inverse);
                        _IDWComputeShader.SetMatrix("CurrentProjectionViewMatrix", _camera.projectionMatrix * _camera.worldToCameraMatrix);
                        _IDWComputeShader.SetFloat("FarClipPlane", _camera.farClipPlane);
                        _IDWComputeShader.SetFloat("NearClipPlane", _camera.nearClipPlane);
                        _IDWComputeShader.Dispatch(_motionVectorKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                        // get motion vector mipmap
                        int currentWidth = Screen.width;
                        int currentHeight = Screen.height;
                        int level = 0;
                        while (currentWidth > 1 || currentHeight > 1)
                        {
                            int nextWidth = Mathf.Max(1, currentWidth / 2);
                            int nextHeight = Mathf.Max(1, currentHeight / 2);
                            _IDWComputeShader.SetTexture(_minmaxMipmapKernel, "MinmaxMipmapLayerSrc", _motionVectorsTexture, level);
                            _IDWComputeShader.SetTexture(_minmaxMipmapKernel, "MinmaxMipmapLayerDst", _motionVectorsTexture, level + 1);
                            _IDWComputeShader.Dispatch(_minmaxMipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                            currentWidth = nextWidth;
                            currentHeight = nextHeight;
                            ++ level;
                        }
                        // backward search
                        _IDWComputeShader.SetTexture(_backwardKernel, "MotionVectorAndPredictedDepthTexture", _forwardWarpingDepthTexture);
                        _IDWComputeShader.SetTexture(_backwardKernel, "BackwardWarpingDepthTexture", _backwardWarpingDepthTexture);
                        _IDWComputeShader.SetTexture(_backwardKernel, "MipmapMotionVectorsTexture", _motionVectorsTexture);
                        _IDWComputeShader.SetTexture(_backwardKernel, "DebugTexture", debugTexture);
                        _IDWComputeShader.SetInt("MaxMipmapLevel", level);
                        _IDWComputeShader.SetInt("Width", Screen.width);
                        _IDWComputeShader.SetInt("Height", Screen.height);
                        _IDWComputeShader.SetInt("MaxBoundIter", maxBoundIter);
                        _IDWComputeShader.SetInt("SeedNum", seedNum);
                        _IDWComputeShader.SetInt("MaxSearchIter", maxSearchIter);
                        _IDWComputeShader.SetFloat("Threshold", threshold);
                        _IDWComputeShader.Dispatch(_backwardKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                        // generate yMaps
                        for (int layer = 0, curWidth = Screen.width, curHeight = Screen.height; layer < yMapMipmapCount; ++layer)
                        {
                            int nextWidth = Mathf.Max(1, curWidth / 2);
                            int nextHeight = Mathf.Max(1, curHeight / 2);
                            _IDWComputeShader.SetTexture(_maxMipmapKernel, "PrevYMapBuffer", _backwardWarpingDepthTexture, layer);
                            _IDWComputeShader.SetTexture(_maxMipmapKernel, "CurYMapBuffer", _backwardWarpingDepthTexture, layer + 1);
                            _IDWComputeShader.Dispatch(_maxMipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                            curWidth = nextWidth;
                            curHeight = nextHeight;
                        }
                        // // cull object
                        // IDWComputeShader.SetBuffer(computeVisibilityKernel, "BoundingBoxes", boundingBoxesBuffer);
                        // IDWComputeShader.SetBuffer(computeVisibilityKernel, "Visibility", visibilityBuffer);
                        // IDWComputeShader.SetMatrix("CurrentProjectionViewMatrix", _camera.projectionMatrix * _camera.worldToCameraMatrix);
                        // IDWComputeShader.SetInt("ObjectNum", objectNum);
                        // IDWComputeShader.Dispatch(computeVisibilityKernel, (objectNum + 63) / 64, 1, 1);
                        // visibilityBuffer.GetData(visibilityOutcome);
                        // for (int i = 0; i < visibilityOutcome.Length; i++)
                        //     Debug.Log(visibilityOutcome[i]);
                        // Debug.Log("---------------");

#if DEBUGPRINT
                        // debug
                        // SaveRenderTextureToFile(_forwardWarpingDepthTexture, 0, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
                        // for (int i = 0; i <= level; ++i)
                        // {
                        //     SaveRenderTextureToFile(_motionVectorsTexture, i, "Assets/Debug/DepthData" + fileCount + ".txt");
                        //     ++fileCount;
                        // }
                        // SaveRenderTextureToFile(backwardWarpingDepthTexture, 0, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
                        // SaveRenderTextureToFile(motionVectorsTexture, level, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
                        // SaveRenderTextureToFile(debugTexture, 0, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
                        //
                        // Texture2D tempTexture = new Texture2D(prevDepthTexture.width, prevDepthTexture.height, TextureFormat.RFloat, false);
                        // RenderTexture.active = prevDepthTexture;
                        // tempTexture.ReadPixels(new Rect(0, 0, prevDepthTexture.width, prevDepthTexture.height), 0, 0);
                        // tempTexture.Apply();
                        // Color[] pixels = tempTexture.GetPixels();
                        // Color[] debugDepth = new Color[prevDepthTexture.width * prevDepthTexture.height];
                        //
                        // for (int y = 0; y < prevDepthTexture.height; y++)
                        // {
                        //     for (int x = 0; x < prevDepthTexture.width; x++)
                        //     {
                        //         int index = y * prevDepthTexture.width + x;
                        //         float depth = GetScreenDepth(pixels[index].r);
                        //         debugDepth[index].r = depth;
                        //     }
                        // }
                        // SaveColorsToFile(debugDepth, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
                        // for (int layer = 0; layer < yMapsSize; ++layer)
                        // {
                        //     SaveRenderTextureToFile(yMapsTextures[layer], 0, "Assets/Debug/DepthData" + fileCount + ".txt");
                        //     ++fileCount;
                        // }
                        // SaveIntsToFile(_visibilityOutcome, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
                        for (int i = 0; i < yMapMipmapCount; ++i)
                        {
                            SaveRenderTextureToFile(_backwardWarpingDepthTexture, i, "Assets/Debug/DepthData" + fileCount + ".txt");
                            ++fileCount;
                        }
#endif
#if EVALUATE
                        // evaluate
                        SaveRenderTextureToBin(_backwardWarpingDepthTexture,
                            "Assets/Record/Predict/depthData" + predictCount + ".bin");
                        ++predictCount;
                        SaveRenderTextureToBin(_prevDepthTexture,
                            "Assets/Record/Reference/depthData" + referenceCount + ".bin", true);
                        ++referenceCount;
#endif
                    }
                    else
                    {
                        --skipFrameCount;
                    }
                }
            }
        }

        private void StartRecording()
        {
            Debug.Log("StartRecording");
            _positions.Clear();
            _rotations.Clear();
            _recordTimer = 0.0f;
            _isRecording = true;
            _isReplaying = false;
            _timeStart = DateTime.Now;
        }

        private void StartReplaying()
        {
            Debug.Log("StartReplaying");
            LoadData();
            if (_positions.Count == 0)
            {
                Debug.LogError("No positions found in playbackRoamingPathAsset!");
                return;
            }
            if (_positions.Count != _rotations.Count)
            {
                Debug.LogError("positions and rotations have different size!");
                return;
            }
            _isRecording = false;
            _isReplaying = true;
            _currentReplayFrameIndex = 0;
            _lerpFactor = 0.0f;
        }

        private void Stop()
        {
            Debug.Log("Over!");
            if (_isRecording) SaveRoamingPathData();
            _isRecording = false;
            _isReplaying = false;
            _isFinished = true;
        }
        
        private void SaveRoamingPathData()
        {
            var asset = ScriptableObject.CreateInstance<RoamingPathAsset>();
            asset.poses = new RoamingPathAsset.Pose[_positions.Count];
            for (var i = 0; i < _positions.Count; i++)
            {
                asset.poses[i].position = _positions[i];
                asset.poses[i].rotation = _rotations[i];
            }
            var filePath = $"{SavingDir}/CameraRoamingPath_{_timeStart:yyMMdd-H-mm-ss}.asset";
            AssetDatabase.CreateAsset(asset, filePath);
            Debug.Log("Saving File: " + filePath);
        }
        
        private void LoadData()
        {
            if (!playbackRoamingPathAsset)
            {
                Debug.LogError("PlaybackRoamingPathAsset is Missing!");
                return;
            }
            _positions = playbackRoamingPathAsset.poses.Select(it => it.position).ToList();
            _rotations = playbackRoamingPathAsset.poses.Select(it => it.rotation).ToList();
        }
        
        private float GetScreenDepth(float depth)
        {
            float z = (_farClipPlane * _nearClipPlane) / (_nearClipPlane + depth * (_farClipPlane - _nearClipPlane));
            float ndcZ = -_prevProjectionMatrix[2, 2] + _prevProjectionMatrix[2, 3] / z;
            return ndcZ;
        }
#if EVALUATE
        private void SaveRenderTextureToBin(RenderTexture texture, string filePath, bool convert = false)
        {
            if (texture == null)
            {
                Debug.LogError("RenderTexture is not initialized!");
                return;
            }
            Texture2D tempTexture = new Texture2D(texture.width, texture.height, TextureFormat.RFloat, false);
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = texture;
            tempTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            tempTexture.Apply();
            float[] floatData = tempTexture.GetRawTextureData<float>().ToArray();
            if (convert)
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