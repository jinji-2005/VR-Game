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

    [Header("Audio")]
    [SerializeField] private AudioSource runningAudioSource;

    private InputAction sprintAction;
    private bool enabledSprintActionLocally;
    private XRIHybridDemoDriver hybridDemoDriver;

    public CharacterController BodyController => bodyController;
    public GameObject PlayerTarget => bodyController != null ? bodyController.gameObject : gameObject;
    public bool IsProducingRunNoise { get; private set; }
    public bool IsProducingFootstepNoise { get; private set; }
    public float MovementNoiseStrength { get; private set; }

    private void OnEnable()
    {
        if (bodyController != null)
            bodyController.gameObject.tag = "Player";

        CacheHybridDemoDriver();

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

        StopMovementAudio();
        StopAllNoise();
    }

    private void Update()
    {
        if (hybridDemoDriver == null)
            CacheHybridDemoDriver();

        if (hybridDemoDriver != null && hybridDemoDriver.isActiveAndEnabled)
        {
            ApplyHybridMovementNoise();
            UpdateMovementAudio(IsProducingRunNoise);
            return;
        }

        if (moveProvider == null)
            return;

        bool crouching = bodyController != null && bodyController.height <= crouchHeight + 0.05f;
        bool sprinting = !crouching && sprintAction != null && sprintAction.IsPressed();
        moveProvider.moveSpeed = crouching
            ? crouchSpeed
            : walkSpeed * (sprinting ? sprintMultiplier : 1f);

        UpdateMovementNoise(crouching, sprinting);
        UpdateMovementAudio(sprinting);
    }

    public void DisableLocomotion()
    {
        if (moveProvider != null)
            moveProvider.enabled = false;

        StopMovementAudio();
        StopAllNoise();
    }

    private void StopAllNoise()
    {
        IsProducingRunNoise = false;
        IsProducingFootstepNoise = false;
        MovementNoiseStrength = 0f;
    }

    private void UpdateMovementNoise(bool crouching, bool sprinting)
    {
        if (bodyController == null)
        {
            StopAllNoise();
            return;
        }

        Vector3 movement = bodyController.velocity;
        movement.y = 0f;
        bool audibleMovement = !crouching && bodyController.isGrounded && movement.magnitude > 0.2f;
        if (!audibleMovement)
        {
            StopAllNoise();
            return;
        }

        float baseNoise = sprinting ? 1f : 0.68f;
        float inputMagnitude = Mathf.Clamp01(movement.magnitude / Mathf.Max(walkSpeed, 0.01f));
        MovementNoiseStrength = baseNoise * Mathf.Lerp(0.55f, 1f, inputMagnitude);
        IsProducingFootstepNoise = MovementNoiseStrength > 0.05f;
        IsProducingRunNoise = sprinting && IsProducingFootstepNoise;
    }

    private void ApplyHybridMovementNoise()
    {
        IsProducingRunNoise = hybridDemoDriver.IsProducingRunNoise;
        IsProducingFootstepNoise = hybridDemoDriver.IsProducingFootstepNoise;
        MovementNoiseStrength = hybridDemoDriver.MovementNoiseStrength;
    }

    private void UpdateMovementAudio(bool sprinting)
    {
        if (runningAudioSource == null)
            return;

        bool shouldRunSound = sprinting && IsProducingFootstepNoise;
        if (shouldRunSound && !runningAudioSource.isPlaying)
        {
            runningAudioSource.loop = true;
            runningAudioSource.Play();
        }

        float targetVolume = shouldRunSound ? 1f : 0f;
        runningAudioSource.volume = Mathf.Lerp(
            runningAudioSource.volume,
            targetVolume,
            Time.deltaTime * 12f);
    }

    private void CacheHybridDemoDriver()
    {
        hybridDemoDriver = GetComponent<XRIHybridDemoDriver>() ??
            GetComponentInParent<XRIHybridDemoDriver>() ??
            GetComponentInChildren<XRIHybridDemoDriver>(true);
    }

    private void StopMovementAudio()
    {
        if (runningAudioSource == null)
            return;

        runningAudioSource.Stop();
        runningAudioSource.volume = 0f;
    }
}
