using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthSaveFeature : ScriptableRendererFeature
{
    class DepthSavePass : ScriptableRenderPass
    {
        private RenderTexture depthTexture;

        public DepthSavePass()
        {
            depthTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat);
            renderPassEvent = RenderPassEvent.AfterRendering;
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            CommandBuffer cmd = CommandBufferPool.Get("DepthSave");
            cmd.Blit(Shader.GetGlobalTexture("_CameraDepthTexture"), depthTexture);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public RenderTexture GetDepthTexture()
        {
            return depthTexture;
        }
    }

    private DepthSavePass renderPass;
    
    public override void Create()
    {
        renderPass = new DepthSavePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera.name == "SceneCamera") return;
        renderer.EnqueuePass(renderPass);
    }
    
    public RenderTexture GetDepthTexture()
    {
        return renderPass?.GetDepthTexture();
    }
}
