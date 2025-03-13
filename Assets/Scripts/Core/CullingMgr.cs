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
        foreach (var cullingMethod in cullingMethods)
            cullingMethod.Cull();
    }
}