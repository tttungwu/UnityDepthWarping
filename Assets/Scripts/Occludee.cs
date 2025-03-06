using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Occludee : MonoBehaviour
{
    public Bounds GetBounds()
    {
        return GetComponent<Renderer>().bounds;
    }
}