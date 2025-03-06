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
        
        private DepthSaveFeature depthSaveFeature;
        private RenderTexture prevDepthTexture;
        private RenderTexture forwardWarpingDepthTexture;
        private RenderTexture backwardWarpingDepthTexture;
        private RenderTexture motionVectorsTexture;
        private RenderTexture[] yMapsTextures;
        private int yMapsSize = Mathf.FloorToInt(Mathf.Log(Mathf.Min(Screen.width, Screen.height), 2.0f)) + 1;
        private ComputeBuffer boundingBoxesBuffer;
        private ComputeBuffer visibilityBuffer;
        private int[] visibilityOutcome;
        private int objectNum;
        private Matrix4x4 prevProjectionMatrix, prevViewMatrix;
        private int skipFrameCount = 200;
        
#if DEBUGPRINT
        private int fileCount = 0;
#endif
#if EVALUATE
        private int predictCount = 0;
        private int referenceCount = 0;
#endif
        
        public ComputeShader IDWComputeShader;
        private int motionVectorKernel;
        private int mipmapKernel;
        private int backwardKernel;
        private int nBufferKernel;
        private int computeVisibilityKernel;
        
        public int seedNum = 8;
        public int maxBoundIter = 3;
        public int maxSearchIter = 3;
        public float threshold = 0.1f;

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
                            depthSaveFeature = (DepthSaveFeature)feature;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("Depth save feature not found");
            }
            forwardWarpingDepthTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            forwardWarpingDepthTexture.enableRandomWrite = true;
            forwardWarpingDepthTexture.Create();
            motionVectorsTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            motionVectorsTexture.enableRandomWrite = true;
            motionVectorsTexture.useMipMap = true;
            motionVectorsTexture.autoGenerateMips = false;
            motionVectorsTexture.Create();
            yMapsTextures = new RenderTexture[yMapsSize];
            for (int i = 0; i < yMapsSize; ++i)
            {
                yMapsTextures[i] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat);
                yMapsTextures[i].enableRandomWrite = true;
                yMapsTextures[i].Create();
            }
            backwardWarpingDepthTexture = yMapsTextures[0];

            Occludee[] occludees = FindObjectsOfType<Occludee>();
            objectNum = occludees.Length;
            Vector3[] boundingBoxesData = new Vector3[objectNum * 2];
            for (int i = 0; i < objectNum; ++ i)
            {
                Bounds bounds = occludees[i].GetBounds();
                boundingBoxesData[i * 2] = bounds.min;
                boundingBoxesData[i * 2 + 1] = bounds.max;
            }
            boundingBoxesBuffer = new ComputeBuffer(objectNum, sizeof(float) * 3 * 2);
            boundingBoxesBuffer.SetData(boundingBoxesData);
            visibilityOutcome = new int[objectNum];
            visibilityBuffer = new ComputeBuffer(objectNum, sizeof(int));
            visibilityBuffer.SetData(visibilityOutcome);
            Debug.Log($"bounding box buffer is created for {objectNum}");
            
            debugTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            debugTexture.enableRandomWrite = true;
            debugTexture.Create();
            
            motionVectorKernel = IDWComputeShader.FindKernel("GetMotionVector");
            mipmapKernel = IDWComputeShader.FindKernel("GenerateMipmap");
            backwardKernel = IDWComputeShader.FindKernel("BackwardSearch");
            nBufferKernel = IDWComputeShader.FindKernel("GenerateNBuffer");
            computeVisibilityKernel = IDWComputeShader.FindKernel("ComputeVisibility");
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
                        prevDepthTexture = depthSaveFeature.GetDepthTexture();
                        prevViewMatrix = _camera.worldToCameraMatrix;
                        prevProjectionMatrix = _camera.projectionMatrix;
                    }
                    transform.position = Vector3.Lerp(_positions[_currentReplayFrameIndex], _positions[_currentReplayFrameIndex + 1], _lerpFactor);
                    transform.rotation = Quaternion.Slerp(_rotations[_currentReplayFrameIndex], _rotations[_currentReplayFrameIndex + 1], _lerpFactor);
                    if (skipFrameCount == 0)
                    {
                        // get motion vector and forward depth
                        IDWComputeShader.SetTexture(motionVectorKernel, "ForwardWarpingDepthTexture", forwardWarpingDepthTexture);
                        IDWComputeShader.SetTexture(motionVectorKernel, "MotionVector", motionVectorsTexture);
                        IDWComputeShader.SetTexture(motionVectorKernel, "PrevDepthTexture", prevDepthTexture);
                        IDWComputeShader.SetMatrix("PrevProjectionMatrix", prevProjectionMatrix);
                        IDWComputeShader.SetMatrix("InversedPrevProjectionViewMatrix", (prevProjectionMatrix * prevViewMatrix).inverse);
                        IDWComputeShader.SetMatrix("CurrentProjectionViewMatrix", _camera.projectionMatrix * _camera.worldToCameraMatrix);
                        IDWComputeShader.SetFloat("FarClipPlane", _camera.farClipPlane);
                        IDWComputeShader.SetFloat("NearClipPlane", _camera.nearClipPlane);
                        IDWComputeShader.Dispatch(motionVectorKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                        // get motion vector mipmap
                        int currentWidth = Screen.width;
                        int currentHeight = Screen.height;
                        int level = 0;
                        while (currentWidth > 1 || currentHeight > 1)
                        {
                            int nextWidth = Mathf.Max(1, currentWidth / 2);
                            int nextHeight = Mathf.Max(1, currentHeight / 2);
                            IDWComputeShader.SetInt("SrcLevel", level);
                            IDWComputeShader.SetTexture(mipmapKernel, "MotionTextureSrc", motionVectorsTexture, level);
                            IDWComputeShader.SetTexture(mipmapKernel, "MotionTextureDst", motionVectorsTexture, level + 1);
                            IDWComputeShader.Dispatch(mipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                            currentWidth = nextWidth;
                            currentHeight = nextHeight;
                            ++ level;
                        }
                        // backward search
                        IDWComputeShader.SetTexture(backwardKernel, "MotionVectorAndPredictedDepthTexture", forwardWarpingDepthTexture);
                        IDWComputeShader.SetTexture(backwardKernel, "BackwardWarpingDepthTexture", backwardWarpingDepthTexture);
                        IDWComputeShader.SetTexture(backwardKernel, "MipmapMotionVectorsTexture", motionVectorsTexture);
                        IDWComputeShader.SetTexture(backwardKernel, "DebugTexture", debugTexture);
                        IDWComputeShader.SetInt("MaxMipmapLevel", level);
                        IDWComputeShader.SetInt("Width", Screen.width);
                        IDWComputeShader.SetInt("Height", Screen.height);
                        IDWComputeShader.SetInt("MaxBoundIter", maxBoundIter);
                        IDWComputeShader.SetInt("SeedNum", seedNum);
                        IDWComputeShader.SetInt("MaxSearchIter", maxSearchIter);
                        IDWComputeShader.SetFloat("Threshold", threshold);
                        IDWComputeShader.Dispatch(backwardKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                        // generate N-Buffer
                        for (int layer = 1; layer < yMapsSize; ++layer)
                        {
                            IDWComputeShader.SetInt("Layer", layer);
                            IDWComputeShader.SetTexture(nBufferKernel, "PrevNBuffer", yMapsTextures[layer - 1]);
                            IDWComputeShader.SetTexture(nBufferKernel, "CurNBuffer", yMapsTextures[layer]);
                            IDWComputeShader.Dispatch(nBufferKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                        }
                        // cull object
                        IDWComputeShader.SetBuffer(computeVisibilityKernel, "BoundingBoxes", boundingBoxesBuffer);
                        IDWComputeShader.SetBuffer(computeVisibilityKernel, "Visibility", visibilityBuffer);
                        IDWComputeShader.SetMatrix("CurrentProjectionViewMatrix", _camera.projectionMatrix * _camera.worldToCameraMatrix);
                        IDWComputeShader.SetInt("ObjectNum", objectNum);
                        IDWComputeShader.Dispatch(computeVisibilityKernel, (objectNum + 63) / 64, 1, 1);
                        visibilityBuffer.GetData(visibilityOutcome);
                        for (int i = 0; i < visibilityOutcome.Length; i++)
                            Debug.Log(visibilityOutcome[i]);
                        Debug.Log("---------------");
#if EVALUATE
                        // evaluate
                        SaveRenderTextureToBin(yMapsTextures[0],
                            "Assets/Record/Predict/depthData" + predictCount + ".bin");
                        ++predictCount;
                        SaveRenderTextureToBin(prevDepthTexture,
                            "Assets/Record/Reference/depthData" + referenceCount + ".bin", true);
                        ++referenceCount;
#endif
#if DEBUGPRINT
                        // // debug
                        // SaveRenderTextureToFile(forwardWarpingDepthTexture, 0, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
                        // for (int i = 0; i <= level; ++i)
                        // {
                        //     SaveRenderTextureToFile(motionVectorsTexture, i, "Assets/Debug/DepthData" + fileCount + ".txt");
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
                        SaveIntsToFile(visibilityOutcome, "Assets/Debug/DepthData" + fileCount + ".txt");
                        ++fileCount;
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
        
        private void OnGUI()
        {
            if (!enableUI) return;
            if (_isRecording) GUI.Box(new Rect(10, 10, 100, 30), "Recording...");
            if (_isReplaying) GUI.Box(new Rect(10, 10, 100, 30), "Replaying...");
        }

        private float GetScreenDepth(float depth)
        {
            float z = (_farClipPlane * _nearClipPlane) / (_nearClipPlane + depth * (_farClipPlane - _nearClipPlane));
            float ndcZ = -prevProjectionMatrix[2, 2] + prevProjectionMatrix[2, 3] / z;
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
            sb.AppendLine($"RenderTexture Size: {prevDepthTexture.width}x{prevDepthTexture.height}");
            for (int y = 0; y < prevDepthTexture.height; y++)
            {
                for (int x = 0; x < prevDepthTexture.width; x++)
                {
                    int index = y * prevDepthTexture.width + x;
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
            sb.AppendLine($"RenderTexture Size: {prevDepthTexture.width}x{prevDepthTexture.height}");
            for (int y = 0; y < prevDepthTexture.height; y++)
            {
                for (int x = 0; x < prevDepthTexture.width; x++)
                {
                    int index = y * prevDepthTexture.width + x;
                    sb.AppendLine($"[{x}, {y}]: {ans[index]}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Depth data saved to {filePath}");
        }
#endif
    }
}