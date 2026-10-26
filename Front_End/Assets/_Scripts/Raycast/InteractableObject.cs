using UnityEngine;

public class InteractableObject : MonoBehaviour
{
   public InteractableType interactableType;
}

public enum InteractableType
{
    None,
    Chair
}
