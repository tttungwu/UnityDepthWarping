using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    // private Vector4 position = new Vector4(-0.5f, -0.5f, -0.5f, 1.0f);
    public float near = 0.3f, far = 1000f;
    void Start()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        Mesh mesh = meshRenderer.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        Vector4[] worldVertices = new Vector4[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var tempVec = meshRenderer.transform.TransformPoint(vertices[i]);
            worldVertices[i].x = tempVec.x;
            worldVertices[i].y = tempVec.y;
            worldVertices[i].z = tempVec.z;
            worldVertices[i].w = 1.0f;
        }
        Vector4[] screenVertices = new Vector4[vertices.Length];
        Matrix4x4 viewAndProjectionMatrix = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix;
        for (int i = 0; i < worldVertices.Length; i++)
        {
            screenVertices[i] = viewAndProjectionMatrix * worldVertices[i];
            screenVertices[i] /= screenVertices[i].w;
        }

        for (int i = 0; i < screenVertices.Length; i++)
        {
            Vector2 pos;
            pos.x = screenVertices[i].x * Screen.width / 2 + Screen.width / 2;
            pos.y = screenVertices[i].y * Screen.height / 2 + Screen.height / 2;
            float depth = (1.0f / (worldVertices[i].z + 3.42f) - 1.0f / 1000.0f) / (1.0f / 0.3f - 1.0f / 1000.0f);
            print(pos);
            print(depth);
        }
        
        // print(Camera.main.projectionMatrix);
        // print(Camera.main.worldToCameraMatrix);
        // Vector4 clipPos = viewAndProjectionMatrix * position;
        // print(clipPos);
        // Vector4 ndcPos = clipPos / clipPos.w;
        // print(ndcPos);
        // print((1.0 / ndcPos.z - 1.0 / far) / (1.0 / near - 1.0 / far));
        // print((1.0f / 2.92f - 1.0f / 1000.0f) / (1.0f / 0.3f - 1.0f / 1000.0f));
    }

    void Update()
    {
        
    }
}
