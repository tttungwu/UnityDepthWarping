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
        private RenderTexture _depthTexture;
        
        public ComputeShader HiZComputeShader;
        private int _convertDepthToNDCKernel;
        private int _maxMipmapKernel;
        private int _computeVisibilityKernel;
        
        private static readonly int FarClipPlaneShaderPropertyID = Shader.PropertyToID("FarClipPlane");
        private static readonly int NearClipPlaneShaderPropertyID = Shader.PropertyToID("NearClipPlane");
        private static readonly int ProjectionMatrixShaderPropertyID = Shader.PropertyToID("ProjectionMatrix");
        private static readonly int PrevMipmapBufferPropertyID = Shader.PropertyToID("PrevMipmapBuffer");
        private static readonly int CurMipmapBufferPropertyID = Shader.PropertyToID("CurMipmapBuffer");
        private static readonly int MipmapWidthPropertyID = Shader.PropertyToID("MipmapWidth");
        private static readonly int MipmapHeightPropertyID = Shader.PropertyToID("MipmapHeight");
        private static readonly int ObjectNumPropertyID = Shader.PropertyToID("ObjectNum");
        private static readonly int WidthPropertyID = Shader.PropertyToID("Width");
        private static readonly int HeightPropertyID = Shader.PropertyToID("Height");
        private static readonly int BoundsMinPropertyID = Shader.PropertyToID("BoundsMin");
        private static readonly int BoundsMaxPropertyID = Shader.PropertyToID("BoundsMax");
        private static readonly int CurrentProjectionViewMatrixPropertyID = Shader.PropertyToID("CurrentProjectionViewMatrix");
        private static readonly int MipmapBufferPropertyID = Shader.PropertyToID("MipmapBuffer");
        private static readonly int ModelMatrixBufferPropertyID = Shader.PropertyToID("ModelMatrixBuffer");
        private static readonly int CullResultBufferPropertyID = Shader.PropertyToID("CullResultBuffer");
        
        
    }
}