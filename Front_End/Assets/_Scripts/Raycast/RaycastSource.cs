using UnityEngine;

public class RaycastSource : Singleton<RaycastSource>
{
    public InteractableObject currentObject;

    [SerializeField] private float distance = 1.0f;

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
            if (!hit.collider.CompareTag("InteractableObject"))
            {
                return;
            }

            InteractableObject obj = hit.collider.GetComponent<InteractableObject>();
            FirstPersonCanvas.Instance.ShowText(obj.interactableType);
            currentObject = obj;
            hitInteractable = true;
        }

        if (!hitInteractable)
        {
            FirstPersonCanvas.Instance.HideText();
            currentObject = null;
        }
    }
}
