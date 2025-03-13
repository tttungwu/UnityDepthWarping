using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class RemoveOverlappingOccludeeObjects : MonoBehaviour
{
    [MenuItem("Tools/Overlap/Remove Overlapping Occludee Objects")]
    public static void RemoveOverlapsAndSave()
    {
        UnityEngine.SceneManagement.Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!currentScene.isLoaded)
        {
            Debug.LogError("No active scene loaded!");
            return;
        }

        Occludee[] occludees = FindObjectsOfType<Occludee>();
        if (occludees.Length == 0)
        {
            Debug.LogWarning("No objects with Occludee component found in the scene.");
            return;
        }

        // 存储物体、包围盒、冲突次数和位置信息
        List<(GameObject obj, Bounds bounds, int overlapCount, Vector3 position, string name)> objectsWithBounds = 
            new List<(GameObject, Bounds, int, Vector3, string)>();
        foreach (var occludee in occludees)
        {
            Renderer renderer = occludee.gameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"GameObject '{occludee.gameObject.name}' has Occludee but no Renderer. Skipping.");
                continue;
            }
            objectsWithBounds.Add((occludee.gameObject, renderer.bounds, 0, occludee.transform.position, occludee.gameObject.name));
        }

        if (objectsWithBounds.Count == 0)
        {
            Debug.LogWarning("No Occludee objects with Renderer found in the scene.");
            return;
        }

        // 计算重叠次数
        for (int i = 0; i < objectsWithBounds.Count - 1; ++i)
        {
            for (int j = i + 1; j < objectsWithBounds.Count; ++j)
            {
                if (objectsWithBounds[i].bounds.Intersects(objectsWithBounds[j].bounds))
                {
                    objectsWithBounds[i] = (objectsWithBounds[i].obj, objectsWithBounds[i].bounds, 
                                           objectsWithBounds[i].overlapCount + 1, objectsWithBounds[i].position, objectsWithBounds[i].name);
                    objectsWithBounds[j] = (objectsWithBounds[j].obj, objectsWithBounds[j].bounds, 
                                           objectsWithBounds[j].overlapCount + 1, objectsWithBounds[j].position, objectsWithBounds[j].name);
                }
            }
        }

        // 按冲突次数、位置和名称排序，确保确定性
        objectsWithBounds.Sort((a, b) =>
        {
            int compareByOverlap = a.overlapCount.CompareTo(b.overlapCount);
            if (compareByOverlap != 0)
                return compareByOverlap;
            
            int compareByX = a.position.x.CompareTo(b.position.x);
            if (compareByX != 0)
                return compareByX;
            int compareByY = a.position.y.CompareTo(b.position.y);
            if (compareByY != 0)
                return compareByY;
            int compareByZ = a.position.z.CompareTo(b.position.z);
            if (compareByZ != 0)
                return compareByZ;
            
            return a.name.CompareTo(b.name);
        });

        List<GameObject> toKeep = new List<GameObject>();
        foreach (var (obj, bounds, overlapCount, position, name) in objectsWithBounds)
        {
            bool overlaps = false;
            foreach (var keptObj in toKeep)
            {
                Bounds keptBounds = keptObj.GetComponent<Renderer>().bounds;
                if (bounds.Intersects(keptBounds))
                {
                    overlaps = true;
                    break;
                }
            }
            if (!overlaps) toKeep.Add(obj);
        }

        HashSet<GameObject> allObjects = new HashSet<GameObject>(objectsWithBounds.Select(x => x.obj));
        HashSet<GameObject> toDelete = new HashSet<GameObject>(allObjects.Except(toKeep));
        int deletedCount = 0;
        foreach (var obj in toDelete)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
                ++deletedCount;
            }
        }

        if (deletedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(currentScene);
            bool saved = EditorSceneManager.SaveScene(currentScene);
            if (saved) Debug.Log($"Removed {deletedCount} overlapping Occludee objects, retained {toKeep.Count}, saved scene: {currentScene.path}");
            else Debug.LogError("Failed to save scene!");
        }
        else Debug.Log("No overlapping Occludee objects found. Scene unchanged.");
    }

    [MenuItem("Tools/Overlap/Check Overlapping Occludee Objects")]
    public static void CheckOverlappingOccludeeObjects()
    {
        UnityEngine.SceneManagement.Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!currentScene.isLoaded)
        {
            Debug.LogError("No active scene loaded!");
            return;
        }

        Occludee[] occludees = FindObjectsOfType<Occludee>();
        if (occludees.Length == 0)
        {
            Debug.LogWarning("No objects with Occludee component found in the scene.");
            return;
        }

        List<(GameObject obj, Bounds bounds)> objectsWithBounds = new List<(GameObject, Bounds)>();
        foreach (var occludee in occludees)
        {
            Renderer renderer = occludee.gameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"GameObject '{occludee.gameObject.name}' has Occludee but no Renderer. Skipping.");
                continue;
            }
            objectsWithBounds.Add((occludee.gameObject, renderer.bounds));
        }

        if (objectsWithBounds.Count == 0)
        {
            Debug.LogWarning("No Occludee objects with Renderer found in the scene.");
            return;
        }

        int overlapPairCount = 0;
        for (int i = 0; i < objectsWithBounds.Count - 1; i++)
        {
            for (int j = i + 1; j < objectsWithBounds.Count; j++)
            {
                var (objA, boundsA) = objectsWithBounds[i];
                var (objB, boundsB) = objectsWithBounds[j];
                if (boundsA.Intersects(boundsB)) ++overlapPairCount;
            }
        }
        Debug.Log($"There are {overlapPairCount} pairs of overlapping Occludee objects in the scene.");
    }
}