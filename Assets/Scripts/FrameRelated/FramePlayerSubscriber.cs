using UnityEngine;

namespace FrameRelated
{
    public abstract class FramePlayerSubscriber : MonoBehaviour
    {
        public virtual void OnPlayFrame(int frameId) {}
        
        public virtual void OnBeforeFirstFrame(int frameId) {}

        public virtual void OnAfterLastFrame(int frameId) {}
    }
}