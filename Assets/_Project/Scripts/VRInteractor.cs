using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

public class VRInteractor : MonoBehaviour
{
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private Transform fallbackCameraTransform;
    [SerializeField] private float interactDistance = 4f;
    [SerializeField] private float interactRadius = 0.08f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField] private float floorBlockNormalY = 0.6f;
    [SerializeField] private InteractionPrompt prompt;
    [SerializeField] private LineRenderer rayLine;

    [Header("Input")]
    [SerializeField] private InputActionAsset xriInputActions;
    [SerializeField] private string xriActivateActionPath = "XRI RightHand Interaction/Activate";
    [SerializeField] private bool allowConvenienceTriggerInputInDemoMode = true;

    private IInteractable focusedInteractable;
    private bool wasInteractPressed;
    private InputAction xriActivateAction;
    private bool enabledActivateActionLocally;

    private void Reset()
    {
        fallbackCameraTransform = GetComponentInChildren<Camera>()?.transform;
    }

    private void OnEnable()
    {
        xriActivateAction = xriInputActions != null ? xriInputActions.FindAction(xriActivateActionPath) : null;
        if (xriActivateAction != null && !xriActivateAction.enabled)
        {
            xriActivateAction.Enable();
            enabledActivateActionLocally = true;
        }
    }

    private void OnDisable()
    {
        if (enabledActivateActionLocally && xriActivateAction != null)
            xriActivateAction.Disable();

        xriActivateAction = null;
        enabledActivateActionLocally = false;
        wasInteractPressed = false;
    }

    private void Update()
    {
        Transform origin = GetRayOrigin();
        if (origin == null)
        {
            focusedInteractable = null;
            HideRay();
            return;
        }

        focusedInteractable = FindFocusedInteractable(origin, out Vector3 rayEnd);
        UpdateRay(origin.position, rayEnd, focusedInteractable != null);
        UpdatePrompt();
        HandleInteractionInput();
    }

    private Transform GetRayOrigin()
    {
        if (VRDemoSimulator.IsDemoModeActive)
            return rayOrigin != null ? rayOrigin : fallbackCameraTransform;

        if (rayOrigin != null)
        {
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 position))
                    rayOrigin.localPosition = position;

                if (device.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rotation))
                    rayOrigin.localRotation = rotation;

                return rayOrigin;
            }
        }

        XRInputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        return headDevice.isValid ? fallbackCameraTransform : null;
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

        prompt.Show(ToVrPrompt(focusedInteractable.GetInteractionPrompt()));
    }

    private void HandleInteractionInput()
    {
        bool isInteractPressed = IsInteractPressed();
        bool pressedThisFrame = isInteractPressed && !wasInteractPressed;
        wasInteractPressed = isInteractPressed;

        if (!pressedThisFrame || focusedInteractable == null)
            return;

        focusedInteractable.Interact(gameObject);
    }

    private bool IsInteractPressed()
    {
        if (allowConvenienceTriggerInputInDemoMode && VRDemoSimulator.IsDemoModeActive &&
            (Input.GetMouseButton(0) || Input.GetKey(KeyCode.E)))
        {
            return true;
        }

        if (xriActivateAction != null && xriActivateAction.IsPressed())
        {
            return true;
        }

        if (VRDemoSimulator.IsDemoModeActive)
            return false;

        XRInputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid)
            return false;

        return TryGetButton(device, XRCommonUsages.triggerButton);
    }

    private IInteractable FindFocusedInteractable(Transform origin, out Vector3 rayEnd)
    {
        Vector3 start = origin.position;
        Vector3 direction = origin.forward;
        rayEnd = start + direction * interactDistance;

        int effectiveBlockingMask = GetEffectiveMask(blockingMask);
        int effectiveInteractionMask = GetEffectiveMask(interactionMask);

        if (Physics.Raycast(
                start,
                direction,
                out RaycastHit rayHit,
                interactDistance,
                effectiveBlockingMask,
                QueryTriggerInteraction.Collide))
        {
            rayEnd = rayHit.point;
            if (!IsOwnCollider(rayHit.collider) &&
                TryResolveInteractable(rayHit.collider, effectiveInteractionMask, out IInteractable rayInteractable))
            {
                return rayInteractable;
            }
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            start,
            interactRadius,
            direction,
            interactDistance,
            effectiveBlockingMask,
            QueryTriggerInteraction.Collide);

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider))
                continue;

            if (TryResolveInteractable(hit.collider, effectiveInteractionMask, out IInteractable interactable))
            {
                rayEnd = hit.point;
                return interactable;
            }

            if (!hit.collider.isTrigger && !IsFloorLikeBlocker(hit))
            {
                rayEnd = hit.point;
                return null;
            }
        }

        return null;
    }

    private bool IsFloorLikeBlocker(RaycastHit hit)
    {
        return hit.normal.y >= floorBlockNormalY;
    }

    private void UpdateRay(Vector3 start, Vector3 end, bool hasTarget)
    {
        if (rayLine == null)
            return;

        rayLine.enabled = true;
        rayLine.positionCount = 2;
        rayLine.SetPosition(0, start);
        rayLine.SetPosition(1, end);

        Color color = hasTarget ? new Color(0.3f, 1f, 0.6f, 0.95f) : new Color(1f, 1f, 1f, 0.45f);
        rayLine.startColor = color;
        rayLine.endColor = new Color(color.r, color.g, color.b, 0.05f);
    }

    private void HideRay()
    {
        if (rayLine != null)
            rayLine.enabled = false;
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        Transform hitTransform = hitCollider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
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

    private static string ToVrPrompt(string promptText)
    {
        if (string.IsNullOrEmpty(promptText))
            return promptText;

        return promptText
            .Replace("[E]", "[Trigger]")
            .Replace(" E ", " Trigger ")
            .Replace("Press E", "Press Trigger");
    }

    private static int GetEffectiveMask(LayerMask mask)
    {
        return mask.value == 0 ? ~0 : mask.value;
    }

    private static bool IsLayerInMask(int layer, int mask)
    {
        return (mask & (1 << layer)) != 0;
    }

    private static bool TryGetButton(XRInputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }
}
