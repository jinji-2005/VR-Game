using System;
using UnityEngine;
using UnityEngine.XR;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float interactRadius = 0.12f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField] private float floorBlockNormalY = 0.6f;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InteractionPrompt prompt;

    private IInteractable focusedInteractable;

    private void Update()
    {
        if (VRDemoSimulator.IsDemoModeActive || IsXRInputActive())
            return;

        focusedInteractable = FindFocusedInteractable();
        HandleInteractionDetection();
        HandleInteractionInput();
    }

    private void HandleInteractionDetection()
    {
        if (focusedInteractable != null)
        {
            if (prompt != null)
                prompt.Show(focusedInteractable.GetInteractionPrompt());
            return;
        }

        if (prompt != null)
            prompt.Hide();
    }

    private void HandleInteractionInput()
    {
        if (!Input.GetKeyDown(KeyCode.E) || focusedInteractable == null)
            return;

        focusedInteractable.Interact(gameObject);
    }

    private IInteractable FindFocusedInteractable()
    {
        if (cameraTransform == null)
            return null;

        int effectiveBlockingMask = GetEffectiveMask(blockingMask);
        int effectiveInteractionMask = GetEffectiveMask(interactionMask);

        if (Physics.Raycast(
                cameraTransform.position,
                cameraTransform.forward,
                out RaycastHit rayHit,
                interactDistance,
                effectiveBlockingMask,
                QueryTriggerInteraction.Collide))
        {
            if (!IsOwnCollider(rayHit.collider) &&
                TryResolveInteractable(rayHit.collider, effectiveInteractionMask, out IInteractable rayInteractable))
            {
                return rayInteractable;
            }
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            cameraTransform.position,
            interactRadius,
            cameraTransform.forward,
            interactDistance,
            effectiveBlockingMask,
            QueryTriggerInteraction.Collide);

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider))
                continue;

            if (TryResolveInteractable(hit.collider, effectiveInteractionMask, out IInteractable interactable))
                return interactable;

            if (!hit.collider.isTrigger && !IsFloorLikeBlocker(hit))
                return null;
        }

        return null;
    }

    private bool IsFloorLikeBlocker(RaycastHit hit)
    {
        return hit.normal.y >= floorBlockNormalY;
    }

    private static bool TryResolveInteractable(Collider hitCollider, int interactionMask, out IInteractable interactable)
    {
        if (hitCollider.TryGetComponent(out interactable) && IsLayerInMask(hitCollider.gameObject.layer, interactionMask))
            return true;

        foreach (MonoBehaviour behaviour in hitCollider.GetComponentsInParent<MonoBehaviour>())
        {
            if (!(behaviour is IInteractable parentInteractable))
                continue;

            if (!IsLayerInMask(behaviour.gameObject.layer, interactionMask))
                continue;

            interactable = parentInteractable;
            return true;
        }

        interactable = null;
        return false;
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        Transform hitTransform = hitCollider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    private static int GetEffectiveMask(LayerMask mask)
    {
        return mask.value == 0 ? ~0 : mask.value;
    }

    private static bool IsLayerInMask(int layer, int mask)
    {
        return (mask & (1 << layer)) != 0;
    }

    private static bool IsXRInputActive()
    {
        if (XRSettings.isDeviceActive)
            return true;

        InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        return headDevice.isValid || rightHandDevice.isValid;
    }
}
