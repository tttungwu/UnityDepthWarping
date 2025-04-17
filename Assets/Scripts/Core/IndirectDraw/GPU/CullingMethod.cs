using UnityEngine;

namespace Core.IndirectDraw.GPU
{
    public class CullingMethod : MonoBehaviour
    {
        public virtual void Init(Camera cam, Mesh mesh, Matrix4x4[] matrices)
        {
        
        }
        
        public virtual void Cull(ComputeBuffer cullResultBuffer, Matrix4x4[] matrices = null)
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