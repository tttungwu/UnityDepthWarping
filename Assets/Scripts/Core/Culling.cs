using System.Collections.Generic;
using Core;
using UnityEngine;

public abstract class Culling : MonoBehaviour
{
    public virtual void Init(Camera cam, List<Occludee> bounds) {}
    public virtual void Cull(List<Occludee> bounds = null) {}
}
