using TMPro;
using UnityEngine;

public class FirstPersonCanvas : Singleton<FirstPersonCanvas>
{
    [SerializeField] private TextMeshProUGUI displayText;
    [SerializeField] private InteractText interactText;

    public void ShowText(InteractableType interactableType)
    {
        displayText.gameObject.SetActive(true);
        displayText.text = interactText.GetInteractText(interactableType);
    }

    public void HideText()
    {
        displayText.gameObject.SetActive(false);
    }
}
