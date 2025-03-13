using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrustumCullingTest : MonoBehaviour
{
    private Camera _camera;
    private Vector4[] _cornerPos;

    private Vector4[] _ndcFrustumPlanes = new Vector4[6]
    {
        new Vector4(-1, 0, 0, -1),
        new Vector4(1, 0, 0, -1),
        new Vector4(0, -1, 0, -1),
        new Vector4(0, 1, 0, -1),
        new Vector4(0, 0, -1, -1),
        new Vector4(0, 0, 1, -1),
    };
    private Vector4[] _viewFrustumPlanes = new Vector4[6];
    private Vector3[] _frustumCorners = new Vector3[8];
    private int[][] _planeIndices = new int[][]
    {
        new int[] {4, 6, 7},
        new int[] {1, 3, 2},
        new int[] {1, 5, 7},
        new int[] {0, 2, 6},
        new int[] {2, 4, 7},
        new int[] {0, 4, 5}
    };
    
    Vector4[] viewPos = new Vector4[8];
    Vector4[] ndcPos = new Vector4[8];
    
    public void GetFrustumCorners()
    {
        float near = _camera.nearClipPlane;
        float far = _camera.farClipPlane;
        float fov = _camera.fieldOfView;
        float aspect = _camera.aspect;

        float nearHeight = 2 * near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float nearWidth = nearHeight * aspect;
        float farHeight = 2 * far * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float farWidth = farHeight * aspect;

        Vector3 nearCenter = Vector3.forward * -near;
        Vector3 farCenter = Vector3.forward * -far;

        _frustumCorners[0] = nearCenter + new Vector3(-nearWidth * 0.5f, -nearHeight * 0.5f, 0);
        _frustumCorners[1] = nearCenter + new Vector3(nearWidth * 0.5f, -nearHeight * 0.5f, 0);
        _frustumCorners[2] = nearCenter + new Vector3(-nearWidth * 0.5f, nearHeight * 0.5f, 0);
        _frustumCorners[3] = nearCenter + new Vector3(nearWidth * 0.5f, nearHeight * 0.5f, 0);
        _frustumCorners[4] = farCenter + new Vector3(-farWidth * 0.5f, -farHeight * 0.5f, 0);
        _frustumCorners[5] = farCenter + new Vector3(farWidth * 0.5f, -farHeight * 0.5f, 0);
        _frustumCorners[6] = farCenter + new Vector3(-farWidth * 0.5f, farHeight * 0.5f, 0);
        _frustumCorners[7] = farCenter + new Vector3(farWidth * 0.5f, farHeight * 0.5f, 0);
    }
    
    public Vector4 GetPlaneEquation(int[] pointsIndex)
    {
        Vector3 v1 = _frustumCorners[pointsIndex[1]] - _frustumCorners[pointsIndex[0]];
        Vector3 v2 = _frustumCorners[pointsIndex[2]] - _frustumCorners[pointsIndex[0]];
        Vector3 normal = Vector3.Cross(v1, v2).normalized;
        float a = normal.x;
        float b = normal.y;
        float c = normal.z;
        float d = -(a * _frustumCorners[pointsIndex[1]].x + b * _frustumCorners[pointsIndex[1]].y + c * _frustumCorners[pointsIndex[1]].z);
        return new Vector4(a, b, c, d);
    }
    
    void Start()
    {
        _camera = Camera.main;
        GetFrustumCorners();
        for (int i = 0; i < _viewFrustumPlanes.Length; ++i)
            _viewFrustumPlanes[i] = GetPlaneEquation(_planeIndices[i]);
        
        Bounds bounds = GetComponent<Renderer>().bounds;
        _cornerPos = new Vector4[8];
        _cornerPos[0] = new Vector4(bounds.min.x, bounds.min.y, bounds.min.z, 1.0f);
        _cornerPos[1] = new Vector4(bounds.max.x, bounds.min.y, bounds.min.z, 1.0f);
        _cornerPos[2] = new Vector4(bounds.max.x, bounds.max.y, bounds.min.z, 1.0f);
        _cornerPos[3] = new Vector4(bounds.min.x, bounds.max.y, bounds.min.z, 1.0f);
        _cornerPos[4] = new Vector4(bounds.min.x, bounds.min.y, bounds.max.z, 1.0f);
        _cornerPos[5] = new Vector4(bounds.max.x, bounds.min.y, bounds.max.z, 1.0f);
        _cornerPos[6] = new Vector4(bounds.max.x, bounds.max.y, bounds.max.z, 1.0f);
        _cornerPos[7] = new Vector4(bounds.min.x, bounds.max.y, bounds.max.z, 1.0f);

        for (int i = 0; i < _cornerPos.Length; ++i)
        {
            viewPos[i] = _camera.worldToCameraMatrix * _cornerPos[i];
            ndcPos[i] = _camera.projectionMatrix * viewPos[i];
            ndcPos[i] /= ndcPos[i].w;
            Debug.Log(_cornerPos[i]);
            Debug.Log(viewPos[i]);
            Debug.Log(ndcPos[i]);
            Debug.Log("");
        }

        Debug.Log($"view result: {CheckIfInsideFrustum(_viewFrustumPlanes, viewPos)}");
        Debug.Log($"ndc result: {CheckIfInsideFrustum(_ndcFrustumPlanes, ndcPos)}");
    }

    bool CheckIfInsideFrustum(Vector4[] frustumPlane, Vector4[] cornerPos)
    {
        bool isOutside = false;
        for (int p = 0; p < 6; ++ p)
        {
            bool outSidePlane = true;
            for (int v = 0; v < 8; ++ v)
            {
                if (Vector4.Dot(frustumPlane[p], cornerPos[v]) <= 1e-8)
                {
                    outSidePlane = false;
                    break;
                }
            }
            if (outSidePlane)
            {
                isOutside = true;
                break;
            }
        }
        return !isOutside;
    }

    void Update()
    {
        // 绘制包围盒的 12 条边
        // 底面 (0-1-2-3-0)
        Debug.DrawLine(viewPos[0], viewPos[1], Color.red);
        Debug.DrawLine(viewPos[1], viewPos[2], Color.red);
        Debug.DrawLine(viewPos[2], viewPos[3], Color.red);
        Debug.DrawLine(viewPos[3], viewPos[0], Color.red);

        // 顶面 (4-5-6-7-4)
        Debug.DrawLine(viewPos[4], viewPos[5], Color.red);
        Debug.DrawLine(viewPos[5], viewPos[6], Color.red);
        Debug.DrawLine(viewPos[6], viewPos[7], Color.red);
        Debug.DrawLine(viewPos[7], viewPos[4], Color.red);

        // 连接底面和顶面 (0-4, 1-5, 2-6, 3-7)
        Debug.DrawLine(viewPos[0], viewPos[4], Color.red);
        Debug.DrawLine(viewPos[1], viewPos[5], Color.red);
        Debug.DrawLine(viewPos[2], viewPos[6], Color.red);
        Debug.DrawLine(viewPos[3], viewPos[7], Color.red);
    }
}
