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
        private RenderTexture predictedDepthTexture;
        private RenderTexture motionVectorsTexture;
        private Matrix4x4 prevProjectionMatrix, prevViewMatrix;
        private int skipFrameCount = 200;
        private int fileCount = 0;
        
        public ComputeShader motionVectorComputeShader;
        public int motionVectorKernel;
        public int mipmapKernel;

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
            predictedDepthTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            predictedDepthTexture.enableRandomWrite = true;
            predictedDepthTexture.Create();
            motionVectorsTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            motionVectorsTexture.enableRandomWrite = true;
            motionVectorsTexture.useMipMap = true;
            motionVectorsTexture.autoGenerateMips = false;
            motionVectorsTexture.Create();
            
            motionVectorKernel = motionVectorComputeShader.FindKernel("CSMain");
            mipmapKernel = motionVectorComputeShader.FindKernel("GenerateMipmap");
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
                        // SaveRenderTextureToFile(prevDepthTexture, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
                        prevViewMatrix = _camera.worldToCameraMatrix;
                        prevProjectionMatrix = _camera.projectionMatrix;
                    }
                    transform.position = Vector3.Lerp(_positions[_currentReplayFrameIndex], _positions[_currentReplayFrameIndex + 1], _lerpFactor);
                    transform.rotation = Quaternion.Slerp(_rotations[_currentReplayFrameIndex], _rotations[_currentReplayFrameIndex + 1], _lerpFactor);
                    if (skipFrameCount == 0)
                    {
                        motionVectorComputeShader.SetTexture(motionVectorKernel, "Result", predictedDepthTexture);
                        motionVectorComputeShader.SetTexture(motionVectorKernel, "MotionVector", motionVectorsTexture);
                        motionVectorComputeShader.SetTexture(motionVectorKernel, "PrevDepthTexture", prevDepthTexture);
                        motionVectorComputeShader.SetMatrix("PrevProjectionMatrix", prevProjectionMatrix);
                        motionVectorComputeShader.SetMatrix("InversedPrevProjectionViewMatrix", (prevProjectionMatrix * prevViewMatrix).inverse);
                        motionVectorComputeShader.SetMatrix("CurrentProjectionViewMatrix", _camera.projectionMatrix * _camera.worldToCameraMatrix);
                        motionVectorComputeShader.SetFloat("FarClipPlane", _camera.farClipPlane);
                        motionVectorComputeShader.SetFloat("NearClipPlane", _camera.nearClipPlane);
                        motionVectorComputeShader.Dispatch(motionVectorKernel, (Screen.width + 7) / 8, (Screen.height + 7) / 8, 1);
                        
                        int currentWidth = Screen.width;
                        int currentHeight = Screen.height;
                        int level = 0;
                        while (currentWidth > 1 || currentHeight > 1)
                        {
                            int nextWidth = Mathf.Max(1, currentWidth / 2);
                            int nextHeight = Mathf.Max(1, currentHeight / 2);
                            motionVectorComputeShader.SetInt("SrcLevel", level);
                            motionVectorComputeShader.SetTexture(mipmapKernel, "MotionTextureSrc", motionVectorsTexture, level);
                            motionVectorComputeShader.SetTexture(mipmapKernel, "MotionTextureDst", motionVectorsTexture, level + 1);
                            motionVectorComputeShader.Dispatch(mipmapKernel, (nextWidth + 7) / 8, (nextHeight + 7) / 8, 1);
                            currentWidth = nextWidth;
                            currentHeight = nextHeight;
                            level++;
                        }
                        
                        SaveRenderTextureToFile(predictedDepthTexture, 0, "Assets/Debug/DepthData" + fileCount + ".txt");
                        ++fileCount;
                        for (int i = 0; i <= level; ++i)
                        {
                            SaveRenderTextureToFile(motionVectorsTexture, i, "Assets/Debug/DepthData" + fileCount + ".txt");
                            ++fileCount;
                        }

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
                        //         float depth = getScreenDepth(pixels[index].r);
                        //         Vector4 ndcPos = new Vector4(2.0f * x / prevDepthTexture.width - 1,
                        //             2.0f * y / prevDepthTexture.height - 1, depth, 1.0f);
                        //         Vector4 worldPos = (prevProjectionMatrix * prevViewMatrix).inverse * ndcPos;
                        //         worldPos /= worldPos.w;
                        //         Vector4 newNDCPos = _camera.projectionMatrix * _camera.worldToCameraMatrix * worldPos;
                        //         newNDCPos /= newNDCPos.w;
                        //         debugDepth[index].r = (newNDCPos.x * prevDepthTexture.width + prevDepthTexture.width) / 2.0f;
                        //         debugDepth[index].g = (newNDCPos.y * prevDepthTexture.height + prevDepthTexture.height) / 2.0f;
                        //         debugDepth[index].b = newNDCPos.z;
                        //         debugDepth[index].a = newNDCPos.w;
                        //         // debugDepth[index] = depth;
                        //     }
                        // }
                        // SaveColorsToFile(debugDepth, "Assets/Debug/DepthData" + fileCount + ".txt");
                        // ++fileCount;
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

        private float getScreenDepth(float depth)
        {
            float z = (_farClipPlane * _nearClipPlane) / (_nearClipPlane + depth * (_farClipPlane - _nearClipPlane));
            float ndcZ = -prevProjectionMatrix[2, 2] + prevProjectionMatrix[2, 3] / z;
            return ndcZ;
        }
        
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
            sb.AppendLine("Depth Values:");
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
            sb.AppendLine("Depth Values:");
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
        
        private void SaveColorsToFile(Color[] ans, string filePath = "Assets/Debug/DepthData.txt")
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"RenderTexture Size: {prevDepthTexture.width}x{prevDepthTexture.height}");
            sb.AppendLine("Depth Values:");
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
    }
}