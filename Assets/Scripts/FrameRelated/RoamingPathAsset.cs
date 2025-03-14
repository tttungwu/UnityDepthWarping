using System;
using UnityEngine;

namespace FrameRelated
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