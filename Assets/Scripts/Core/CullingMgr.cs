using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CullingMgr : MonoBehaviour
{
    [SerializeField] private CullingMethod[] cullingMethods;
    private Camera _camera;
    private List<Occludee> _occludees;

    void Start()
    {
        _camera = GetComponent<Camera>();
        _occludees = FindObjectsOfType<Occludee>().ToList();
        
        foreach (var cullingMethod in cullingMethods)
            cullingMethod.Init(_camera, _occludees);
    }

    void Update()
    {
        List<Occludee> occludees = new List<Occludee>(_occludees);
        foreach (var cullingMethod in cullingMethods)
        {
            cullingMethod.Cull(occludees);
            occludees = cullingMethod.GetVisibleBounds();
        }

        for (int i = 0; i < _occludees.Count; i++) _occludees[i].MarkAsOccluded();
        List<Occludee> visible = cullingMethods[^1].GetVisibleBounds();
        for (int i = 0; i < visible.Count; i++) visible[i].MarkAsVisible();
    }
}