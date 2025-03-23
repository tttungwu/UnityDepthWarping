using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AABBTest : MonoBehaviour
{
    public Camera cam;
    
    void Start()
    {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
        
        Vector3 center = new Vector3(-0.92f, 0.27f, -2.77f);
        Vector3 extent = new Vector3(2.59f, 2.13f, 2.45f);
        Vector3 size = 2 * extent;
        Bounds bounds = new Bounds(center, size);
        
        Debug.Log(GeometryUtility.TestPlanesAABB(frustumPlanes, bounds));
        Debug.Log(bounds);
        for (int i = 0; i < frustumPlanes.Length; i++)
            Debug.Log(frustumPlanes[i]);
    }
    
}
