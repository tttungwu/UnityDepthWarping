using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Occludee : MonoBehaviour
{
    private int _visibleLayer;
    private int _invisibleLayer;

    private void Start()
    {
        _visibleLayer = LayerMask.NameToLayer("Default");
        _invisibleLayer = LayerMask.NameToLayer("Occludee");
        gameObject.layer = _visibleLayer;
    }

    public void MarkAsOccluded()
    {
        gameObject.layer = _invisibleLayer;
    }

    public void MarkAsVisible()
    {
        gameObject.layer = _visibleLayer;
    }

    public Bounds GetBounds()
    {
        return GetComponent<Renderer>().bounds;
    }
}
