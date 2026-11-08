using UnityEngine;

public class InteractableObject : MonoBehaviour
{
   public InteractableType interactableType;

   [Tooltip("Khoảng cách tối đa (mét) từ camera để tương tác được với vật này. Ghế 1, gương 2.5.")]
   [Min(0.1f)] public float maxDistance = 1f;
}

public enum InteractableType
{
    None,
    Chair,
    Mirror
}
