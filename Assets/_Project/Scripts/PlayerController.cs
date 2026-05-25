using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float jumpForce = 4f;
    [SerializeField] private Transform cameraTransform;

    [Header("Collision")]
    [SerializeField] private float controllerRadius = 0.32f;
    [SerializeField] private float cameraNearClipPlane = 0.05f;
    [SerializeField] private bool limitCameraPlanarOffset = true;
    [SerializeField] private float cameraPlanarPadding = 0.08f;

    [Header("Crouch")]
    [SerializeField] private float crouchHeight = 1.0f;
    [SerializeField] private float crouchSpeed = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource runningAudioSource;

    [Header("XR")]
    [SerializeField] private bool disableDesktopInputWhenXRActive = true;

    private CharacterController characterController;
    private Vector3 velocity;
    private float pitch;
    private float lastJumpPressTime = float.MinValue;
    private float standingHeight;
    private float standingCenterY;
    private float initialCamLocalY;
    private bool isCrouching;

    private float lastGroundedTime;
    [SerializeField] private float groundedGraceTime = 0.15f;

    public float WalkSpeed => walkSpeed;
    public float SprintMultiplier => sprintMultiplier;
    public float Gravity => gravity;
    public float JumpForce => jumpForce;
    public float CrouchHeight => crouchHeight;
    public float CrouchSpeed => crouchSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        ApplyCollisionSettings();
    }

    private void Start()
    {
        standingHeight = characterController.height;
        standingCenterY = characterController.center.y;
        if (cameraTransform != null)
            initialCamLocalY = cameraTransform.localPosition.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (VRDemoSimulator.IsDemoModeActive)
        {
            LimitCameraPlanarOffset();
            return;
        }

        if (disableDesktopInputWhenXRActive && IsXRDeviceActive())
        {
            LimitCameraPlanarOffset();
            return;
        }

        HandleMouseLook();
        HandleMovement();
        HandleCrouch();
        LimitCameraPlanarOffset();
    }

    private void HandleMouseLook()
    {
        if (cameraTransform == null)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        pitch = Mathf.Clamp(pitch - mouseY, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        if (characterController.isGrounded)
        {
            lastGroundedTime = Time.time;

            if (velocity.y < 0f)
                velocity.y = -2f;

            if (Time.time - lastJumpPressTime < 0.15f)
            {
                velocity.y = jumpForce;
                lastJumpPressTime = float.MinValue;
            }
        }

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        float speed = isCrouching ? crouchSpeed : walkSpeed;
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching;
        if (isSprinting)
            speed *= sprintMultiplier;

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        characterController.Move(move * (speed * Time.deltaTime));

        // running sound: hold Shift + moving + grounded → loop
        if (runningAudioSource != null)
        {
            bool isMoving = move.magnitude > 0.2f;
            bool isRecentlyGrounded =
                Time.time - lastGroundedTime < groundedGraceTime;

            bool shouldRunSound =
                isSprinting &&
                isMoving &&
                isRecentlyGrounded &&
                velocity.y <= 0.1f;

            if (!runningAudioSource.isPlaying)
            {
                runningAudioSource.loop = true;
                runningAudioSource.Play();
            }

            float targetVolume = shouldRunSound ? 1f : 0f;

            runningAudioSource.volume = Mathf.Lerp(
                runningAudioSource.volume,
                targetVolume,
                Time.deltaTime * 12f
            );
        }

        if (Input.GetKeyDown(KeyCode.Space))
            lastJumpPressTime = Time.time;

        if (characterController.isGrounded)
        {
            if (velocity.y < 0f)
                velocity.y = -2f;

            if (Time.time - lastJumpPressTime < 0.15f)
            {
                velocity.y = jumpForce;
                lastJumpPressTime = float.MinValue;
            }
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        float targetHeight = Input.GetKey(KeyCode.C) ? crouchHeight : standingHeight;
        float smoothHeight = Mathf.Lerp(characterController.height, targetHeight, Time.deltaTime * 10f);
        isCrouching = smoothHeight < standingHeight - 0.05f;
        characterController.height = smoothHeight;

        float ratio = smoothHeight / standingHeight;
        characterController.center = new Vector3(0, standingCenterY * ratio, 0);

        if (cameraTransform != null)
        {
            Vector3 cp = cameraTransform.localPosition;
            cp.y = initialCamLocalY - (standingHeight - smoothHeight);
            cameraTransform.localPosition = cp;
        }
    }

    private void ApplyCollisionSettings()
    {
        characterController.radius = Mathf.Max(0.1f, controllerRadius);

        if (cameraTransform != null && cameraTransform.TryGetComponent(out Camera playerCamera))
            playerCamera.nearClipPlane = cameraNearClipPlane;
    }

    private void LimitCameraPlanarOffset()
    {
        if (!limitCameraPlanarOffset || cameraTransform == null)
            return;

        float maxPlanarOffset = Mathf.Max(0f, characterController.radius - cameraPlanarPadding);
        Vector3 localPosition = cameraTransform.localPosition;
        Vector2 planarOffset = new Vector2(localPosition.x, localPosition.z);

        if (planarOffset.sqrMagnitude <= maxPlanarOffset * maxPlanarOffset)
            return;

        Vector2 clampedOffset = planarOffset.normalized * maxPlanarOffset;
        cameraTransform.localPosition = new Vector3(clampedOffset.x, localPosition.y, clampedOffset.y);
    }

    private static bool IsXRDeviceActive()
    {
        if (XRSettings.isDeviceActive)
            return true;

        InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        return headDevice.isValid;
    }
}
