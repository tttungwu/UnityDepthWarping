using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

public class SceneGenerator : MonoBehaviour
{
    private static string[] colorNames = new string[]
    {
        "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta", "White"
    };

    [MenuItem("Tools/Scenes/Generate Materials")]
    public static void GenerateMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            Debug.LogError("Universal Render Pipeline/Lit shader not found. Please ensure URP is installed and configured in the project.");
            return;
        }

        Color[] fixedColors = new Color[]
        {
            Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta, Color.white
        };

        for (int i = 0; i < fixedColors.Length; i++)
        {
            Material mat = new Material(urpLitShader);
            mat.SetColor("_BaseColor", fixedColors[i]);
            string matName = colorNames[i];
            AssetDatabase.CreateAsset(mat, "Assets/Materials/" + matName + ".mat");
        }

        Debug.Log("7 color materials have been generated and saved to Assets/Materials folder.");
    }

    [MenuItem("Tools/Scenes/Generate Scene")]
    public static void GenerateScene()
    {
        Material[] materials;
        materials = new Material[colorNames.Length];
        for (int i = 0; i < colorNames.Length; i++)
        {
            materials[i] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + colorNames[i] + ".mat");
            if (materials[i] == null)
            {
                Debug.LogError("Material " + colorNames[i] + ".mat not found. Please generate materials first.");
                return;
            }
        }

        GameObject occludees = GameObject.Find("OCCLUDEES");
        if (occludees == null) occludees = new GameObject("OCCLUDEES");

        int numObjects = 20000;
        List<GameObject> existingObjects = new List<GameObject>();

        PrimitiveType[] typePool = new PrimitiveType[]
        {
            PrimitiveType.Cube,
            PrimitiveType.Sphere,
            PrimitiveType.Sphere,
            PrimitiveType.Sphere,
            PrimitiveType.Capsule
        };

        for (int i = 0; i < numObjects; i++)
        {
            PrimitiveType type = typePool[Random.Range(0, typePool.Length)];
            GameObject obj = GameObject.CreatePrimitive(type);
            Collider objCollider;
            switch (type)
            {
                case PrimitiveType.Cube:
                    objCollider = obj.GetComponent<BoxCollider>();
                    if (objCollider == null) objCollider = obj.AddComponent<BoxCollider>();
                    break;
                case PrimitiveType.Sphere:
                    objCollider = obj.GetComponent<SphereCollider>();
                    if (objCollider == null) objCollider = obj.AddComponent<SphereCollider>();
                    break;
                case PrimitiveType.Capsule:
                    objCollider = obj.GetComponent<CapsuleCollider>();
                    if (objCollider == null) objCollider = obj.AddComponent<CapsuleCollider>();
                    break;
                default:
                    objCollider = obj.AddComponent<BoxCollider>();
                    break;
            }

            Vector3 pos = Vector3.zero;
            float s = 0.0f;
            int attempts = 0;
            bool placed = false;

            while (attempts < 10)
            {
                s = Random.Range(0.5f, 10f);
                obj.transform.localScale = new Vector3(s, s, s);

                float yRotation = Random.Range(0f, 360f);
                obj.transform.rotation = Quaternion.Euler(0, yRotation, 0);

                float x = Random.Range(500f, 1495f);
                float z = Random.Range(-500f, 495f);

                if (type == PrimitiveType.Cube || type == PrimitiveType.Sphere)
                {
                    pos = new Vector3(x, 0.5f * s, z); 
                }
                else 
                {
                    pos = new Vector3(x, s, z);
                }
                obj.transform.position = pos;
                Physics.SyncTransforms();

                bool overlap = false;
                foreach (var existing in existingObjects)
                {
                    Renderer existingRenderer = existing.GetComponent<Renderer>();
                    Renderer objRenderer = obj.GetComponent<Renderer>();
                    if (existingRenderer != null && objRenderer != null && objRenderer.bounds.Intersects(existingRenderer.bounds))
                    {
                        overlap = true;
                        break;
                    }
                }

                if (!overlap)
                {
                    placed = true;
                    break;
                }

                attempts++;
            }

            if (!placed)
            {
                Debug.LogWarning("Could not place object without overlapping after 10 attempts. Skipping this object.");
                DestroyImmediate(obj);
                continue;
            }

            int matIndex = Random.Range(0, materials.Length);
            obj.GetComponent<MeshRenderer>().material = materials[matIndex];

            existingObjects.Add(obj);

            obj.transform.parent = occludees.transform;
        }
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Scene generation completed. Generated " + existingObjects.Count + " objects.");
    }
}