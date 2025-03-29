using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class EqualSpacingInstanceGenerator : EditorWindow
{
    private Mesh selectedMesh;
    private Material selectedMaterial;
    private int instanceCount = 10;
    private float spacing = 1f;
    private float scale = 1f;

    [MenuItem("Tools/Equal Spacing Instance Generator")]
    public static void ShowWindow()
    {
        GetWindow<EqualSpacingInstanceGenerator>("Equal Spacing Instance Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Equal Spacing Instance Generator", EditorStyles.boldLabel);

        selectedMesh = (Mesh)EditorGUILayout.ObjectField("Mesh", selectedMesh, typeof(Mesh), false);
        selectedMaterial = (Material)EditorGUILayout.ObjectField("Material", selectedMaterial, typeof(Material), false);
        instanceCount = EditorGUILayout.IntField("Instance Count", instanceCount);
        spacing = EditorGUILayout.FloatField("Spacing (Z-Axis)", spacing);
        scale = EditorGUILayout.FloatField("Scale", scale);

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

        List<Matrix4x4> matrices = new List<Matrix4x4>();

        for (int i = 0; i < instanceCount; i++)
        {
            Vector3 position = new Vector3(0, 0, i * spacing);
            Quaternion rotation = Quaternion.identity;
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
            matrices.Add(matrix);
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string assetName = $"EqualSpacing_{selectedMesh.name}_{selectedMaterial.name}_{instanceCount}_{timestamp}";
        InstanceDataAsset instanceDataAsset = ScriptableObject.CreateInstance<InstanceDataAsset>();
        instanceDataAsset.matrices = matrices.ToArray();
        instanceDataAsset.material = selectedMaterial;
        instanceDataAsset.mesh = selectedMesh;

        string path = $"Assets/Prefabs/Instances/{assetName}.asset";
        AssetDatabase.CreateAsset(instanceDataAsset, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"Generated {instanceCount} instances. Asset saved as: {path}");
    }
}
