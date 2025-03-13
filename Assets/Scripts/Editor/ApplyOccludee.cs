using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


public class ApplyOccludee : MonoBehaviour
{
    [MenuItem("Tools/Occludee/Apply Occludee Script To All Objects")]
    public static void AddOccludee()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var gameObjectsWithRenderers = FindObjectsOfType<Renderer>()
            .Select(r => r.gameObject)
            .Distinct();
        int count = 0;
        foreach (var go in gameObjectsWithRenderers)
        {
            if (!go.GetComponent<Occludee>())
            {
                go.AddComponent<Occludee>();
                count++;
            }
        }
        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Added Occludee to {count} game objects. Scene '{scene.name}' has been saved.");
        }
        else
        {
            Debug.Log("No Occludee components were added. Scene remains unchanged.");
        }
    }
    
    [MenuItem("Tools/Occludee/Remove All Occludee Script")]
    public static void RemoveOccludee()
    {
        var scene = EditorSceneManager.GetActiveScene();

        var gameObjectsWithOccludee = FindObjectsOfType<Occludee>()
            .Select(o => o.gameObject)
            .Distinct();

        int count = 0;
        foreach (var go in gameObjectsWithOccludee)
        {
            var occludee = go.GetComponent<Occludee>();
            if (occludee != null)
            {
                Object.DestroyImmediate(occludee);
                count++;
            }
        }
        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Removed {count} Occludee components. Scene '{scene.name}' has been saved.");
        }
        else
        {
            Debug.Log("No Occludee components found in the scene. Scene remains unchanged.");
        }
    }
}
