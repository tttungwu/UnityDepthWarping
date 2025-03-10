using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CameraRecorder
{
    [RequireComponent(typeof(Camera))]
    public class FrameRecorder : MonoBehaviour
    {
        private Camera _camera;
        public float recordInterval = 0.1f;
        private float _recordTimer = 0.0f;
        private bool _isRecording = false;
        
        private List<Vector3> _positions = new ();
        private List<Quaternion> _rotations = new ();
        
        private DateTime _timeStart;
        private const string SavingDir = "Assets/Prefabs/CameraRoamingPath/";

        void Start()
        {
            _camera = GetComponent<Camera>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartRecording();
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
        }

        private void StartRecording()
        {
            Debug.Log("StartRecording");
            _positions.Clear();
            _rotations.Clear();
            _recordTimer = 0.0f;
            _isRecording = true;
            _timeStart = DateTime.Now;
        }

        private void Stop()
        {
            if (_isRecording) SaveRoamingPathData();
            _isRecording = false;
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
    }
}
