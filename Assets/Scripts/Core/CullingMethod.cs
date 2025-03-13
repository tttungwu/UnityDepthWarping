using System.Collections.Generic;
using UnityEngine;

public abstract class CullingMethod : MonoBehaviour
{
    public virtual void Init(Camera cam, List<Occludee> bounds)
    {
        
    }

    public virtual void Cull(List<Occludee> bounds = null)
    {
        
    }

    public virtual List<Occludee> GetVisibleBounds()
    {
        return null;
    }

    public virtual List<Occludee> GetInvisibleBounds()
    {
        return null;
    }
}
