using System.Collections.Generic;
using UnityEngine;

public abstract class Culling : MonoBehaviour
{
    public virtual void Init(Camera cam, List<Bounds> bounds) {}
    public virtual void Cull(Camera cam, List<Bounds> bounds) {}
}
