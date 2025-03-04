using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class OcclusionCulling : MonoBehaviour
{
    private Camera cam;
    private Matrix4x4 prevViewMatrix;
    private Matrix4x4 prevProjectionMatrix;

    private DepthSaveFeature depthSaveFeature;
    private RenderTexture prevDepthTexture;

    private int fileCount = 0;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.depthTextureMode = DepthTextureMode.Depth;
        var proInfo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (proInfo != null)
        {
            var rendererDataList = (ScriptableRendererData[])proInfo.GetValue(UniversalRenderPipeline.asset);
            foreach (var rendererData in rendererDataList)
            {
                foreach (var feature in rendererData.rendererFeatures)
                {
                    if (feature is DepthSaveFeature)
                    {
                        depthSaveFeature = (DepthSaveFeature)feature;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("Depth save feature not found");
        }
    }

    void Update()
    {
        prevDepthTexture = depthSaveFeature.GetDepthTexture();
        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveRenderTextureToFile("Assets/Debug/DepthData" + fileCount + ".txt");
            ++fileCount;
        }

        prevViewMatrix = cam.worldToCameraMatrix;
        prevProjectionMatrix = cam.projectionMatrix;
    }
    
    public void SaveRenderTextureToFile(string filePath = "Assets/Debug/DepthData.txt")
    {
        if (prevDepthTexture == null)
        {
            Debug.LogError("RenderTexture is null!");
            return;
        }

        Texture2D tempTexture = new Texture2D(prevDepthTexture.width, prevDepthTexture.height, TextureFormat.RFloat, false);

        RenderTexture.active = prevDepthTexture;
        tempTexture.ReadPixels(new Rect(0, 0, prevDepthTexture.width, prevDepthTexture.height), 0, 0);
        tempTexture.Apply();

        Color[] pixels = tempTexture.GetPixels();

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"RenderTexture Size: {prevDepthTexture.width}x{prevDepthTexture.height}");
        sb.AppendLine("Depth Values:");

        for (int y = 0; y < prevDepthTexture.height; y++)
        {
            for (int x = 0; x < prevDepthTexture.width; x++)
            {
                int index = y * prevDepthTexture.width + x;
                float depth = pixels[index].r;
                sb.AppendLine($"[{x}, {y}]: {depth}");
            }
        }

        File.WriteAllText(filePath, sb.ToString());
        Debug.Log($"Depth data saved to {filePath}");

        Destroy(tempTexture);
    }
}