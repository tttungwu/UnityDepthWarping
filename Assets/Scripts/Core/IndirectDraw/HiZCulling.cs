using Features;
using UnityEngine;

namespace Core.IndirectDraw
{
    public class HiZCulling : CullingMethod
    {
        private Camera _camera;
        private float _nearClipPlane, _farClipPlane;
        private Mesh _mesh;
        
        private DepthSaveFeature _depthSaveFeature;
        private RenderTexture _prevDepthTexture;
        
        public ComputeShader HiZComputeShader;
        private int _convertDepthToNDCKernel;
        private int _maxMipmapKernel;
        private int _computeVisibilityKernel;
        
    }
}