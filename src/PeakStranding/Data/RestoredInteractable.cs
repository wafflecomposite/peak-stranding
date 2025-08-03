using System;
using Photon.Pun;
using UnityEngine;

public class RestoredInteractable : MonoBehaviour, IInteractible
{
    public string RestoredName = "Restored Interactable";

    public string GetName()
    {
        return RestoredName;
    }
    public bool IsInteractible(Character interactor)
    {
        return true;
    }

    public void Interact(Character interactor) { }

    public void HoverEnter() { }

    public void HoverExit() { }

    public Vector3 Center() { return base.transform.position; }

    public Transform GetTransform()
    {
        return base.transform;
    }

    public string GetInteractionText() { return string.Empty; }


}