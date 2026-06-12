using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class XRIOfficialInteractableBridge : MonoBehaviour
{
    [SerializeField] private XRRayInteractor leftHandRayInteractor;
    [SerializeField] private ActionBasedController leftHandController;
    [SerializeField] private XRRayInteractor rightHandRayInteractor;
    [SerializeField] private ActionBasedController rightHandController;
    [SerializeField] private GameObject interactionOwner;
    [SerializeField] private InteractionPrompt prompt;
    [SerializeField] private float interactDistance = 4f;
    [SerializeField] private float interactRadius = 0.08f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField] private float floorBlockNormalY = 0.6f;

    private IInteractable focusedInteractable;
    private Transform priorityRayOriginOverride;

    private void Update()
    {
        IInteractable overrideFocusedInteractable = FindFocusedInteractable(priorityRayOriginOverride);
        IInteractable rightFocusedInteractable = FindFocusedInteractable(rightHandRayInteractor);
        IInteractable leftFocusedInteractable = FindFocusedInteractable(leftHandRayInteractor);
        focusedInteractable = overrideFocusedInteractable ?? rightFocusedInteractable ?? leftFocusedInteractable;
        UpdatePrompt();

        if (rightFocusedInteractable != null && WasTriggered(rightHandController))
            rightFocusedInteractable.Interact(ResolveInteractionOwner(null));
        else if (leftFocusedInteractable != null && WasTriggered(leftHandController))
            leftFocusedInteractable.Interact(ResolveInteractionOwner(null));
    }

    private void OnDisable()
    {
        focusedInteractable = null;
        if (prompt != null)
            prompt.Hide();
    }

    public bool TryInteractFromRightHand(GameObject ownerOverride = null)
    {
        return TryInteractFromRay(rightHandRayInteractor, ownerOverride);
    }

    public bool TryInteractFromLeftHand(GameObject ownerOverride = null)
    {
        return TryInteractFromRay(leftHandRayInteractor, ownerOverride);
    }

    public bool TryInteractFromRay(XRRayInteractor rayInteractor, GameObject ownerOverride = null)
    {
        IInteractable interactable = FindFocusedInteractable(rayInteractor);
        return TryInteract(interactable, ownerOverride);
    }

    public bool TryInteractFromTransform(Transform rayOrigin, GameObject ownerOverride = null)
    {
        IInteractable interactable = FindFocusedInteractable(rayOrigin);
        return TryInteract(interactable, ownerOverride);
    }

    public void SetPriorityRayOriginOverride(Transform rayOrigin)
    {
        priorityRayOriginOverride = rayOrigin;
    }

    private bool TryInteract(IInteractable interactable, GameObject ownerOverride)
    {
        if (interactable == null)
            return false;

        interactable.Interact(ResolveInteractionOwner(ownerOverride));
        return true;
    }

    private IInteractable FindFocusedInteractable(XRRayInteractor rayInteractor)
    {
        if (rayInteractor == null)
            return null;

        int effectiveMask = interactionMask.value == 0 ? ~0 : interactionMask.value;
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit xriHit) &&
            TryResolveInteractable(xriHit.collider, effectiveMask, out IInteractable xriInteractable))
        {
            return xriInteractable;
        }

        Transform origin = rayInteractor.rayOriginTransform != null
            ? rayInteractor.rayOriginTransform
            : rayInteractor.transform;
        return FindFocusedInteractable(origin);
    }

    private IInteractable FindFocusedInteractable(Transform origin)
    {
        if (origin == null)
            return null;

        int effectiveMask = interactionMask.value == 0 ? ~0 : interactionMask.value;
        int effectiveBlockingMask = blockingMask.value == 0 ? ~0 : blockingMask.value;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin.position,
            interactRadius,
            origin.forward,
            interactDistance,
            effectiveBlockingMask,
            QueryTriggerInteraction.Collide);

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (TryResolveInteractable(hit.collider, effectiveMask, out IInteractable interactable))
                return interactable;

            if (!hit.collider.isTrigger && hit.normal.y < floorBlockNormalY)
                return null;
        }

        return null;
    }

    private GameObject ResolveInteractionOwner(GameObject ownerOverride)
    {
        if (ownerOverride != null)
            return ownerOverride;

        return interactionOwner != null ? interactionOwner : gameObject;
    }

    private static bool TryResolveInteractable(Collider hitCollider, int interactionMask, out IInteractable interactable)
    {
        if (hitCollider.TryGetComponent(out interactable) &&
            IsLayerInMask(hitCollider.gameObject.layer, interactionMask))
        {
            return true;
        }

        foreach (MonoBehaviour behaviour in hitCollider.GetComponentsInParent<MonoBehaviour>())
        {
            if (behaviour is IInteractable parentInteractable &&
                IsLayerInMask(behaviour.gameObject.layer, interactionMask))
            {
                interactable = parentInteractable;
                return true;
            }
        }

        interactable = null;
        return false;
    }

    private static bool WasTriggered(ActionBasedController controller)
    {
        return controller != null && controller.activateInteractionState.activatedThisFrame;
    }

    private void UpdatePrompt()
    {
        if (prompt == null)
            return;

        if (focusedInteractable == null)
        {
            prompt.Hide();
            return;
        }

        prompt.Show(ToTriggerPrompt(focusedInteractable.GetInteractionPrompt()));
    }

    private static string ToTriggerPrompt(string promptText)
    {
        if (string.IsNullOrEmpty(promptText))
            return promptText;

        return promptText
            .Replace("[E]", "[Trigger]")
            .Replace(" E ", " Trigger ")
            .Replace("Press E", "Press Trigger");
    }

    private static bool IsLayerInMask(int layer, int mask)
    {
        return (mask & (1 << layer)) != 0;
    }
}
