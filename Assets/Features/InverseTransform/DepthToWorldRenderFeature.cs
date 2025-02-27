using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthToWorldRenderFeature : ScriptableRendererFeature
{
    class DepthToWorldRenderPass : ScriptableRenderPass
    {
        private ComputeShader depthToWorldShader;
        private RenderTexture worldPosTexture;
        private RTHandle depthTextureHandle;
        private int fileCount = 0;

        public DepthToWorldRenderPass(ComputeShader vDepthToWorldShader)
        {
            depthToWorldShader = vDepthToWorldShader;
            worldPosTexture = null;
            // todo: choose another RenderPassEvent
            renderPassEvent = RenderPassEvent.AfterRendering;
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (worldPosTexture == null)
            {
                worldPosTexture = new RenderTexture(renderingData.cameraData.cameraTargetDescriptor.width, 
                    renderingData.cameraData.cameraTargetDescriptor.height, 
                    0, RenderTextureFormat.ARGBFloat);
                worldPosTexture.enableRandomWrite = true;
                worldPosTexture.Create();
            }
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("DepthToWorldSpace");
            
            int kernel = depthToWorldShader.FindKernel("DepthToWorldSpace");
            depthTextureHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            // ValidateDepthTexture(depthTextureHandle, 
            //                     renderingData.cameraData.cameraTargetDescriptor.width, 
            //                     renderingData.cameraData.cameraTargetDescriptor.height,
            //                     "Assets/Debug/DepthData" + fileCount + ".txt");
            cmd.SetComputeTextureParam(depthToWorldShader, kernel, "_DepthTexture", depthTextureHandle);
            cmd.SetComputeTextureParam(depthToWorldShader, kernel, "_WorldPosTexture", worldPosTexture);
            
            Matrix4x4 viewProjMatrix = renderingData.cameraData.camera.projectionMatrix *
                                       renderingData.cameraData.camera.worldToCameraMatrix;
            cmd.SetComputeMatrixParam(depthToWorldShader, "_InverseViewProjectionMatrix", viewProjMatrix.inverse);

            uint threadX, threadY, threadZ;
            depthToWorldShader.GetKernelThreadGroupSizes(kernel, out threadX, out threadY, out threadZ);
            int groupX = Mathf.CeilToInt(renderingData.cameraData.cameraTargetDescriptor.width / (float)threadX);
            int groupY = Mathf.CeilToInt(renderingData.cameraData.cameraTargetDescriptor.height / (float)threadY);
            cmd.DispatchCompute(depthToWorldShader, kernel, groupX, groupY, 1);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        private void ValidateDepthTexture(RTHandle depthTextureHandle, int width, int height, string filePath)
        {
            RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RFloat);
            Graphics.Blit(depthTextureHandle, tempRT);
            
            RenderTexture.active = tempRT;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RFloat, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            float[] depthValues = texture.GetRawTextureData<float>().ToArray();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"RenderTexture Size: {width}x{height}");
            sb.AppendLine("Depth Values:");

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float depth = depthValues[index];
                    sb.AppendLine($"[{x}, {y}]: {depth}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Depth data saved to {filePath}");

            ++fileCount;

            Object.Destroy(texture);
            RenderTexture.ReleaseTemporary(tempRT);
            RenderTexture.active = null;
        }

        public RenderTexture GetWorldPosTexture()
        {
            return worldPosTexture;
        }

        public RTHandle GetDepthTexture()
        {
            return depthTextureHandle;
        }
    }
    
    public ComputeShader depthToWorldShader;
    private DepthToWorldRenderPass renderPass;

    public override void Create()
    {
        renderPass = new DepthToWorldRenderPass(depthToWorldShader);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(renderPass);
    }
    
    public RenderTexture GetWorldPosTexture()
    {
        return renderPass?.GetWorldPosTexture();
    }

    public RTHandle GetDepthTexture()
    {
        return renderPass?.GetDepthTexture();
    }
}
