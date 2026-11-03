using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InteractText", menuName = "Interaction/Interact Text")]
public class InteractText : ScriptableObject
{
    public List<InteractableDis> list;

    public string GetInteractText(InteractableType type)
    {
        foreach (var item in list)
        {
            if (item.InteractableType == type)
            {
                return item.interactText;
            }
        }

        return "";
    }
}

[Serializable]
public struct InteractableDis
{
    public InteractableType InteractableType;
    public string interactText;
}
