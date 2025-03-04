using UnityEngine;

namespace DefaultNamespace
{
    public class IDWOccludee : MonoBehaviour
    {
        private Renderer objectRenderer;
        private Bounds bounds;
        private Vector3[] corners;
        private Vector2 screenMin;
        private Vector2 screenMax;
        
        void Start()
        {
            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer == null)
            {
                Debug.LogError("This object has no Renderer component, cannot retrieve bounds: " + gameObject.name);
                return;
            }
            bounds = objectRenderer.bounds;
            corners = GetBoundsCorners(bounds);
        }

        void Update()
        {
            screenMin = Vector2.zero;
            screenMax = Vector2.zero;
            bool firstPoint = true;
            foreach (var corner in corners)
            {
                Vector3 screenPoint = Camera.main.WorldToScreenPoint(corner);
                if (screenPoint.z < 0) continue;
                Vector2 screenPos = new Vector2(screenPoint.x, screenPoint.y);

                if (firstPoint)
                {
                    screenMin = screenPos;
                    screenMax = screenPos;
                    firstPoint = false;
                }
                else
                {
                    screenMin.x = Mathf.Min(screenMin.x, screenPos.x);
                    screenMin.y = Mathf.Min(screenMin.y, screenPos.y);
                    screenMax.x = Mathf.Max(screenMax.x, screenPos.x);
                    screenMax.y = Mathf.Max(screenMax.y, screenPos.y);
                }
            }
        }
        
        private Vector3[] GetBoundsCorners(Bounds b)
        {
            Vector3 center = b.center;
            Vector3 extents = b.extents;
            return new Vector3[]
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, extents.y, -extents.z),
                center + new Vector3(extents.x, extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y, extents.z),
                center + new Vector3(extents.x, -extents.y, extents.z),
                center + new Vector3(-extents.x, extents.y, extents.z),
                center + new Vector3(extents.x, extents.y, extents.z)
            };
        }
    }
}