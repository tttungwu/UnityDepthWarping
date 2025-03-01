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
        private Matrix4x4 prevProjectionViewMatrix;
        private int skipFrameCount = 5;
        private int fileCount = 0;

        void Start()
        {
            _camera = GetComponent<Camera>();
            _camera.depthTextureMode = DepthTextureMode.Depth;
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
                        prevProjectionViewMatrix = _camera.projectionMatrix * _camera.worldToCameraMatrix;
                    }
                    transform.position = Vector3.Lerp(_positions[_currentReplayFrameIndex], _positions[_currentReplayFrameIndex + 1], _lerpFactor);
                    transform.rotation = Quaternion.Slerp(_rotations[_currentReplayFrameIndex], _rotations[_currentReplayFrameIndex + 1], _lerpFactor);
                    if (skipFrameCount == 0)
                    {
                        
                    }
                }
            }

            if (skipFrameCount > 0)
            {
                --skipFrameCount;
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
        
        private void SaveRenderTextureToFile(RenderTexture texture, string filePath = "Assets/Debug/DepthData.txt")
        {
            if (!texture)
            {
                Debug.LogError("RenderTexture is null!");
                return;
            }

            Texture2D tempTexture = new Texture2D(texture.width, texture.height, TextureFormat.RFloat, false);

            RenderTexture.active = texture;
            tempTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            tempTexture.Apply();

            Color[] pixels = tempTexture.GetPixels();

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"RenderTexture Size: {texture.width}x{texture.height}");
            sb.AppendLine("Depth Values:");

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    int index = y * texture.width + x;
                    float depth = pixels[index].r;
                    sb.AppendLine($"[{x}, {y}]: {depth}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Depth data saved to {filePath}");

            Destroy(tempTexture);
        }
    }
}