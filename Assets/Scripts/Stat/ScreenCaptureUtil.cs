using UnityEngine;

namespace Stat
{
    public class ScreenCaptureUtil : MonoBehaviour
    {
        private enum CaptureType
        {
            Reference,
            Predict
        };
        
        private int frameIndex = 0;
        [SerializeField] private CaptureType captureType = CaptureType.Reference;
        private string savePath = "Assets/Record/";
        

        void Start()
        {
            var allCanvases = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in allCanvases)
                if (canvas != null && canvas.enabled) canvas.enabled = false;
        }
        
        void Update()
        {
            string filePathPrefix;
            if (captureType == CaptureType.Reference) filePathPrefix = "/Reference/screen/frame_";
            else filePathPrefix = "/Predict/screen/frame_";
            string fileName = $"{savePath}{filePathPrefix}{frameIndex.ToString("D5")}.png";
            ScreenCapture.CaptureScreenshot(fileName);
            frameIndex++;
            Debug.Log($"Saved frame to: {fileName}");
        }
    }
}