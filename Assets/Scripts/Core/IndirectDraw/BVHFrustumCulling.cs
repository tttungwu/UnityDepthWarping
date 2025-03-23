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
            Bounds[] instanceBounds = new Bounds[matrices.Length];
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
                instanceBounds[i] = new Bounds(center, size);
                indices[i] = i;
            }
            
            _rootNode = BuildBVHRecursive(instanceBounds, indices, 0, indices.Length);
        }
        
        private BVHNode BuildBVHRecursive(Bounds[] bounds, int[] indices, int start, int count)
        {
            if (count == 0 || bounds == null || indices == null) return null;
            
            BVHNode node = new BVHNode();
            if (count <= leafThreshold)
            {
                node.indices = new int[count];
                System.Array.Copy(indices, start, node.indices, 0, count);
                node.bounds = bounds[start];
                for (int i = start + 1; i < start + count; i++)
                {
                    node.bounds.Encapsulate(bounds[i]);
                }
                return node;
            }

            Bounds nodeBounds = bounds[start];
            for (int i = start + 1; i < start + count; i++)
            {
                nodeBounds.Encapsulate(bounds[i]);
            }
            node.bounds = nodeBounds;

            Vector3 size = nodeBounds.size;
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

        public override void Cull(Matrix4x4[] matrices = null)
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
                    visibleCount++;
                }
                return;
            }
            
            CollectVisibleMatrices(node.left, visibleMatrices, ref visibleCount);
            CollectVisibleMatrices(node.right, visibleMatrices, ref visibleCount);
        }
        
        public override Matrix4x4[] GetInvisibleMatrices()
        {
            if (_rootNode == null || _matrices == null) return null;

            Matrix4x4[] invisibleMatrices = new Matrix4x4[_matrices.Length];
            int invisibleCount = 0;
            CollectInvisibleMatrices(_rootNode, invisibleMatrices, ref invisibleCount);

            Matrix4x4[] result = new Matrix4x4[invisibleCount];
            System.Array.Copy(invisibleMatrices, result, invisibleCount);
            return result;
        }
        
        private void CollectInvisibleMatrices(BVHNode node, Matrix4x4[] invisibleMatrices, ref int invisibleCount)
        {
            if (node == null) return;

            if (!node.visible && node.isLeaf)
            {
                for (int i = 0; i < node.indices.Length && invisibleCount < invisibleMatrices.Length; i++)
                {
                    invisibleMatrices[invisibleCount] = _matrices[node.indices[i]];
                    invisibleCount++;
                }
                return;
            }

            CollectInvisibleMatrices(node.left, invisibleMatrices, ref invisibleCount);
            CollectInvisibleMatrices(node.right, invisibleMatrices, ref invisibleCount);
        }
    }
}