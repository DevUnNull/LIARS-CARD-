using UnityEngine;

public class RaycastSource : Singleton<RaycastSource>
{
    public InteractableObject currentObject;

    [Tooltip("Tầm tia tối đa (mét). Mỗi vật tự giới hạn thêm bằng InteractableObject.maxDistance.")]
    [SerializeField] private float distance = 3.0f;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Update()
    {
        HandleTargetRaycast();
    }

    private void HandleTargetRaycast()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        bool hitInteractable = false;
        
        if (Physics.Raycast(ray, out RaycastHit hit, distance))
        {
            // Trúng tường / vật thường, hoặc vật ở xa hơn tầm riêng của nó -> coi như không nhắm gì
            InteractableObject obj = hit.collider.CompareTag("InteractableObject") ? hit.collider.GetComponent<InteractableObject>() : null;
            if (obj != null && hit.distance <= obj.maxDistance)
            {
                FirstPersonCanvas.Instance.ShowText(obj.interactableType);
                currentObject = obj;
                hitInteractable = true;
            }
        }

        if (!hitInteractable)
        {
            FirstPersonCanvas.Instance.HideText();
            currentObject = null;
        }
    }
}
