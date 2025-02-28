using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CameraRecorder
{
    [RequireComponent(typeof(Camera))]
    public class CameraRecorder : MonoBehaviour
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

        void Start()
        {
            _camera = GetComponent<Camera>();
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
                    transform.position = Vector3.Lerp(_positions[_currentReplayFrameIndex], _positions[_currentReplayFrameIndex + 1], _lerpFactor);
                    transform.rotation = Quaternion.Slerp(_rotations[_currentReplayFrameIndex], _rotations[_currentReplayFrameIndex + 1], _lerpFactor);
                }
            }
        }

        void StartRecording()
        {
            Debug.Log("StartRecording");
            _positions.Clear();
            _rotations.Clear();
            _recordTimer = 0.0f;
            _isRecording = true;
            _isReplaying = false;
            _timeStart = DateTime.Now;
        }

        void StartReplaying()
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

        void Stop()
        {
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
    }
}
