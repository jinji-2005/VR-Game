using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(CharacterController))]
public class VRRigDriver : MonoBehaviour
{
    [Header("Tracking")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;
    [SerializeField] private float fallbackEyeHeight = 1.65f;

    [Header("Movement")]
    [SerializeField] private PlayerController desktopMovementSettings;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float jumpForce = 4f;
    [SerializeField] private float bodyFollowThreshold = 0.12f;

    [Header("Crouch")]
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchSpeed = 2f;

    [Header("Turn")]
    [SerializeField] private bool useSnapTurn = true;
    [SerializeField] private float snapTurnAngle = 30f;
    [SerializeField] private float snapTurnCooldown = 0.35f;
    [SerializeField] private float smoothTurnSpeed = 75f;
    [SerializeField] private float turnDeadzone = 0.65f;

    [Header("Audio")]
    [SerializeField] private AudioSource runningAudioSource;

    [Header("Demo Simulation")]
    [SerializeField] private bool allowKeyboardDemoSimulation = true;
    [SerializeField] private bool autoInstallDemoSimulator = true;
    [SerializeField] private float demoMouseSensitivity = 2f;
    [SerializeField] private Vector3 demoLeftHandLocalPosition = new Vector3(-0.22f, 1.42f, 0.42f);
    [SerializeField] private Vector3 demoRightHandLocalPosition = new Vector3(0.22f, 1.42f, 0.42f);

    private CharacterController characterController;
    private Vector3 velocity;
    private float nextSnapTurnTime;
    private float lastJumpPressTime = float.MinValue;
    private bool previousJumpPressed;
    private float demoPitch;
    private float standingHeight;
    private float standingCenterY;
    private float initialCameraLocalY;
    private bool isCrouching;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (desktopMovementSettings == null)
            desktopMovementSettings = GetComponent<PlayerController>();

        if (autoInstallDemoSimulator && GetComponent<VRDemoSimulator>() == null)
            gameObject.AddComponent<VRDemoSimulator>();
    }

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        cameraTransform = GetComponentInChildren<Camera>()?.transform;
    }

    private void Start()
    {
        standingHeight = characterController.height;
        standingCenterY = characterController.center.y;
        if (cameraTransform != null)
            initialCameraLocalY = cameraTransform.localPosition.y;

        if (cameraTransform != null && cameraTransform.localPosition == Vector3.zero)
            cameraTransform.localPosition = new Vector3(0f, fallbackEyeHeight, 0f);
    }

    private void Update()
    {
        if (IsDemoSimulationActive())
        {
            UpdateDemoRig();
            HandleCrouch(Input.GetKey(KeyCode.C), true);
            HandleDemoMovement();
            return;
        }

        bool hasHeadPose = UpdateNodePose(XRNode.CenterEye, cameraTransform, fallbackEyeHeight);
        UpdateNodePose(XRNode.LeftHand, leftHandTransform, 1.25f);
        UpdateNodePose(XRNode.RightHand, rightHandTransform, 1.25f);

        if (!hasHeadPose)
            return;

        FollowHeadPlanarOffset();
        HandleTurn();
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        HandleCrouch(TryGetButton(leftHand, CommonUsages.secondaryButton), false);
        HandleMovement();
    }

    private bool IsDemoSimulationActive()
    {
        return allowKeyboardDemoSimulation && VRDemoSimulator.IsDemoModeActive;
    }

    private void UpdateDemoRig()
    {
        if (cameraTransform != null)
        {
            float mouseX = Input.GetAxis("Mouse X") * demoMouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * demoMouseSensitivity;

            demoPitch = Mathf.Clamp(demoPitch - mouseY, -80f, 80f);
            transform.Rotate(Vector3.up * mouseX);
            cameraTransform.localRotation = Quaternion.Euler(demoPitch, 0f, 0f);

            Vector3 cameraLocalPosition = cameraTransform.localPosition;
            if (cameraLocalPosition == Vector3.zero)
                cameraTransform.localPosition = new Vector3(0f, fallbackEyeHeight, 0f);
        }

        if (leftHandTransform != null)
            leftHandTransform.localPosition = demoLeftHandLocalPosition;

        if (rightHandTransform != null)
        {
            rightHandTransform.localPosition = demoRightHandLocalPosition;
            rightHandTransform.rotation = cameraTransform != null ? cameraTransform.rotation : transform.rotation;
        }
    }

    private void HandleDemoMovement()
    {
        bool sprintPressed = Input.GetKey(KeyCode.LeftShift);
        bool jumpPressed = Input.GetKey(KeyCode.Space);

        if (jumpPressed && !previousJumpPressed)
            lastJumpPressTime = Time.time;

        previousJumpPressed = jumpPressed;

        ApplyGroundingAndJump();

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 move = right * moveX + forward * moveZ;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        float speed = GetMoveSpeed(sprintPressed);
        characterController.Move(move * (speed * Time.deltaTime));

        velocity.y += GetGravity() * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);

        UpdateRunningAudio(move, sprintPressed);
    }

    private bool UpdateNodePose(XRNode node, Transform target, float fallbackHeight)
    {
        if (target == null)
            return false;

        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
        {
            if (node == XRNode.CenterEye && target.localPosition == Vector3.zero)
                target.localPosition = new Vector3(0f, fallbackHeight, 0f);

            return false;
        }

        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
        bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);

        if (hasPosition)
            target.localPosition = position;

        if (hasRotation)
            target.localRotation = rotation;

        return hasPosition || hasRotation;
    }

    private void FollowHeadPlanarOffset()
    {
        if (cameraTransform == null)
            return;

        Vector3 localHead = cameraTransform.localPosition;
        Vector3 planarOffset = new Vector3(localHead.x, 0f, localHead.z);
        if (planarOffset.magnitude <= bodyFollowThreshold)
            return;

        Vector3 worldOffset = transform.TransformVector(planarOffset);
        characterController.Move(worldOffset);
        cameraTransform.localPosition = new Vector3(0f, localHead.y, 0f);
    }

    private void HandleMovement()
    {
        ApplyGroundingAndJump();

        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        leftHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 moveInput);
        bool sprintPressed = TryGetButton(leftHand, CommonUsages.gripButton);
        bool jumpPressed = TryGetButton(leftHand, CommonUsages.primaryButton);

        if (jumpPressed && !previousJumpPressed)
            lastJumpPressTime = Time.time;

        previousJumpPressed = jumpPressed;

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 move = right * moveInput.x + forward * moveInput.y;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        float speed = GetMoveSpeed(sprintPressed);
        characterController.Move(move * (speed * Time.deltaTime));

        velocity.y += GetGravity() * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);

        UpdateRunningAudio(move, sprintPressed);
    }

    private void ApplyGroundingAndJump()
    {
        if (!characterController.isGrounded)
            return;

        if (velocity.y < 0f)
            velocity.y = -2f;

        if (Time.time - lastJumpPressTime < 0.15f)
        {
            velocity.y = GetJumpForce();
            lastJumpPressTime = float.MinValue;
        }
    }

    private void HandleCrouch(bool crouchPressed, bool moveCamera)
    {
        float configuredCrouchHeight = desktopMovementSettings != null ? desktopMovementSettings.CrouchHeight : crouchHeight;
        float targetHeight = crouchPressed ? configuredCrouchHeight : standingHeight;
        float smoothHeight = Mathf.Lerp(characterController.height, targetHeight, Time.deltaTime * 10f);
        isCrouching = smoothHeight < standingHeight - 0.05f;
        characterController.height = smoothHeight;

        float ratio = smoothHeight / standingHeight;
        characterController.center = new Vector3(0f, standingCenterY * ratio, 0f);

        if (!moveCamera || cameraTransform == null)
            return;

        Vector3 cameraPosition = cameraTransform.localPosition;
        cameraPosition.y = initialCameraLocalY - (standingHeight - smoothHeight);
        cameraTransform.localPosition = cameraPosition;
    }

    private float GetMoveSpeed(bool sprintPressed)
    {
        float configuredWalkSpeed = desktopMovementSettings != null ? desktopMovementSettings.WalkSpeed : moveSpeed;
        float configuredCrouchSpeed = desktopMovementSettings != null ? desktopMovementSettings.CrouchSpeed : crouchSpeed;
        float configuredSprintMultiplier = desktopMovementSettings != null ? desktopMovementSettings.SprintMultiplier : sprintMultiplier;

        if (isCrouching)
            return configuredCrouchSpeed;

        return sprintPressed ? configuredWalkSpeed * configuredSprintMultiplier : configuredWalkSpeed;
    }

    private float GetGravity()
    {
        return desktopMovementSettings != null ? desktopMovementSettings.Gravity : gravity;
    }

    private float GetJumpForce()
    {
        return desktopMovementSettings != null ? desktopMovementSettings.JumpForce : jumpForce;
    }

    private void HandleTurn()
    {
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 turnInput);

        if (useSnapTurn)
        {
            if (Mathf.Abs(turnInput.x) < turnDeadzone || Time.time < nextSnapTurnTime)
                return;

            RotateRig(turnInput.x > 0f ? snapTurnAngle : -snapTurnAngle);
            nextSnapTurnTime = Time.time + snapTurnCooldown;
            return;
        }

        if (Mathf.Abs(turnInput.x) > 0.1f)
            RotateRig(turnInput.x * smoothTurnSpeed * Time.deltaTime);
    }

    private void RotateRig(float degrees)
    {
        Vector3 pivot = cameraTransform != null ? cameraTransform.position : transform.position;
        transform.RotateAround(pivot, Vector3.up, degrees);
    }

    private void UpdateRunningAudio(Vector3 move, bool sprintPressed)
    {
        if (runningAudioSource == null)
            return;

        bool shouldRunSound = sprintPressed && move.magnitude > 0.2f && characterController.isGrounded;

        if (!runningAudioSource.isPlaying)
        {
            runningAudioSource.loop = true;
            runningAudioSource.Play();
        }

        float targetVolume = shouldRunSound ? 1f : 0f;
        runningAudioSource.volume = Mathf.Lerp(runningAudioSource.volume, targetVolume, Time.deltaTime * 12f);
    }

    private static bool TryGetButton(InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }
}
