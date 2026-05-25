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

    private void Update()
    {
        IInteractable rightFocusedInteractable = FindFocusedInteractable(rightHandRayInteractor);
        IInteractable leftFocusedInteractable = FindFocusedInteractable(leftHandRayInteractor);
        focusedInteractable = rightFocusedInteractable ?? leftFocusedInteractable;
        UpdatePrompt();

        if (rightFocusedInteractable != null && WasTriggered(rightHandController))
            rightFocusedInteractable.Interact(interactionOwner != null ? interactionOwner : gameObject);
        else if (leftFocusedInteractable != null && WasTriggered(leftHandController))
            leftFocusedInteractable.Interact(interactionOwner != null ? interactionOwner : gameObject);
    }

    private void OnDisable()
    {
        focusedInteractable = null;
        if (prompt != null)
            prompt.Hide();
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
