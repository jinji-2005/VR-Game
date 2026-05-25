using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class XRIOfficialPlayerTuning : MonoBehaviour
{
    [SerializeField] private ContinuousMoveProviderBase moveProvider;
    [SerializeField] private CharacterController bodyController;
    [SerializeField] private InputActionAsset xriInputActions;
    [SerializeField] private string sprintActionPath = "XRI LeftHand Interaction/Select";

    [Header("Desktop-Matched Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchSpeed = 2f;

    private InputAction sprintAction;
    private bool enabledSprintActionLocally;

    private void OnEnable()
    {
        sprintAction = xriInputActions != null ? xriInputActions.FindAction(sprintActionPath) : null;
        if (sprintAction != null && !sprintAction.enabled)
        {
            sprintAction.Enable();
            enabledSprintActionLocally = true;
        }
    }

    private void OnDisable()
    {
        if (enabledSprintActionLocally && sprintAction != null)
            sprintAction.Disable();

        sprintAction = null;
        enabledSprintActionLocally = false;

        if (moveProvider != null)
            moveProvider.moveSpeed = walkSpeed;
    }

    private void Update()
    {
        if (moveProvider == null)
            return;

        bool crouching = bodyController != null && bodyController.height <= crouchHeight + 0.05f;
        bool sprinting = !crouching && sprintAction != null && sprintAction.IsPressed();
        moveProvider.moveSpeed = crouching
            ? crouchSpeed
            : walkSpeed * (sprinting ? sprintMultiplier : 1f);
    }
}
