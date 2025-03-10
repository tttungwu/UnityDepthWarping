using UnityEngine;

namespace CameraRecorder
{
    [RequireComponent(typeof(Camera))]
    public class FrameReplayer : MonoBehaviour
    {
        [SerializeField] private RoamingPathAsset roamingPathAsset;
        [SerializeField] private int frameId = -1;
        [SerializeField] private FramePlayerSubscriber[] framePlayerSubscribers;
        [SerializeField] private bool autoPlay;

        private Camera _camera;
        
        public int FrameId => frameId;
        public bool PlayDone { get; private set; }
        
        private void Start()
        {
            _camera = GetComponent<Camera>();
            if (!roamingPathAsset) Debug.LogWarning("roamingPathAsset is null");
        }
        
        private void OnDestroy()
        {
            if (frameId == -1) NotifyEnd();
        }
        
        private void LateUpdate()
        {
            if (autoPlay && !PlayDone) PlayNextFrame();
        }
        
        public void PlayNextFrame()
        {
            if (frameId == -1) NotifyStart();
            ++ frameId;
            if (frameId >= roamingPathAsset.poses.Length)
            {
                frameId = roamingPathAsset.poses.Length - 1;
                NotifyEnd();
                PlayDone = true;
                return;
            }
            SetupFrame();
        }
        
        public void PlayPreviousFrame()
        {
            frameId--;
            if (frameId < 0)
            {
                frameId = 0;
                return;
            }
            SetupFrame();
        }
        
        private void SetupFrame()
        {
            ApplyCameraPose();
            if (framePlayerSubscribers == null) return;
            foreach (var subscriber in framePlayerSubscribers)
            {
                if (!subscriber || !subscriber.enabled) continue;
                subscriber.OnPlayFrame(frameId);
            }
        }

        private void NotifyStart()
        {
            if (framePlayerSubscribers == null) return;
            foreach (var subscriber in framePlayerSubscribers)
            {
                if (!subscriber || !subscriber.enabled) continue;
                subscriber.OnBeforeFirstFrame(frameId);
            }
        }

        private void NotifyEnd()
        {
            if (framePlayerSubscribers == null) return;
            foreach (var subscriber in framePlayerSubscribers)
            {
                if (!subscriber || !subscriber.enabled) continue;
                subscriber.OnAfterLastFrame(frameId);
            }
        }

        private void ApplyCameraPose()
        {
            _camera.transform.position = roamingPathAsset.poses[frameId].position;
            _camera.transform.rotation = roamingPathAsset.poses[frameId].rotation;
        }
    }
}