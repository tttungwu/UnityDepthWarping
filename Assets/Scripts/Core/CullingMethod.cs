using System.Collections.Generic;
using UnityEngine;

public abstract class CullingMethod : MonoBehaviour
{
    public virtual void Init(Camera cam, List<Occludee> occludees)
    {
        
    }

    public virtual void Cull(List<Occludee> occludees = null)
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
