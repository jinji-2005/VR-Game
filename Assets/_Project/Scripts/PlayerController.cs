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

    [Header("Crouch")]
    [SerializeField] private float crouchHeight = 1.0f;
    [SerializeField] private float crouchSpeed = 2f;

    private CharacterController characterController;
    private Vector3 velocity;
    private float pitch;
    private float lastJumpPressTime = float.MinValue;
    private float standingHeight;
    private float standingCenterY;
    private float initialCamLocalY;
    private bool isCrouching;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
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
        HandleMovement();
        HandleCrouch();
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        pitch = Mathf.Clamp(pitch - mouseY, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        float speed = isCrouching ? crouchSpeed : walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) && !isCrouching)
            speed *= sprintMultiplier;

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        characterController.Move(move * (speed * Time.deltaTime));

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
}
