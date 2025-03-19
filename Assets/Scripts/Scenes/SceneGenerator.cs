using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SceneGenerator : MonoBehaviour
{
    public int numObjects = 50000;
    private List<Material> materials = new List<Material>();

    private Color[] fixedColors = new Color[]
    {
        Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta, Color.white
    };

    private string[] colorNames = new string[]
    {
        "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta", "White"
    };

    [ContextMenu("Generate Materials")]
    public void GenerateMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        for (int i = 0; i < fixedColors.Length; i++)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = fixedColors[i];
            string matName = colorNames[i];
            AssetDatabase.CreateAsset(mat, "Assets/Materials/" + matName + ".mat");
            materials.Add(mat);
        }

        Debug.Log("7种颜色的材质已生成并保存到Assets/Materials文件夹。");
    }

    [ContextMenu("Generate Scene")]
    public void GenerateScene()
    {
        if (materials.Count == 0)
        {
            for (int i = 0; i < colorNames.Length; i++)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + colorNames[i] + ".mat");
                if (mat != null)
                {
                    materials.Add(mat);
                }
                else
                {
                    Debug.LogError("材质 " + colorNames[i] + ".mat 未找到，请先生成材质。");
                    return;
                }
            }
        }

        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        int matIndex = Random.Range(0, materials.Count);
        obj.GetComponent<MeshRenderer>().material = materials[matIndex];

        Debug.Log("场景生成完成，物体已分配随机颜色材质。");
    }
}