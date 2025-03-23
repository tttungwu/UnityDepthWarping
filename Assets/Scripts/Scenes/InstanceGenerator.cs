using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RandomInstanceGenerator : EditorWindow
{
    private Mesh selectedMesh;
    private Material selectedMaterial;
    private Vector3 areaSize = new Vector3(10f, 10f, 10f);
    private int instanceCount = 10;
    private float minScale = 0.5f;
    private float maxScale = 2f;
    private float safetyDistance = 0.1f;

    [MenuItem("Tools/Random Instance Generator")]
    public static void ShowWindow()
    {
        GetWindow<RandomInstanceGenerator>("Random Instance Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Random Instance Generator", EditorStyles.boldLabel);

        selectedMesh = (Mesh)EditorGUILayout.ObjectField("Mesh", selectedMesh, typeof(Mesh), false);
        selectedMaterial = (Material)EditorGUILayout.ObjectField("Material", selectedMaterial, typeof(Material), false);
        areaSize = EditorGUILayout.Vector3Field("Area Size", areaSize);
        instanceCount = EditorGUILayout.IntField("Instance Count", instanceCount);
        minScale = EditorGUILayout.FloatField("Min Scale", minScale);
        maxScale = EditorGUILayout.FloatField("Max Scale", maxScale);
        safetyDistance = EditorGUILayout.FloatField("Safety Distance", safetyDistance);

        if (GUILayout.Button("Generate Instances"))
        {
            GenerateInstances();
        }
    }

    private void GenerateInstances()
    {
        if (selectedMesh == null || selectedMaterial == null)
        {
            Debug.LogError("Please select both a Mesh and a Material!");
            return;
        }

        List<Bounds> existingBounds = new List<Bounds>();
        List<Matrix4x4> matrices = new List<Matrix4x4>();

        GameObject tempObj = new GameObject("Temp");
        MeshFilter mf = tempObj.AddComponent<MeshFilter>();
        MeshRenderer mr = tempObj.AddComponent<MeshRenderer>();
        mf.mesh = selectedMesh;
        mr.material = selectedMaterial;

        for (int i = 0; i < instanceCount; i++)
        {
            int attempts = 0;
            const int maxAttempts = 100;
            bool validPosition = false;
            Vector3 position = Vector3.zero;
            float scale = 0.0f;
            Quaternion rotation = Quaternion.identity;

            while (attempts < maxAttempts && !validPosition)
            {
                position = new Vector3(
                    Random.Range(-areaSize.x / 2, areaSize.x / 2),
                    Random.Range(-areaSize.y / 2, areaSize.y / 2),
                    Random.Range(-areaSize.z / 2, areaSize.z / 2)
                );
                scale = Random.Range(minScale, maxScale);
                rotation = Random.rotation;

                tempObj.transform.position = position;
                tempObj.transform.rotation = rotation;
                tempObj.transform.localScale = Vector3.one * scale;

                Bounds currentBounds = mr.bounds;
                currentBounds.Expand(safetyDistance);

                validPosition = true;
                foreach (Bounds bounds in existingBounds)
                {
                    if (currentBounds.Intersects(bounds))
                    {
                        validPosition = false;
                        break;
                    }
                }

                attempts++;
            }

            if (validPosition)
            {
                existingBounds.Add(mr.bounds);
                Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
                matrices.Add(matrix);
            }
        }

        DestroyImmediate(tempObj);

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string assetName = $"{selectedMesh.name}_{selectedMaterial.name}_{matrices.Count}_{timestamp}";
        InstanceDataAsset instanceDataAsset = ScriptableObject.CreateInstance<InstanceDataAsset>();
        instanceDataAsset.matrices = matrices.ToArray();
        instanceDataAsset.material = selectedMaterial;
        instanceDataAsset.mesh = selectedMesh;

        string path = $"Assets/Prefabs/Instances/{assetName}.asset";
        AssetDatabase.CreateAsset(instanceDataAsset, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"Generated {matrices.Count} instances. Asset saved as: {path}");
    }
}