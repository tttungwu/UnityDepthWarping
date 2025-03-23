// #define BVHFRUSTUMCULLINGDEBUG

using System.Collections.Generic;
using UnityEngine;

namespace Core.IndirectDraw
{
    public class BVHFrustumCulling : CullingMethod
    {
        private class BVHNode
        {
            public Bounds bounds;
            public BVHNode left;
            public BVHNode right;
            public int[] indices;
            public bool visible;
            public bool isLeaf => left == null && right == null;
        }
        
        private BVHNode _rootNode;
        private Camera _camera;
        private Mesh _mesh;
        private Matrix4x4[] _matrices;
        private Bounds[] _instanceBounds;
        [SerializeField] private int leafThreshold = 4;

        public override void Init(Camera cam, Mesh mesh, Matrix4x4[] matrices)
        {
            if (cam == null || mesh == null || matrices == null)
            {
                Debug.LogError("Init failed: Camera, Mesh or Matrices is null");
                return;
            }
            
            _camera = cam;
            _mesh = mesh;
            _matrices = matrices;
            _instanceBounds = new Bounds[matrices.Length];
            int[] indices = new int[matrices.Length];
            
            for (int i = 0; i < matrices.Length; i++)
            {
                Matrix4x4 objectToWorld = matrices[i];
    
                Bounds meshBounds = _mesh.bounds;
                Vector3 extents = meshBounds.extents;
                Vector3[] localCorners = new Vector3[8]
                {
                    meshBounds.center + new Vector3(extents.x, extents.y, extents.z),
                    meshBounds.center + new Vector3(-extents.x, extents.y, extents.z),
                    meshBounds.center + new Vector3(extents.x, -extents.y, extents.z),
                    meshBounds.center + new Vector3(-extents.x, -extents.y, extents.z),
                    meshBounds.center + new Vector3(extents.x, extents.y, -extents.z),
                    meshBounds.center + new Vector3(-extents.x, extents.y, -extents.z),
                    meshBounds.center + new Vector3(extents.x, -extents.y, -extents.z),
                    meshBounds.center + new Vector3(-extents.x, -extents.y, -extents.z)
                };

                Vector3 min = objectToWorld.MultiplyPoint(localCorners[0]);
                Vector3 max = min;
                
                for (int j = 1; j < 8; j++)
                {
                    Vector3 worldCorner = objectToWorld.MultiplyPoint(localCorners[j]);
                    min = Vector3.Min(min, worldCorner);
                    max = Vector3.Max(max, worldCorner);
                }
                
                Vector3 center = (min + max) * 0.5f;
                Vector3 size = max - min;
                _instanceBounds[i] = new Bounds(center, size);
                
                indices[i] = i;
            }
            
            _rootNode = BuildBVHRecursive(_instanceBounds, indices, 0, indices.Length);
        }
        
        private BVHNode BuildBVHRecursive(Bounds[] bounds, int[] indices, int start, int count)
        {
            if (count == 0 || bounds == null || indices == null) return null;
            
            BVHNode node = new BVHNode();
            node.bounds = bounds[indices[start]];
            for (int i = start + 1; i < start + count; i++)
            {
                node.bounds.Encapsulate(bounds[indices[i]]);
            }
            
            if (count <= leafThreshold)
            {
                node.indices = new int[count];
                System.Array.Copy(indices, start, node.indices, 0, count);
                return node;
            }

            Vector3 size = node.bounds.size;
            int axis = 0;
            if (size.y > size.x && size.y > size.z) axis = 1;
            else if (size.z > size.x && size.z > size.y) axis = 2;

            System.Array.Sort(indices, start, count, Comparer<int>.Create((a, b) => 
                bounds[a].center[axis].CompareTo(bounds[b].center[axis])));
            
            int mid = count / 2;
            node.left = BuildBVHRecursive(bounds, indices, start, mid);
            node.right = BuildBVHRecursive(bounds, indices, start + mid, count - mid);

            return node;
        }

        public override void Cull(Matrix4x4[] matrices = null, ComputeBuffer cullResultBuffer = null)
        {
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_camera);
            FrustumCullRecursive(_rootNode, frustumPlanes);
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
        
        public override Matrix4x4[] GetVisibleMatrices()
        {
            if (_rootNode == null || _matrices == null) return null;

            Matrix4x4[] visibleMatrices = new Matrix4x4[_matrices.Length];
            int visibleCount = 0;
            CollectVisibleMatrices(_rootNode, visibleMatrices, ref visibleCount);

            Matrix4x4[] result = new Matrix4x4[visibleCount];
            System.Array.Copy(visibleMatrices, result, visibleCount);
            return result;
        }
        
        private void CollectVisibleMatrices(BVHNode node, Matrix4x4[] visibleMatrices, ref int visibleCount)
        {
            if (node == null || !node.visible) return;
            
            if (node.isLeaf)
            {
                for (int i = 0; i < node.indices.Length && visibleCount < visibleMatrices.Length; i++)
                {
                    visibleMatrices[visibleCount] = _matrices[node.indices[i]];
#if BVHFRUSTUMCULLINGDEBUG
                    Debug.Log(_instanceBounds[node.indices[i]]);
                    DrawBounds(_instanceBounds[node.indices[i]], Color.green);
#endif
                    visibleCount++;
                }
                return;
            }
            
            CollectVisibleMatrices(node.left, visibleMatrices, ref visibleCount);
            CollectVisibleMatrices(node.right, visibleMatrices, ref visibleCount);
        }
#if BVHFRUSTUMCULLINGDEBUG
        private void DrawBounds(Bounds bounds, Color color)
        {
            Vector3[] corners = GetCorners(bounds);
    
            Debug.DrawLine(corners[0], corners[1], color);
            Debug.DrawLine(corners[1], corners[3], color);
            Debug.DrawLine(corners[3], corners[2], color);
            Debug.DrawLine(corners[2], corners[0], color);
    
            Debug.DrawLine(corners[4], corners[5], color);
            Debug.DrawLine(corners[5], corners[7], color);
            Debug.DrawLine(corners[7], corners[6], color);
            Debug.DrawLine(corners[6], corners[4], color);
    
            Debug.DrawLine(corners[0], corners[4], color);
            Debug.DrawLine(corners[1], corners[5], color);
            Debug.DrawLine(corners[2], corners[6], color);
            Debug.DrawLine(corners[3], corners[7], color);
        }

        private Vector3[] GetCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new Vector3[]
            {
                new Vector3(min.x, min.y, min.z), 
                new Vector3(min.x, min.y, max.z), 
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z) 
            };
        }
#endif
    }
}