using UnityEngine;

namespace Core.IndirectDraw
{
    public abstract class CullingMethod : MonoBehaviour
    {
        public virtual void Init(Camera cam, Mesh mesh, Matrix4x4[] matrices)
        {
        
        }
        
        public virtual void Cull(Matrix4x4[] matrices = null, ComputeBuffer cullResultBuffer = null)
        {
        
        }
        
        public virtual Matrix4x4[] GetVisibleMatrices()
        {
            return null;
        }

        public virtual Matrix4x4[] GetInvisibleMatrices()
        {
            return null;
        }
    }
}