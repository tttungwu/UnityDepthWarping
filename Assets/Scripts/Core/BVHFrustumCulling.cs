using System.Collections.Generic;
using Core;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class BVHFrustumCulling : Culling
{
    public class BVHNode
    {
        public Bounds bounds;
        public BVHNode left;
        public BVHNode right;
        public List<Occludee> objects;
        public bool visible;
    }

    public int leafThreshold = 4;
    private BVHNode _root;
    private Camera _camera;

    void Start()
    {
        _camera = GetComponent<Camera>();
    }
    
    public void BuildBVH(List<Occludee> occludeeList) 
    {
        _root = BuildBVHRecursive(occludeeList);
    }
    
    private BVHNode BuildBVHRecursive(List<Occludee> objs) 
    {
        BVHNode node = new BVHNode();
        node.bounds = ComputeBounds(objs);
        if (objs.Count <= leafThreshold) 
        {
            node.objects = objs;
            return node;
        }
        
        Vector3 size = node.bounds.size;
        int axis = 0; // 0:x, 1:y, 2:z
        if (size.y >= size.x && size.y >= size.z) axis = 1;
        else if (size.z >= size.x && size.z >= size.y) axis = 2;
        objs.Sort((a, b) => {
            float aCenter = a.GetBounds().center[axis];
            float bCenter = b.GetBounds().center[axis];
            return aCenter.CompareTo(bCenter);
        });
        
        int mid = objs.Count / 2;
        List<Occludee> leftList = objs.GetRange(0, mid);
        List<Occludee> rightList = objs.GetRange(mid, objs.Count - mid);

        node.left = BuildBVHRecursive(leftList);
        node.right = BuildBVHRecursive(rightList);
        return node;
    }
    
    private Bounds ComputeBounds(List<Occludee> objs) 
    {
        if (objs == null || objs.Count == 0)
            return new Bounds();

        Bounds bounds = objs[0].GetBounds();
        for (int i = 1; i < objs.Count; ++ i) bounds.Encapsulate(objs[i].GetBounds());
        return bounds;
    }
    
    public void FrustumCull() 
    {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_camera);
        FrustumCullRecursive(_root, frustumPlanes);
    }
    
    private void FrustumCullRecursive(BVHNode node, Plane[] frustumPlanes) 
    {
        if (node == null) return;

        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, node.bounds))
        {
            node.visible = false;
            return;
        }

        node.visible = true;
        FrustumCullRecursive(node.left, frustumPlanes);
        FrustumCullRecursive(node.right, frustumPlanes);
    }

    public void GetAllLeafBounds(BVHNode node, List<Bounds> bounds)
    {
        if (node == null) return;
        if (node.objects != null)
        {
            foreach (var occludee in node.objects)
            {
                bounds.Add(occludee.GetComponent<Renderer>().bounds);
            }
        }
        GetAllLeafBounds(node.left, bounds);
        GetAllLeafBounds(node.right, bounds);
    }

    public List<Bounds> GetVisibleBounds()
    {
        List<Bounds> visibleBounds = new List<Bounds>();
        GetVisibleBoundsRecursive(_root, visibleBounds);
        return visibleBounds;
    }

    private void GetVisibleBoundsRecursive(BVHNode node, List<Bounds> visibleBounds)
    {
        if (node == null || node.visible == false) return;
        if (node.objects != null)
        {
            foreach (var occludee in node.objects)
            {
                visibleBounds.Add(occludee.GetComponent<Renderer>().bounds);
            }
        }
        GetVisibleBoundsRecursive(node.left, visibleBounds);
        GetVisibleBoundsRecursive(node.right, visibleBounds);
    }

    public List<Bounds> GetInvisibleBounds()
    {
        List<Bounds> invisibleBounds = new List<Bounds>();
        GetInVisibleBoundsRecursive(_root, invisibleBounds);
        return invisibleBounds;
    }

    private void GetInVisibleBoundsRecursive(BVHNode node, List<Bounds> invisibleBounds)
    {
        if (node == null) return;
        if (node.visible == false) GetAllLeafBounds(node, invisibleBounds);
        else
        {
            GetInVisibleBoundsRecursive(node.left, invisibleBounds);
            GetInVisibleBoundsRecursive(node.right, invisibleBounds);
        }
    }
}
