using System;
using UnityEngine;

namespace CameraRecorder
{
    public class RoamingPathAsset : ScriptableObject
    {
        [Serializable]
        public struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
        }
        
        public Pose[] poses;
    }
}