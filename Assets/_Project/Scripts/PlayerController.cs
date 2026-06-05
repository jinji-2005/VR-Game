using UnityEngine;

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

    public bool IsProducingRunNoise { get; private set; }
    public bool IsProducingFootstepNoise { get; private set; }
    public float MovementNoiseStrength { get; private set; }
    public bool IsCrouching => isCrouching;

    public void StopAllNoise()
    {
        IsProducingFootstepNoise = false;
        MovementNoiseStrength = 0f;

        if (runningAudioSource != null)
        {
            runningAudioSource.Stop();
            runningAudioSource.volume = 0f;
        }
    }

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
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
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

        bool isMoving = move.magnitude > 0.2f;
        bool isRecentlyGrounded =
            Time.time - lastGroundedTime < groundedGraceTime;
        bool canJump = characterController.isGrounded || isRecentlyGrounded;
        bool isAudibleMovement =
            !isCrouching &&
            isMoving &&
            isRecentlyGrounded &&
            velocity.y <= 0.1f;
        float inputMagnitude = Mathf.Clamp01(new Vector2(moveX, moveZ).magnitude);

        if (isAudibleMovement)
        {
            float baseNoise =
                isSprinting ? 1f :
                isCrouching ? 0.28f :
                0.68f;

            MovementNoiseStrength = baseNoise * Mathf.Lerp(0.55f, 1f, inputMagnitude);
            IsProducingFootstepNoise = MovementNoiseStrength > 0.05f;
        }
        else
        {
            MovementNoiseStrength = 0f;
            IsProducingFootstepNoise = false;
        }

        // running sound: hold Shift + moving + grounded → loop
        if (runningAudioSource != null)
        {
            bool shouldRunSound =
                isSprinting &&
                isAudibleMovement;

            IsProducingRunNoise = shouldRunSound;

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
        else
        {
            IsProducingRunNoise = false;
        }

        if (Input.GetKeyDown(KeyCode.Space))
            lastJumpPressTime = Time.time;

        if (characterController.isGrounded)
        {   

            if (velocity.y < 0f)
                velocity.y = -2f;

        }
        if (canJump && Time.time - lastJumpPressTime < 0.15f)
        {
            velocity.y = jumpForce;
            lastJumpPressTime = float.MinValue;
        }
        

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        bool wantsToCrouch = Input.GetKey(KeyCode.C);
        float targetHeight = wantsToCrouch ? crouchHeight : standingHeight;
        float smoothHeight = Mathf.Lerp(characterController.height, targetHeight, Time.deltaTime * 10f);
        isCrouching = wantsToCrouch || smoothHeight < standingHeight - 0.05f;
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
}
