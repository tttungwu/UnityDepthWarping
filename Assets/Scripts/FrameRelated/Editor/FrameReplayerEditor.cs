using UnityEditor;
using UnityEngine;

namespace FrameRelated.Editor
{
    [CustomEditor(typeof(FrameReplayer))]
    public class FrameReplayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            var frameReplayer = (FrameReplayer)target;
            if (!EditorApplication.isPlaying) return;
            if (GUILayout.Button("Next Frame")) frameReplayer.PlayNextFrame();
            if (GUILayout.Button("Previous Frame")) frameReplayer.PlayPreviousFrame();
        }
    }
}