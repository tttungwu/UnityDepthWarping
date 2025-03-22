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
            float scale = Random.Range(minScale, maxScale);
            Quaternion rotation = Random.rotation;

            while (attempts < maxAttempts && !validPosition)
            {
                position = new Vector3(
                    Random.Range(-areaSize.x / 2, areaSize.x / 2),
                    Random.Range(-areaSize.y / 2, areaSize.y / 2),
                    Random.Range(-areaSize.z / 2, areaSize.z / 2)
                );

                // 设置临时对象的变换
                tempObj.transform.position = position;
                tempObj.transform.rotation = rotation;
                tempObj.transform.localScale = Vector3.one * scale;

                // 获取当前对象的Bounds
                Bounds currentBounds = mr.bounds;
                currentBounds.Expand(safetyDistance); // 增加安全距离

                validPosition = true;
                // 检查与所有已有物体是否相交
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
                existingBounds.Add(mr.bounds); // 保存这次的bounds
                Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
                matrices.Add(matrix);
            }
        }

        // 清理临时对象
        DestroyImmediate(tempObj);

        // 创建并保存变换矩阵资产
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string assetName = $"{selectedMesh.name}_{selectedMaterial.name}_{timestamp}";
        MatrixAsset matrixAsset = ScriptableObject.CreateInstance<MatrixAsset>();
        matrixAsset.matrices = matrices.ToArray();

        string path = $"Assets/{assetName}.asset";
        AssetDatabase.CreateAsset(matrixAsset, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"Generated {matrices.Count} instances. Asset saved as: {path}");

        // 可视化预览
        GameObject previewParent = new GameObject("Preview_" + assetName);
        for (int i = 0; i < matrices.Count; i++)
        {
            GameObject instance = new GameObject($"Instance_{i}");
            instance.transform.parent = previewParent.transform;
            instance.transform.position = matrices[i].GetColumn(3);
            instance.transform.rotation = matrices[i].rotation;
            instance.transform.localScale = matrices[i].lossyScale;

            MeshFilter instanceMF = instance.AddComponent<MeshFilter>();
            MeshRenderer instanceMR = instance.AddComponent<MeshRenderer>();
            instanceMF.mesh = selectedMesh;
            instanceMR.material = selectedMaterial;
        }
    }
}

// 用于存储矩阵的资产类
public class MatrixAsset : ScriptableObject
{
    public Matrix4x4[] matrices;
}