using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CullingMgr : MonoBehaviour
{
    [SerializeField] private CullingMethod cullingMethods;
}