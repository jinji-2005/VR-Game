using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(1000)]
public class XRIHybridDemoDriver : MonoBehaviour
{
    private static readonly Vector3 OfficialLeftControllerRestOffset = new Vector3(-0.1f, -0.05f, 0.3f);
    private static readonly Vector3 OfficialRightControllerRestOffset = new Vector3(0.1f, -0.05f, 0.3f);

    [Header("References")]
    [SerializeField] private CharacterController bodyController;
    [SerializeField] private Camera xriCamera;
    [SerializeField] private ActionBasedController leftHandController;
    [SerializeField] private ActionBasedController rightHandController;
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;
    [SerializeField] private XRIOfficialInteractableBridge interactableBridge;
    [SerializeField] private GameObject interactionOwner;
    [SerializeField] private GameObject leftControllerVisualPrefab;
    [SerializeField] private GameObject rightControllerVisualPrefab;

    [Header("Desktop-Matched Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float jumpForce = 4f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchSpeed = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource runningAudioSource;
    [SerializeField] private AudioSource jumpAudioSource;

    [Header("Pose Normalization")]
    [SerializeField] private bool lockCameraLocalPose = true;
    [SerializeField] private bool resetCameraOffsetParent = true;
    [SerializeField] private float cameraHeightOffset = 0.18f;
    [SerializeField] private float minimumCameraHeight = 1.82f;
    [SerializeField] private bool logPoseHeightsOnEnable = true;
    [SerializeField] private Vector3 cameraLocalPosition = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector3 cameraLocalEuler = Vector3.zero;

    [Header("Demo Hands")]
    [SerializeField] private bool useOfficialDeviceSimulatorRestPose = true;
    [SerializeField] private Vector3 leftHandCameraOffset = new Vector3(-0.28f, -0.28f, 0.55f);
    [SerializeField] private Vector3 rightHandCameraOffset = new Vector3(0.28f, -0.32f, 0.65f);
    [SerializeField] private float handAimDistance = 8f;
    [SerializeField] private float controllerVisualPoseScale = 1f;
    [SerializeField] private float controllerVisualScale = 1f;
    [SerializeField] private Vector3 controllerVisualLocalOffset = new Vector3(0f, 0f, -0.05f);
    [SerializeField] private Vector3 controllerVisualLocalEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool updatePoseBeforeRender = true;
    [SerializeField] private bool driveOfficialRayVisual = true;
    [SerializeField] private float officialRayLength = 10f;
    [SerializeField] private float officialRayLengthChangeSpeed = 12f;
    [SerializeField] private float officialLineVisualWidth = 0.005f;
    [SerializeField] private float officialLineRendererWidth = 0.005f;
    [SerializeField] private int hybridRaySegmentCount = 16;
    [SerializeField] private float fallbackRayRibbonWidth = 0.005f;
    [SerializeField] private bool smoothOfficialRayFollow = true;
    [SerializeField] private float officialRayFollowTightness = 10f;
    [SerializeField] private float officialRaySnapThresholdDistance = 10f;
    [SerializeField] private bool preferOfficialRayVisual = true;
    [SerializeField] private bool fallbackWhenOfficialRayHidden = true;
    [SerializeField] private bool useFallbackRayRibbon = false;
    [SerializeField] private float rayVisualDistanceScale = 1f;
    [SerializeField] private LayerMask officialRayMask = ~0;

    private readonly List<Behaviour> disabledLocomotionBehaviours = new List<Behaviour>();
    private readonly List<Behaviour> disabledOfficialInputBehaviours = new List<Behaviour>();
    private readonly DrivenRayState leftDrivenRay = new DrivenRayState();
    private readonly DrivenRayState rightDrivenRay = new DrivenRayState();
    private Vector3 velocity;
    private float pitch;
    private float standingHeight;
    private float standingCenterY;
    private Vector3 initialCameraLocalPosition;
    private bool hasDefaults;
    private bool isCrouching;
    private bool inputFrozen;
    private float lastJumpPressTime = float.MinValue;
    private GameObject leftControllerVisualInstance;
    private GameObject rightControllerVisualInstance;
    private Transform leftControllerVisualAnchor;
    private Transform rightControllerVisualAnchor;
    private bool loggedLeftVisualStatus;
    private bool loggedRightVisualStatus;
    private bool loggedRayVisualStatus;
    private bool loggedPoseHeights;
    private bool subscribedBeforeRender;
    private Material drivenRayMaterial;
    private Material defaultLineMaterial;

    private sealed class DrivenRayState
    {
        public readonly List<LineRenderer> Lines = new List<LineRenderer>();
        public readonly List<LineRenderer> OfficialLines = new List<LineRenderer>();
        public GameObject HybridOfficialLineObject;
        public XRIHybridRayProvider HybridOfficialLineProvider;
        public XRInteractorLineVisual HybridOfficialLineVisual;
        public LineRenderer HybridOfficialLineRenderer;
        public Material SourceOfficialLineMaterial;
        public Vector3[] HybridLinePoints = new Vector3[2];
        public Vector3[] HybridRenderPoints = new Vector3[16];
        public LineRenderer FallbackLine;
        public GameObject FallbackBeam;
        public MeshRenderer FallbackBeamRenderer;
        public GameObject FallbackRibbon;
        public MeshFilter FallbackRibbonMeshFilter;
        public MeshRenderer FallbackRibbonRenderer;
        public Mesh FallbackRibbonMesh;
        public float CurrentLength;
        public Vector3 CurrentRenderOrigin;
        public Vector3 CurrentRenderEnd;
        public bool HasRenderPoints;
        public bool HasOfficialLineVisual;
        public bool LoggedRuntimeStatus;
        public bool LoggedHybridLineStatus;
        public bool LoggedOfficialHiddenStatus;
        public bool LoggedLineVisualUpdateFailure;
    }

    public static XRIHybridDemoDriver ActiveDriver { get; private set; }
    public bool IsProducingRunNoise { get; private set; }
    public bool IsProducingFootstepNoise { get; private set; }
    public float MovementNoiseStrength { get; private set; }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        DisableOfficialLocomotion();
        DisableOfficialInputDrivers();
        NormalizeHybridRigPose();
        CacheDefaults();
        PreparePlayerTarget();
        EnsureOfficialControllerModels();
        EnsureOfficialRayVisuals();
        SetBridgeAimOverride();
        inputFrozen = false;
        loggedPoseHeights = false;
        ActiveDriver = this;
        SubscribeBeforeRender();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Hybrid XRI demo using provider-backed official ray LineRenderers.");
        RefreshDemoPoseAndRayVisuals();
    }

    private void OnDisable()
    {
        UnsubscribeBeforeRender();

        if (interactableBridge != null)
            interactableBridge.SetPriorityRayOriginOverride(null);

        HideDrivenRay(leftDrivenRay);
        HideDrivenRay(rightDrivenRay);
        RestoreOfficialInputDrivers();
        RestoreOfficialLocomotion();
        StopMovementAudio();
        StopAllNoise();
        velocity = Vector3.zero;
        isCrouching = false;
        inputFrozen = false;

        if (bodyController != null && hasDefaults)
        {
            bodyController.height = standingHeight;
            bodyController.center = new Vector3(bodyController.center.x, standingCenterY, bodyController.center.z);
        }

        if (xriCamera != null && hasDefaults)
            xriCamera.transform.localPosition = initialCameraLocalPosition;

        if (ActiveDriver == this)
            ActiveDriver = null;
    }

    private static void HideDrivenRay(DrivenRayState rayState)
    {
        if (rayState == null)
            return;

        foreach (LineRenderer lineRenderer in rayState.Lines)
        {
            if (lineRenderer != null)
                lineRenderer.enabled = false;
        }

        if (rayState.FallbackLine != null)
            rayState.FallbackLine.enabled = false;

        if (rayState.FallbackBeam != null)
            rayState.FallbackBeam.SetActive(false);

        if (rayState.FallbackRibbon != null)
            rayState.FallbackRibbon.SetActive(false);

        if (rayState.HybridOfficialLineObject != null)
            rayState.HybridOfficialLineObject.SetActive(false);

        rayState.Lines.Clear();
        rayState.OfficialLines.Clear();
        rayState.CurrentLength = 0f;
        rayState.CurrentRenderOrigin = Vector3.zero;
        rayState.CurrentRenderEnd = Vector3.zero;
        rayState.HasRenderPoints = false;
        rayState.HasOfficialLineVisual = false;
        rayState.LoggedRuntimeStatus = false;
        rayState.LoggedOfficialHiddenStatus = false;
    }

    private void Update()
    {
        CacheReferences();
        EnsureOfficialControllerModels();
        EnsureOfficialRayVisuals();
        SetBridgeAimOverride();
        RefreshDemoPoseAndRayVisuals();

        if (inputFrozen)
            return;

        HandleLook();
        HandleCrouch();
        HandleMovement();
        HandleInteraction();
    }

    private void LateUpdate()
    {
        RefreshDemoPoseAndRayVisuals();
    }

    private void SubscribeBeforeRender()
    {
        UnsubscribeBeforeRender();
        if (!updatePoseBeforeRender)
            return;

        Application.onBeforeRender += HandleBeforeRender;
        subscribedBeforeRender = true;
    }

    private void UnsubscribeBeforeRender()
    {
        if (!subscribedBeforeRender)
            return;

        Application.onBeforeRender -= HandleBeforeRender;
        subscribedBeforeRender = false;
    }

    private void HandleBeforeRender()
    {
        if (!isActiveAndEnabled || !updatePoseBeforeRender)
            return;

        RefreshDemoPoseAndRayVisuals();
    }

    private void RefreshDemoPoseAndRayVisuals()
    {
        UpdateDemoHands();
        UpdateOfficialRayVisuals();
        ForceHybridOfficialLineVisualUpdate(leftDrivenRay);
        ForceHybridOfficialLineVisualUpdate(rightDrivenRay);
        LogPoseHeightsOnce();
    }

    public void CopyMovementSettings(PlayerController desktopController)
    {
        if (desktopController == null)
            return;

        walkSpeed = desktopController.WalkSpeed;
        sprintMultiplier = desktopController.SprintMultiplier;
        gravity = desktopController.Gravity;
        jumpForce = desktopController.JumpForce;
        crouchHeight = desktopController.CrouchHeight;
        crouchSpeed = desktopController.CrouchSpeed;
    }

    public void FreezeForDeath()
    {
        inputFrozen = true;
        velocity = Vector3.zero;
        StopMovementAudio();
        StopAllNoise();
        DisableOfficialLocomotion();
        DisableOfficialInputDrivers();
    }

    public static void FreezeActiveForDeath()
    {
        if (ActiveDriver != null)
            ActiveDriver.FreezeForDeath();
    }

    public void ResetMotionAfterTeleport()
    {
        velocity = Vector3.zero;
        lastJumpPressTime = float.MinValue;
        StopMovementAudio();
        NormalizeHybridRigPose();
        RefreshDemoPoseAndRayVisuals();
    }

    private void CacheReferences()
    {
        if (bodyController == null)
            bodyController = GetComponentInChildren<CharacterController>(true);

        if (xriCamera == null)
            xriCamera = GetComponentInChildren<Camera>(true);

        if (xriCamera == null)
            xriCamera = Camera.main;

        if (xriCamera == null)
        {
            foreach (Camera candidate in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate == null)
                    continue;

                if (candidate.name.IndexOf("Main", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    candidate.name.IndexOf("Camera", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    xriCamera = candidate;
                    break;
                }
            }
        }

        if (interactableBridge == null)
            interactableBridge = GetComponent<XRIOfficialInteractableBridge>();

        bool needsLeft = !IsUsableController(leftHandController, "Left") || leftHandTransform == null;
        bool needsRight = !IsUsableController(rightHandController, "Right") || rightHandTransform == null;
        if (needsLeft || needsRight)
        {
            foreach (ActionBasedController controller in GetComponentsInChildren<ActionBasedController>(true))
            {
                if (controller == null || controller.modelPrefab == null)
                    continue;

                bool isLeft = IsControllerNamedForHand(controller, "Left");
                bool isRight = IsControllerNamedForHand(controller, "Right");

                if (needsLeft && isLeft)
                {
                    leftHandController = controller;
                    leftHandTransform = controller.modelParent != null ? controller.modelParent : controller.transform;
                    needsLeft = false;
                }

                if (needsRight && isRight)
                {
                    rightHandController = controller;
                    rightHandTransform = controller.modelParent != null ? controller.modelParent : controller.transform;
                    needsRight = false;
                }

                if (!needsLeft && !needsRight)
                    break;
            }
        }
    }

    private static bool IsUsableController(ActionBasedController controller, string handedness)
    {
        return controller != null &&
            controller.modelPrefab != null &&
            IsControllerNamedForHand(controller, handedness);
    }

    private static bool IsControllerNamedForHand(ActionBasedController controller, string handedness)
    {
        return controller != null &&
            controller.name.IndexOf(handedness, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            controller.name.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) < 0 &&
            controller.name.IndexOf("Stabilized", System.StringComparison.OrdinalIgnoreCase) < 0;
    }

    private void PreparePlayerTarget()
    {
        if (bodyController != null)
            bodyController.gameObject.tag = "Player";
    }

    private void CacheDefaults()
    {
        if (hasDefaults || bodyController == null)
            return;

        standingHeight = bodyController.height;
        standingCenterY = bodyController.center.y;
        if (xriCamera != null)
            initialCameraLocalPosition = xriCamera.transform.localPosition;

        hasDefaults = true;
    }

    private void NormalizeHybridRigPose()
    {
        if (xriCamera == null)
            return;

        Transform cameraTransform = xriCamera.transform;
        Transform cameraParent = cameraTransform.parent;
        if (resetCameraOffsetParent && IsCameraOffsetTransform(cameraParent))
        {
            cameraParent.localPosition = Vector3.zero;
            cameraParent.localRotation = Quaternion.identity;
            cameraParent.localScale = Vector3.one;
        }

        if (!lockCameraLocalPose)
            return;

        Vector3 targetLocalPosition = cameraLocalPosition + Vector3.up * cameraHeightOffset;
        targetLocalPosition.y = Mathf.Max(targetLocalPosition.y, minimumCameraHeight);
        cameraTransform.localPosition = targetLocalPosition;
        cameraTransform.localRotation = Quaternion.Euler(cameraLocalEuler);
        pitch = NormalizePitch(cameraLocalEuler.x);

        if (hasDefaults)
            initialCameraLocalPosition = cameraTransform.localPosition;
    }

    private void LogPoseHeightsOnce()
    {
        if (!logPoseHeightsOnEnable || loggedPoseHeights || xriCamera == null)
            return;

        float bodyY = bodyController != null ? bodyController.transform.position.y : transform.position.y;
        float cameraWorldHeight = xriCamera.transform.position.y - bodyY;
        float leftWorldHeight = leftHandTransform != null ? leftHandTransform.position.y - bodyY : float.NaN;
        float rightWorldHeight = rightHandTransform != null ? rightHandTransform.position.y - bodyY : float.NaN;
        Debug.Log(
            $"Hybrid XRI pose heights: cameraLocalY={xriCamera.transform.localPosition.y:F3}, " +
            $"cameraWorldHeight={cameraWorldHeight:F3}, leftHandWorldHeight={leftWorldHeight:F3}, " +
            $"rightHandWorldHeight={rightWorldHeight:F3}, baseLocalY={cameraLocalPosition.y:F3}, " +
            $"heightOffset={cameraHeightOffset:F3}, minimumCameraHeight={minimumCameraHeight:F3}, " +
            $"controllerVisualPoseScale={controllerVisualPoseScale:F2}, " +
            $"controllerVisualScale={controllerVisualScale:F2}, rayVisualDistanceScale={rayVisualDistanceScale:F2}.");
        loggedPoseHeights = true;
    }

    private static bool IsCameraOffsetTransform(Transform candidate)
    {
        if (candidate == null)
            return false;

        return candidate.name.IndexOf("Camera Offset", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            candidate.name.IndexOf("CameraFloorOffset", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float NormalizePitch(float angle)
    {
        angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
        return Mathf.Clamp(angle, -85f, 85f);
    }

    private void DisableOfficialLocomotion()
    {
        if (disabledLocomotionBehaviours.Count > 0)
            return;

        foreach (ContinuousMoveProviderBase provider in GetComponentsInChildren<ContinuousMoveProviderBase>(true))
            DisableTemporarily(provider);

        foreach (CharacterControllerDriver driver in GetComponentsInChildren<CharacterControllerDriver>(true))
            DisableTemporarily(driver);
    }

    private void DisableOfficialInputDrivers()
    {
        if (disabledOfficialInputBehaviours.Count > 0)
            return;

        DisableTemporarily(leftHandController, disabledOfficialInputBehaviours);
        DisableTemporarily(rightHandController, disabledOfficialInputBehaviours);

        foreach (Behaviour behaviour in GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || behaviour == this)
                continue;

            string typeName = behaviour.GetType().Name;
            string fullTypeName = behaviour.GetType().FullName;
            if (typeName == "ActionBasedControllerManager" ||
                typeName == "TrackedPoseDriver" ||
                fullTypeName == "UnityEngine.InputSystem.XR.TrackedPoseDriver" ||
                fullTypeName == "UnityEngine.SpatialTracking.TrackedPoseDriver" ||
                fullTypeName == "UnityEngine.XR.Interaction.Toolkit.Inputs.XRTransformStabilizer")
            {
                DisableTemporarily(behaviour, disabledOfficialInputBehaviours);
            }
        }
    }

    private void DisableTemporarily(Behaviour behaviour)
    {
        DisableTemporarily(behaviour, disabledLocomotionBehaviours);
    }

    private static void DisableTemporarily(Behaviour behaviour, List<Behaviour> disabledBehaviours)
    {
        if (behaviour == null || !behaviour.enabled)
            return;

        behaviour.enabled = false;
        disabledBehaviours.Add(behaviour);
    }

    private void RestoreOfficialLocomotion()
    {
        foreach (Behaviour behaviour in disabledLocomotionBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        disabledLocomotionBehaviours.Clear();
    }

    private void RestoreOfficialInputDrivers()
    {
        foreach (Behaviour behaviour in disabledOfficialInputBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        disabledOfficialInputBehaviours.Clear();
    }

    private void HandleLook()
    {
        if (bodyController == null || xriCamera == null)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        Transform body = bodyController.transform;

        pitch = Mathf.Clamp(pitch - mouseY, -85f, 85f);
        body.Rotate(Vector3.up * mouseX);
        xriCamera.transform.localRotation = Quaternion.Euler(
            pitch,
            cameraLocalEuler.y,
            cameraLocalEuler.z);
    }

    private void HandleMovement()
    {
        if (bodyController == null)
            return;

        bool jumpedThisFrame = false;
        if (bodyController.isGrounded)
        {
            if (velocity.y < 0f)
                velocity.y = -2f;

            if (Time.time - lastJumpPressTime < 0.15f)
            {
                velocity.y = jumpForce;
                lastJumpPressTime = float.MinValue;
                jumpedThisFrame = true;
            }
        }

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = bodyController.transform.right * moveX + bodyController.transform.forward * moveZ;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching;
        float speed = isCrouching ? crouchSpeed : walkSpeed * (isSprinting ? sprintMultiplier : 1f);
        bodyController.Move(move * (speed * Time.deltaTime));

        if (Input.GetKeyDown(KeyCode.Space))
            lastJumpPressTime = Time.time;

        velocity.y += gravity * Time.deltaTime;
        bodyController.Move(velocity * Time.deltaTime);

        UpdateMovementAudio(move, isSprinting);
        if (jumpedThisFrame)
            PlayJumpAudio();
    }

    private void UpdateMovementAudio(Vector3 move, bool isSprinting)
    {
        bool shouldRunSound =
            isSprinting &&
            !isCrouching &&
            move.magnitude > 0.2f &&
            bodyController != null &&
            bodyController.isGrounded &&
            velocity.y <= 0.1f;

        UpdateMovementNoise(move, shouldRunSound);

        if (runningAudioSource == null)
            return;

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

    private void UpdateMovementNoise(Vector3 move, bool isRunningAudibly)
    {
        bool audibleMovement =
            !isCrouching &&
            move.magnitude > 0.2f &&
            bodyController != null &&
            bodyController.isGrounded &&
            velocity.y <= 0.1f;

        if (!audibleMovement)
        {
            StopAllNoise();
            return;
        }

        float baseNoise = isRunningAudibly ? 1f : 0.68f;
        float inputMagnitude = Mathf.Clamp01(move.magnitude);
        MovementNoiseStrength = baseNoise * Mathf.Lerp(0.55f, 1f, inputMagnitude);
        IsProducingFootstepNoise = MovementNoiseStrength > 0.05f;
        IsProducingRunNoise = isRunningAudibly && IsProducingFootstepNoise;
    }

    private void StopAllNoise()
    {
        IsProducingRunNoise = false;
        IsProducingFootstepNoise = false;
        MovementNoiseStrength = 0f;
    }

    private void PlayJumpAudio()
    {
        if (jumpAudioSource != null && jumpAudioSource.clip != null)
            jumpAudioSource.PlayOneShot(jumpAudioSource.clip);
    }

    private void StopMovementAudio()
    {
        if (runningAudioSource != null)
        {
            runningAudioSource.Stop();
            runningAudioSource.volume = 0f;
        }
    }

    private void HandleCrouch()
    {
        if (bodyController == null || !hasDefaults)
            return;

        bool wantsToCrouch = Input.GetKey(KeyCode.C);
        float targetHeight = wantsToCrouch ? crouchHeight : standingHeight;
        float smoothHeight = Mathf.Lerp(bodyController.height, targetHeight, Time.deltaTime * 10f);
        isCrouching = wantsToCrouch || smoothHeight < standingHeight - 0.05f;
        bodyController.height = smoothHeight;

        float ratio = smoothHeight / Mathf.Max(standingHeight, 0.01f);
        bodyController.center = new Vector3(bodyController.center.x, standingCenterY * ratio, bodyController.center.z);

        if (xriCamera != null)
        {
            Vector3 cameraPosition = initialCameraLocalPosition;
            cameraPosition.y -= standingHeight - smoothHeight;
            xriCamera.transform.localPosition = cameraPosition;
        }
    }

    private void HandleInteraction()
    {
        if (interactableBridge == null)
            return;

        if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.E))
            return;

        if (xriCamera != null && interactableBridge.TryInteractFromTransform(xriCamera.transform, interactionOwner))
            return;

        interactableBridge.TryInteractFromRightHand(interactionOwner);
    }

    private void UpdateDemoHands()
    {
        if (xriCamera == null)
            return;

        Vector3 leftOffset = useOfficialDeviceSimulatorRestPose ? OfficialLeftControllerRestOffset : leftHandCameraOffset;
        Vector3 rightOffset = useOfficialDeviceSimulatorRestPose ? OfficialRightControllerRestOffset : rightHandCameraOffset;

        UpdateHand(leftHandController, leftHandTransform, leftControllerVisualAnchor, leftOffset, -0.15f);
        UpdateHand(rightHandController, rightHandTransform, rightControllerVisualAnchor, rightOffset, 0f);
    }

    private void EnsureOfficialControllerModels()
    {
        leftControllerVisualInstance = EnsureOfficialControllerVisual(
            leftHandTransform,
            leftHandController,
            leftControllerVisualPrefab,
            leftControllerVisualInstance,
            "Left");

        rightControllerVisualInstance = EnsureOfficialControllerVisual(
            rightHandTransform,
            rightHandController,
            rightControllerVisualPrefab,
            rightControllerVisualInstance,
            "Right");

        AttachControllerVisualToCameraAnchor(
            ref leftControllerVisualAnchor,
            ref leftControllerVisualInstance,
            "Left");

        AttachControllerVisualToCameraAnchor(
            ref rightControllerVisualAnchor,
            ref rightControllerVisualInstance,
            "Right");

        LogControllerVisualStatus("Left", leftHandController, leftHandTransform, leftControllerVisualPrefab, leftControllerVisualInstance, ref loggedLeftVisualStatus);
        LogControllerVisualStatus("Right", rightHandController, rightHandTransform, rightControllerVisualPrefab, rightControllerVisualInstance, ref loggedRightVisualStatus);
    }

    private void EnsureOfficialRayVisuals()
    {
        leftDrivenRay.Lines.Clear();
        rightDrivenRay.Lines.Clear();
        leftDrivenRay.OfficialLines.Clear();
        rightDrivenRay.OfficialLines.Clear();
        leftDrivenRay.HasOfficialLineVisual = false;
        rightDrivenRay.HasOfficialLineVisual = false;

        foreach (XRRayInteractor rayInteractor in GetComponentsInChildren<XRRayInteractor>(true))
        {
            if (rayInteractor == null)
                continue;

            bool isLeftRay = IsChildOfController(rayInteractor.transform, "Left");
            bool isRightRay = IsChildOfController(rayInteractor.transform, "Right");
            bool isControllerRay = isLeftRay || isRightRay;
            bool isTeleportRay = rayInteractor.name.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                rayInteractor.transform.parent != null &&
                rayInteractor.transform.parent.name.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool shouldShowRay = isControllerRay && !isTeleportRay;

            rayInteractor.gameObject.SetActive(shouldShowRay);
            rayInteractor.enabled = shouldShowRay;
            SetRayVisualsEnabled(rayInteractor.gameObject, shouldShowRay);

            if (driveOfficialRayVisual && shouldShowRay && isRightRay)
                PrepareDrivenRayVisual(rayInteractor, rightDrivenRay, rightHandTransform, "right");

            if (driveOfficialRayVisual && shouldShowRay && isLeftRay)
                PrepareDrivenRayVisual(rayInteractor, leftDrivenRay, leftHandTransform, "left");
        }

        if (driveOfficialRayVisual && leftDrivenRay.Lines.Count == 0 && (!preferOfficialRayVisual || !leftDrivenRay.HasOfficialLineVisual))
            AddFallbackDrivenRayLine(leftHandTransform, leftDrivenRay, "Left");

        if (driveOfficialRayVisual && rightDrivenRay.Lines.Count == 0 && (!preferOfficialRayVisual || !rightDrivenRay.HasOfficialLineVisual))
            AddFallbackDrivenRayLine(rightHandTransform, rightDrivenRay, "Right");

        if (!loggedRayVisualStatus)
        {
            if (leftDrivenRay.HasOfficialLineVisual || rightDrivenRay.HasOfficialLineVisual ||
                leftDrivenRay.Lines.Count > 0 || rightDrivenRay.Lines.Count > 0)
            {
                Debug.Log(
                    $"Hybrid XRI ray visuals ready: " +
                    $"leftOfficial={leftDrivenRay.HasOfficialLineVisual}, " +
                    $"rightOfficial={rightDrivenRay.HasOfficialLineVisual}, " +
                    $"leftFallbackLineRenderers={leftDrivenRay.Lines.Count}, " +
                    $"rightFallbackLineRenderers={rightDrivenRay.Lines.Count}.");
            }
            else
            {
                Debug.LogWarning("Hybrid XRI ray visuals were not found. The official Ray Interactor LineRenderers may be missing from the XRI rig.");
            }

            loggedRayVisualStatus = true;
        }
    }

    private void PrepareDrivenRayVisual(
        XRRayInteractor rayInteractor,
        DrivenRayState rayState,
        Transform lineOriginTransform,
        string handedness)
    {
        if (rayInteractor == null || rayState == null)
            return;

        ConfigureOfficialRayInteractor(rayInteractor);

        LineRenderer sourceLineRenderer = rayInteractor.GetComponent<LineRenderer>();
        Material sourceMaterial = sourceLineRenderer != null ? sourceLineRenderer.sharedMaterial : null;
        DisableOriginalOfficialRayLine(rayInteractor);
        EnsureHybridOfficialRayVisual(rayState, lineOriginTransform, handedness, sourceMaterial);

        if (rayState.HybridOfficialLineRenderer != null)
        {
            rayState.HasOfficialLineVisual = true;
            if (!rayState.OfficialLines.Contains(rayState.HybridOfficialLineRenderer))
                rayState.OfficialLines.Add(rayState.HybridOfficialLineRenderer);
        }

        if (preferOfficialRayVisual && rayState.HasOfficialLineVisual)
            return;

        foreach (XRInteractorLineVisual lineVisual in rayInteractor.GetComponentsInChildren<XRInteractorLineVisual>(true))
        {
            ConfigureOfficialLineVisual(lineVisual, lineOriginTransform);
            rayState.HasOfficialLineVisual = true;

            LineRenderer officialLineRenderer = lineVisual.GetComponent<LineRenderer>();
            if (officialLineRenderer != null && !rayState.OfficialLines.Contains(officialLineRenderer))
                rayState.OfficialLines.Add(officialLineRenderer);
        }

        if (preferOfficialRayVisual && rayState.HasOfficialLineVisual)
            return;

        foreach (LineRenderer lineRenderer in rayInteractor.GetComponentsInChildren<LineRenderer>(true))
        {
            ConfigureDrivenRayLine(lineRenderer);
            if (!rayState.Lines.Contains(lineRenderer))
                rayState.Lines.Add(lineRenderer);
        }
    }

    private void EnsureHybridOfficialRayVisual(
        DrivenRayState rayState,
        Transform lineOriginTransform,
        string handedness,
        Material sourceMaterial)
    {
        if (rayState == null || lineOriginTransform == null)
            return;

        if (rayState.HybridOfficialLineObject == null)
        {
            GameObject lineObject = new GameObject($"Hybrid XRI {handedness} Official Ray Visual");
            lineObject.hideFlags = HideFlags.DontSave;
            lineObject.transform.SetParent(transform, false);
            lineObject.transform.localPosition = Vector3.zero;
            lineObject.transform.localRotation = Quaternion.identity;
            lineObject.transform.localScale = Vector3.one;

            rayState.HybridOfficialLineProvider = lineObject.AddComponent<XRIHybridRayProvider>();
            rayState.HybridOfficialLineRenderer = lineObject.AddComponent<LineRenderer>();
            rayState.HybridOfficialLineObject = lineObject;
        }
        else if (rayState.HybridOfficialLineObject.transform.parent != transform)
        {
            rayState.HybridOfficialLineObject.transform.SetParent(transform, false);
        }

        rayState.HybridOfficialLineObject.SetActive(true);
        rayState.HybridOfficialLineObject.transform.localPosition = Vector3.zero;
        rayState.HybridOfficialLineObject.transform.localRotation = Quaternion.identity;
        rayState.HybridOfficialLineObject.transform.localScale = Vector3.one;

        if (rayState.HybridOfficialLineProvider != null)
        {
            rayState.HybridOfficialLineProvider.Origin = lineOriginTransform;
            rayState.HybridOfficialLineProvider.Length = officialRayLength;
            rayState.HybridOfficialLineProvider.RaycastMask = officialRayMask.value == 0 ? ~0 : officialRayMask.value;
            rayState.HybridOfficialLineProvider.InteractionMask = officialRayMask.value == 0 ? ~0 : officialRayMask.value;
            rayState.HybridOfficialLineProvider.IgnoreRoot = transform;
        }

        rayState.SourceOfficialLineMaterial = ResolveOfficialLineMaterial(sourceMaterial, rayState.HybridOfficialLineRenderer);
        ConfigureOfficialProviderLineRenderer(rayState.HybridOfficialLineRenderer, rayState.SourceOfficialLineMaterial);
        if (rayState.HybridOfficialLineVisual != null)
            rayState.HybridOfficialLineVisual.enabled = false;
    }

    private void ConfigureOfficialProviderLineRenderer(LineRenderer lineRenderer, Material sourceMaterial)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.gameObject.SetActive(true);
        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = Mathf.Max(0.001f, officialLineRendererWidth);
        lineRenderer.startWidth = lineRenderer.widthMultiplier;
        lineRenderer.endWidth = lineRenderer.widthMultiplier;
        lineRenderer.widthCurve = CreateOfficialLineWidthCurve();
        lineRenderer.colorGradient = CreateOfficialInvalidRayGradient();
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 4;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.forceRenderingOff = false;
        lineRenderer.sortingOrder = 30005;
        lineRenderer.sharedMaterial = GetOfficialRayMaterial(lineRenderer, sourceMaterial);
    }

    private static void DisableOriginalOfficialRayLine(XRRayInteractor rayInteractor)
    {
        if (rayInteractor == null)
            return;

        foreach (XRInteractorLineVisual lineVisual in rayInteractor.GetComponentsInChildren<XRInteractorLineVisual>(true))
            lineVisual.enabled = false;

        foreach (LineRenderer lineRenderer in rayInteractor.GetComponentsInChildren<LineRenderer>(true))
            lineRenderer.enabled = false;
    }

    private void ForceHybridOfficialLineVisualUpdate(DrivenRayState rayState)
    {
        if (rayState.HybridOfficialLineProvider == null || rayState.HybridOfficialLineRenderer == null)
            return;

        if (!rayState.HybridOfficialLineProvider.GetLinePoints(ref rayState.HybridLinePoints, out int sourcePointCount) ||
            sourcePointCount < 2)
        {
            return;
        }

        rayState.HybridOfficialLineRenderer.enabled = true;
        rayState.HybridOfficialLineRenderer.gameObject.layer = 0;
        if (xriCamera != null && (xriCamera.cullingMask & (1 << rayState.HybridOfficialLineRenderer.gameObject.layer)) == 0)
            rayState.HybridOfficialLineRenderer.gameObject.layer = xriCamera.gameObject.layer;

        rayState.HybridOfficialLineRenderer.forceRenderingOff = false;
        rayState.HybridOfficialLineRenderer.widthMultiplier = Mathf.Max(0.001f, officialLineRendererWidth);
        rayState.HybridOfficialLineRenderer.startWidth = rayState.HybridOfficialLineRenderer.widthMultiplier;
        rayState.HybridOfficialLineRenderer.endWidth = rayState.HybridOfficialLineRenderer.widthMultiplier;
        rayState.HybridOfficialLineRenderer.widthCurve = CreateOfficialLineWidthCurve();
        rayState.HybridOfficialLineRenderer.sharedMaterial = GetOfficialRayMaterial(
            rayState.HybridOfficialLineRenderer,
            rayState.SourceOfficialLineMaterial);

        int pointCount = Mathf.Clamp(hybridRaySegmentCount, 4, 32);
        EnsureHybridRenderPointCapacity(rayState, pointCount);
        BuildOfficialLikeRayPoints(rayState, pointCount);

        rayState.HybridOfficialLineRenderer.positionCount = pointCount;
        rayState.HybridOfficialLineRenderer.SetPositions(rayState.HybridRenderPoints);
        rayState.HybridOfficialLineRenderer.colorGradient = rayState.HybridOfficialLineProvider.HasValidTarget
            ? CreateOfficialValidRayGradient()
            : CreateOfficialInvalidRayGradient();

        if (!rayState.LoggedHybridLineStatus)
        {
            Material material = rayState.HybridOfficialLineRenderer.sharedMaterial;
            Camera camera = xriCamera != null ? xriCamera : Camera.main;
            Vector3 startViewport = camera != null ? camera.WorldToViewportPoint(rayState.HybridRenderPoints[0]) : Vector3.zero;
            Vector3 endViewport = camera != null ? camera.WorldToViewportPoint(rayState.HybridRenderPoints[pointCount - 1]) : Vector3.zero;
            Debug.Log(
                $"Hybrid XRI provider ray visible candidate: points={pointCount}, " +
                $"start={rayState.HybridRenderPoints[0]}, end={rayState.HybridRenderPoints[pointCount - 1]}, " +
                $"width={rayState.HybridOfficialLineRenderer.widthMultiplier:F3}, " +
                $"hit={rayState.HybridOfficialLineProvider.HasHit}, " +
                $"validTarget={rayState.HybridOfficialLineProvider.HasValidTarget}, " +
                $"material={(material != null ? material.name : "null")}, " +
                $"shader={(material != null && material.shader != null ? material.shader.name : "null")}, " +
                $"layer={rayState.HybridOfficialLineRenderer.gameObject.layer}, " +
                $"activeSelf={rayState.HybridOfficialLineRenderer.gameObject.activeSelf}, " +
                $"activeInHierarchy={rayState.HybridOfficialLineRenderer.gameObject.activeInHierarchy}, " +
                $"rendererEnabled={rayState.HybridOfficialLineRenderer.enabled}, " +
                $"forceOff={rayState.HybridOfficialLineRenderer.forceRenderingOff}, " +
                $"isVisible={rayState.HybridOfficialLineRenderer.isVisible}, " +
                $"camera={(camera != null ? camera.name : "null")}, " +
                $"cameraPos={(camera != null ? camera.transform.position.ToString() : "null")}, " +
                $"cameraForward={(camera != null ? camera.transform.forward.ToString() : "null")}, " +
                $"cameraMask={(camera != null ? camera.cullingMask.ToString() : "null")}, " +
                $"startViewport={startViewport}, endViewport={endViewport}.");
            rayState.LoggedHybridLineStatus = true;
        }
    }

    private void EnsureHybridRenderPointCapacity(DrivenRayState rayState, int pointCount)
    {
        if (rayState.HybridRenderPoints == null || rayState.HybridRenderPoints.Length != pointCount)
            rayState.HybridRenderPoints = new Vector3[pointCount];
    }

    private void BuildOfficialLikeRayPoints(DrivenRayState rayState, int pointCount)
    {
        Vector3 sourceOrigin = rayState.HybridLinePoints[0];
        Vector3 sourceEnd = rayState.HybridLinePoints[1];
        Vector3 targetVector = sourceEnd - sourceOrigin;
        float sourceLength = Mathf.Max(0.05f, targetVector.magnitude);
        Vector3 targetDirection = sourceLength > 0.001f ? targetVector / sourceLength : Vector3.forward;
        float visualDistanceScale = Mathf.Max(0.01f, rayVisualDistanceScale);
        float targetLength = sourceLength * visualDistanceScale;
        Vector3 origin = ScalePointFromCamera(sourceOrigin, visualDistanceScale);
        Vector3 targetEnd = origin + targetDirection * targetLength;

        if (!rayState.HasRenderPoints ||
            Vector3.SqrMagnitude(rayState.CurrentRenderEnd - targetEnd) >
            officialRaySnapThresholdDistance * officialRaySnapThresholdDistance)
        {
            rayState.CurrentLength = targetLength;
            rayState.CurrentRenderOrigin = origin;
            rayState.CurrentRenderEnd = targetEnd;
            rayState.HasRenderPoints = true;
        }
        else
        {
            float lengthSpeed = Mathf.Max(0.01f, officialRayLengthChangeSpeed);
            rayState.CurrentLength = Mathf.MoveTowards(rayState.CurrentLength, targetLength, lengthSpeed * Time.deltaTime);

            float follow = smoothOfficialRayFollow
                ? Mathf.Clamp01(Mathf.Max(0.01f, officialRayFollowTightness) * Time.deltaTime)
                : 1f;
            Vector3 desiredEnd = origin + targetDirection * rayState.CurrentLength;
            rayState.CurrentRenderEnd = Vector3.Lerp(rayState.CurrentRenderEnd, desiredEnd, follow);
            rayState.CurrentRenderOrigin = origin;
        }

        Vector3 renderEnd = rayState.CurrentRenderEnd;
        Vector3 renderVector = renderEnd - origin;
        float renderLength = Mathf.Max(0.05f, renderVector.magnitude);
        Vector3 renderDirection = renderLength > 0.001f ? renderVector / renderLength : targetDirection;

        Vector3 forwardControl = origin + targetDirection * renderLength * 0.55f;
        Vector3 endpointControl = Vector3.Lerp(forwardControl, origin + renderDirection * renderLength * 0.55f, 0.35f);
        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount <= 1 ? 1f : i / (float)(pointCount - 1);
            rayState.HybridRenderPoints[i] = SampleQuadraticBezier(origin, endpointControl, renderEnd, t);
        }
    }

    private static Vector3 SampleQuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * a + 2f * inverse * t * b + t * t * c;
    }

    private void ConfigureOfficialRayInteractor(XRRayInteractor rayInteractor)
    {
        rayInteractor.lineType = XRRayInteractor.LineType.StraightLine;
        rayInteractor.maxRaycastDistance = officialRayLength;
        rayInteractor.sampleFrequency = 60;
        rayInteractor.hitDetectionType = XRRayInteractor.HitDetectionType.ConeCast;
        rayInteractor.blendVisualLinePoints = true;
    }

    private void ConfigureOfficialLineVisual(XRInteractorLineVisual lineVisual, Transform lineOriginTransform)
    {
        if (lineVisual == null)
            return;

        lineVisual.enabled = true;
        lineVisual.lineWidth = officialLineVisualWidth;
        lineVisual.overrideInteractorLineLength = true;
        lineVisual.lineLength = officialRayLength;
        lineVisual.autoAdjustLineLength = true;
        lineVisual.minLineLength = 0.5f;
        lineVisual.useDistanceToHitAsMaxLineLength = true;
        lineVisual.lineRetractionDelay = 0.5f;
        lineVisual.lineLengthChangeSpeed = officialRayLengthChangeSpeed;
        lineVisual.setLineColorGradient = true;
        lineVisual.validColorGradient = CreateOfficialValidRayGradient();
        lineVisual.invalidColorGradient = CreateOfficialInvalidRayGradient();
        lineVisual.blockedColorGradient = CreateOfficialBlockedRayGradient();
        lineVisual.smoothMovement = false;
        lineVisual.followTightness = officialRayFollowTightness;
        lineVisual.snapThresholdDistance = officialRaySnapThresholdDistance;
        lineVisual.stopLineAtFirstRaycastHit = true;
        lineVisual.stopLineAtSelection = true;
        lineVisual.snapEndpointIfAvailable = true;
        lineVisual.lineBendRatio = 0.5f;
        lineVisual.overrideInteractorLineOrigin = lineOriginTransform != null;
        lineVisual.lineOriginTransform = lineOriginTransform;
    }

    private static void HideFallbackRayVisuals(DrivenRayState rayState)
    {
        if (rayState == null)
            return;

        if (rayState.FallbackLine != null)
            rayState.FallbackLine.enabled = false;

        if (rayState.FallbackBeam != null)
            rayState.FallbackBeam.SetActive(false);

        if (rayState.FallbackRibbon != null)
            rayState.FallbackRibbon.SetActive(false);
    }

    private void AddFallbackDrivenRayLine(Transform handTransform, DrivenRayState rayState, string handedness)
    {
        if (handTransform == null)
            return;

        if (rayState.FallbackLine == null)
        {
            GameObject rayGo = new GameObject($"Hybrid Official {handedness} Ray Visual");
            rayGo.transform.SetParent(handTransform, false);
            rayState.FallbackLine = rayGo.AddComponent<LineRenderer>();
        }
        else if (rayState.FallbackLine.transform.parent != handTransform)
        {
            rayState.FallbackLine.transform.SetParent(handTransform, false);
        }

        ConfigureDrivenRayLine(rayState.FallbackLine);
        if (!rayState.Lines.Contains(rayState.FallbackLine))
            rayState.Lines.Add(rayState.FallbackLine);
    }

    private void ConfigureDrivenRayLine(LineRenderer lineRenderer)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.gameObject.SetActive(true);
        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.widthMultiplier = Mathf.Max(0.001f, officialLineRendererWidth);
        lineRenderer.startWidth = lineRenderer.widthMultiplier;
        lineRenderer.endWidth = lineRenderer.widthMultiplier;
        lineRenderer.widthCurve = CreateOfficialLineWidthCurve();
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 4;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.colorGradient = CreateOfficialInvalidRayGradient();
        lineRenderer.material = GetDrivenRayMaterial();
    }

    private static Gradient CreateOfficialValidRayGradient()
    {
        return CreateRayGradient(new Color(0f, 0.627451f, 1f, 1f), Color.white, 1f, 0f);
    }

    private static Gradient CreateOfficialInvalidRayGradient()
    {
        return CreateRayGradient(Color.white, Color.white, 1f, 0f);
    }

    private static Gradient CreateOfficialBlockedRayGradient()
    {
        Color blocked = new Color(1f, 0.92156863f, 0.015686275f, 1f);
        return CreateRayGradient(blocked, blocked, 1f, 1f);
    }

    private static AnimationCurve CreateOfficialLineWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));
    }

    private static Gradient CreateRayGradient(Color startColor, Color endColor, float startAlpha, float endAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f),
            },
            new[]
            {
                new GradientAlphaKey(startAlpha, 0f),
                new GradientAlphaKey(endAlpha, 1f),
            });
        return gradient;
    }

    private Material ResolveOfficialLineMaterial(Material sourceMaterial, LineRenderer lineRenderer)
    {
        if (sourceMaterial != null)
            return sourceMaterial;

        if (lineRenderer != null &&
            lineRenderer.sharedMaterial != null &&
            lineRenderer.sharedMaterial != drivenRayMaterial)
        {
            return lineRenderer.sharedMaterial;
        }

        if (defaultLineMaterial == null)
            defaultLineMaterial = Resources.GetBuiltinResource<Material>("Default-Line.mat");

        return defaultLineMaterial;
    }

    private Material GetOfficialRayMaterial(LineRenderer lineRenderer, Material sourceMaterial)
    {
        Material material = ResolveOfficialLineMaterial(sourceMaterial, lineRenderer);
        return material != null ? material : GetDrivenRayMaterial();
    }

    private Material GetDrivenRayMaterial()
    {
        if (drivenRayMaterial != null)
            return drivenRayMaterial;

        Shader shader = Shader.Find("Sprites/Default") ??
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Legacy Shaders/Particles/Alpha Blended") ??
            Shader.Find("Hidden/Internal-Colored") ??
            Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard");
        if (shader == null)
            return null;

        drivenRayMaterial = new Material(shader)
        {
            name = "Hybrid Official Ray Line",
            hideFlags = HideFlags.DontSave,
        };

        Color rayColor = Color.white;
        if (drivenRayMaterial.HasProperty("_Color"))
            drivenRayMaterial.SetColor("_Color", rayColor);

        if (drivenRayMaterial.HasProperty("_BaseColor"))
            drivenRayMaterial.SetColor("_BaseColor", rayColor);

        ConfigureTransparentMaterial(drivenRayMaterial);
        return drivenRayMaterial;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.SetFloat("_Surface", 1f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetRayVisualsEnabled(GameObject rayRoot, bool enabled)
    {
        foreach (XRInteractorLineVisual lineVisual in rayRoot.GetComponentsInChildren<XRInteractorLineVisual>(true))
            lineVisual.enabled = enabled;

        foreach (LineRenderer lineRenderer in rayRoot.GetComponentsInChildren<LineRenderer>(true))
            lineRenderer.enabled = enabled;
    }

    private static bool IsChildOfController(Transform transformToCheck, string handedness)
    {
        Transform current = transformToCheck;
        while (current != null)
        {
            if (current.name.IndexOf(handedness, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                current.name.IndexOf("Controller", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static GameObject EnsureOfficialControllerVisual(
        Transform handTransform,
        ActionBasedController controller,
        GameObject explicitPrefab,
        GameObject currentInstance,
        string handedness)
    {
        if (handTransform == null)
            return currentInstance;

        if (currentInstance == null)
        {
            GameObject prefab = ResolveControllerVisualPrefab(controller, explicitPrefab, handedness);
            if (prefab != null)
                currentInstance = Instantiate(prefab, handTransform);
            else if (controller != null && controller.model != null)
                currentInstance = controller.model.gameObject;
        }

        if (currentInstance != null)
        {
            currentInstance.name = $"XR Controller {handedness} (Hybrid Visual)";
            if (currentInstance.transform.parent != handTransform)
                currentInstance.transform.SetParent(handTransform, false);

            currentInstance.SetActive(true);

            foreach (Renderer renderer in currentInstance.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;
        }

        if (controller != null)
        {
            controller.hideControllerModel = false;
            if (controller.model != null)
                controller.model.gameObject.SetActive(true);
        }

        return currentInstance;
    }

    private void AttachControllerVisualToCameraAnchor(
        ref Transform visualAnchor,
        ref GameObject visualInstance,
        string handedness)
    {
        if (xriCamera == null || visualInstance == null)
            return;

        if (visualAnchor == null)
        {
            GameObject anchor = new GameObject($"Hybrid {handedness} Controller Visual Anchor");
            visualAnchor = anchor.transform;
            visualAnchor.SetParent(xriCamera.transform, false);
        }

        if (visualInstance.transform.parent != visualAnchor)
            visualInstance.transform.SetParent(visualAnchor, false);

        visualInstance.transform.localPosition = controllerVisualLocalOffset;
        visualInstance.transform.localRotation = Quaternion.Euler(controllerVisualLocalEuler);
        visualInstance.transform.localScale = Vector3.one * Mathf.Max(0.01f, controllerVisualScale);
        visualInstance.SetActive(true);
    }

    private static GameObject ResolveControllerVisualPrefab(
        ActionBasedController controller,
        GameObject explicitPrefab,
        string handedness)
    {
        if (explicitPrefab != null)
            return explicitPrefab;

        if (controller != null)
        {
            Transform modelPrefab = controller.modelPrefab;
            if (modelPrefab != null)
                return modelPrefab.gameObject;
        }

#if UNITY_EDITOR
        string prefabPath = $"Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/Prefabs/Controllers/XR Controller {handedness}.prefab";
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
#else
        return null;
#endif
    }

    private static void LogControllerVisualStatus(
        string handedness,
        ActionBasedController controller,
        Transform handTransform,
        GameObject explicitPrefab,
        GameObject visualInstance,
        ref bool alreadyLogged)
    {
        if (alreadyLogged)
            return;

        if (visualInstance == null)
        {
            Debug.LogWarning(
                $"Hybrid XRI {handedness} controller visual was not created. " +
                $"controller={(controller != null ? controller.name : "null")}, " +
                $"modelPrefab={(controller != null && controller.modelPrefab != null ? controller.modelPrefab.name : "null")}, " +
                $"explicitPrefab={(explicitPrefab != null ? explicitPrefab.name : "null")}, " +
                $"handTransform={(handTransform != null ? handTransform.name : "null")}.");
            alreadyLogged = true;
            return;
        }

        int rendererCount = visualInstance.GetComponentsInChildren<Renderer>(true).Length;
        Debug.Log(
            $"Hybrid XRI {handedness} controller visual ready: " +
            $"controller={(controller != null ? controller.name : "null")}, " +
            $"visual={visualInstance.name}, renderers={rendererCount}, " +
            $"parent={(visualInstance.transform.parent != null ? visualInstance.transform.parent.name : "null")}, " +
            $"camera={(Camera.main != null ? Camera.main.name : "null")}.");
        alreadyLogged = true;
    }

    private void UpdateHand(
        ActionBasedController controller,
        Transform handTransform,
        Transform visualAnchor,
        Vector3 cameraOffset,
        float horizontalAimOffset)
    {
        Transform cameraTransform = xriCamera.transform;
        Vector3 position = cameraTransform.TransformPoint(cameraOffset);
        Vector3 visualPosition = ScalePointFromCamera(position, controllerVisualPoseScale);
        Vector3 aimPoint = cameraTransform.position +
            cameraTransform.forward * handAimDistance +
            cameraTransform.right * horizontalAimOffset;
        Quaternion rayRotation = Quaternion.LookRotation((aimPoint - position).normalized, Vector3.up);
        Quaternion visualRotation = useOfficialDeviceSimulatorRestPose ? cameraTransform.rotation : rayRotation;

        if (controller != null)
            controller.transform.SetPositionAndRotation(position, rayRotation);

        if (handTransform != null)
            handTransform.SetPositionAndRotation(position, rayRotation);

        if (visualAnchor != null)
            visualAnchor.SetPositionAndRotation(visualPosition, visualRotation);
    }

    private void UpdateOfficialRayVisuals()
    {
        if (!driveOfficialRayVisual)
            return;

        UpdateDrivenRayVisual(leftHandTransform, leftDrivenRay, "left");
        UpdateDrivenRayVisual(rightHandTransform, rightDrivenRay, "right");
    }

    private void UpdateDrivenRayVisual(Transform handTransform, DrivenRayState rayState, string handedness)
    {
        if (handTransform == null || rayState == null)
            return;

        if (preferOfficialRayVisual && rayState.HasOfficialLineVisual)
        {
            HideFallbackRayVisuals(rayState);
            rayState.Lines.Clear();
            return;
        }

        if (rayState.Lines.Count == 0)
        {
            if (preferOfficialRayVisual && rayState.HasOfficialLineVisual)
            {
                if (!fallbackWhenOfficialRayHidden)
                    return;

                useFallbackRayRibbon = true;
            }

            AddFallbackDrivenRayLine(handTransform, rayState, handedness);
        }

        if (rayState.Lines.Count == 0)
            return;

        Vector3 targetOrigin = handTransform.position;
        Vector3 targetDirection = handTransform.forward.sqrMagnitude > 0.0001f ? handTransform.forward.normalized : Vector3.forward;
        float targetLength = officialRayLength;
        if (TryGetDrivenRayHitDistance(targetOrigin, targetDirection, out float hitDistance))
            targetLength = Mathf.Max(0.1f, hitDistance);

        float speed = Mathf.Max(0.01f, officialRayLengthChangeSpeed);
        rayState.CurrentLength = rayState.CurrentLength <= 0f
            ? targetLength
            : Mathf.MoveTowards(rayState.CurrentLength, targetLength, speed * Time.deltaTime);

        Vector3 targetEnd = targetOrigin + targetDirection * rayState.CurrentLength;
        GetDrivenRayRenderPoints(rayState, targetOrigin, targetEnd, out Vector3 renderOrigin, out Vector3 renderEnd);
        UpdateFallbackRayRibbon(rayState, handedness, renderOrigin, renderEnd);
        for (int i = rayState.Lines.Count - 1; i >= 0; i--)
        {
            LineRenderer lineRenderer = rayState.Lines[i];
            if (lineRenderer == null)
            {
                rayState.Lines.RemoveAt(i);
                continue;
            }

            lineRenderer.enabled = true;
            SetDrivenRayLinePositions(lineRenderer, renderOrigin, renderEnd);

            if (!rayState.LoggedRuntimeStatus)
            {
                string materialName = lineRenderer.sharedMaterial != null ? lineRenderer.sharedMaterial.name : "null";
                string shaderName = lineRenderer.sharedMaterial != null && lineRenderer.sharedMaterial.shader != null
                    ? lineRenderer.sharedMaterial.shader.name
                    : "null";
                Debug.Log(
                    $"Hybrid XRI {handedness} ray runtime: origin={renderOrigin}, end={renderEnd}, length={rayState.CurrentLength:F2}, " +
                    $"width={lineRenderer.widthMultiplier:F3}, material={materialName}, shader={shaderName}, " +
                    $"camera={(xriCamera != null ? xriCamera.name : "null")}, " +
                    $"cameraPos={(xriCamera != null ? xriCamera.transform.position.ToString() : "null")}, " +
                    $"cameraForward={(xriCamera != null ? xriCamera.transform.forward.ToString() : "null")}.");
                rayState.LoggedRuntimeStatus = true;
            }
        }
    }

    private void GetDrivenRayRenderPoints(
        DrivenRayState rayState,
        Vector3 targetOrigin,
        Vector3 targetEnd,
        out Vector3 renderOrigin,
        out Vector3 renderEnd)
    {
        float visualDistanceScale = Mathf.Max(0.01f, rayVisualDistanceScale);
        targetOrigin = ScalePointFromCamera(targetOrigin, visualDistanceScale);
        targetEnd = ScalePointFromCamera(targetEnd, visualDistanceScale);

        renderOrigin = targetOrigin;
        renderEnd = targetEnd;
        if (!smoothOfficialRayFollow || rayState == null)
            return;

        float snapThreshold = Mathf.Max(0.01f, officialRaySnapThresholdDistance);
        if (!rayState.HasRenderPoints ||
            Vector3.SqrMagnitude(rayState.CurrentRenderEnd - targetEnd) > snapThreshold * snapThreshold)
        {
            rayState.CurrentRenderOrigin = targetOrigin;
            rayState.CurrentRenderEnd = targetEnd;
            rayState.HasRenderPoints = true;
            renderOrigin = targetOrigin;
            renderEnd = targetEnd;
            return;
        }

        float followTightness = Mathf.Max(0.01f, officialRayFollowTightness);
        float pointSmoothIncrement = Mathf.Clamp01(followTightness * Time.deltaTime);
        rayState.CurrentRenderOrigin = targetOrigin;
        rayState.CurrentRenderEnd = Vector3.Lerp(rayState.CurrentRenderEnd, targetEnd, pointSmoothIncrement);

        float targetLength = Vector3.Distance(targetOrigin, targetEnd);
        Vector3 smoothedVector = rayState.CurrentRenderEnd - targetOrigin;
        float smoothedLength = smoothedVector.magnitude;
        if (targetLength < smoothedLength && smoothedLength > 0.001f)
            rayState.CurrentRenderEnd = targetOrigin + smoothedVector.normalized * targetLength;

        renderOrigin = targetOrigin;
        renderEnd = rayState.CurrentRenderEnd;
    }

    private Vector3 ScalePointFromCamera(Vector3 worldPoint, float distanceScale)
    {
        float safeScale = Mathf.Max(0.01f, distanceScale);
        if (xriCamera == null || Mathf.Abs(safeScale - 1f) < 0.0001f)
            return worldPoint;

        Vector3 cameraPosition = xriCamera.transform.position;
        return cameraPosition + (worldPoint - cameraPosition) * safeScale;
    }

    private static void SetDrivenRayLinePositions(LineRenderer lineRenderer, Vector3 origin, Vector3 end)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, end);
    }

    private void UpdateFallbackRayRibbon(
        DrivenRayState rayState,
        string handedness,
        Vector3 origin,
        Vector3 end)
    {
        if (!useFallbackRayRibbon)
        {
            if (rayState.FallbackBeam != null)
                rayState.FallbackBeam.SetActive(false);

            if (rayState.FallbackRibbon != null)
                rayState.FallbackRibbon.SetActive(false);

            return;
        }

        if (rayState.FallbackBeam != null)
            rayState.FallbackBeam.SetActive(false);

        EnsureFallbackRayRibbon(rayState, handedness);

        Vector3 delta = end - origin;
        float length = delta.magnitude;
        if (length <= 0.01f)
        {
            rayState.FallbackRibbon.SetActive(false);
            return;
        }

        Transform cameraTransform = xriCamera != null ? xriCamera.transform : null;
        Vector3 viewDirection = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 side = Vector3.Cross(delta.normalized, viewDirection);
        if (side.sqrMagnitude < 0.0001f)
            side = cameraTransform != null ? cameraTransform.up : Vector3.up;

        side.Normalize();
        float halfWidth = Mathf.Max(0.002f, fallbackRayRibbonWidth * 0.5f);
        Vector3[] vertices =
        {
            origin - side * halfWidth,
            origin + side * halfWidth,
            end - side * halfWidth,
            end + side * halfWidth,
        };

        Mesh mesh = rayState.FallbackRibbonMesh;
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 1, 2, 0, 1, 3, 2 };
        mesh.colors = new[]
        {
            new Color(1f, 1f, 1f, 0.9f),
            new Color(1f, 1f, 1f, 0.9f),
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0f),
        };
        mesh.RecalculateBounds();

        rayState.FallbackRibbon.SetActive(true);
        rayState.FallbackRibbon.layer = 0;
        if (rayState.FallbackRibbonRenderer != null)
        {
            rayState.FallbackRibbonRenderer.enabled = true;
            rayState.FallbackRibbonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rayState.FallbackRibbonRenderer.receiveShadows = false;
            rayState.FallbackRibbonRenderer.sharedMaterial = GetDrivenRayMaterial();
            rayState.FallbackRibbonRenderer.sortingOrder = 30005;
        }
    }

    private void EnsureFallbackRayRibbon(DrivenRayState rayState, string handedness)
    {
        if (rayState.FallbackRibbon != null)
            return;

        rayState.FallbackRibbon = new GameObject($"Hybrid Official {handedness} Ray Ribbon");
        rayState.FallbackRibbon.hideFlags = HideFlags.DontSave;
        rayState.FallbackRibbonMeshFilter = rayState.FallbackRibbon.AddComponent<MeshFilter>();
        rayState.FallbackRibbonRenderer = rayState.FallbackRibbon.AddComponent<MeshRenderer>();
        rayState.FallbackRibbonMesh = new Mesh
        {
            name = $"Hybrid Official {handedness} Ray Ribbon Mesh",
            hideFlags = HideFlags.DontSave,
        };
        rayState.FallbackRibbonMeshFilter.sharedMesh = rayState.FallbackRibbonMesh;
        rayState.FallbackRibbonRenderer.sharedMaterial = GetDrivenRayMaterial();
        rayState.FallbackRibbonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rayState.FallbackRibbonRenderer.receiveShadows = false;
        rayState.FallbackRibbonRenderer.sortingOrder = 30005;
    }

    private bool TryGetDrivenRayHitDistance(Vector3 origin, Vector3 direction, out float distance)
    {
        int mask = officialRayMask.value == 0 ? ~0 : officialRayMask.value;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, officialRayLength, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            distance = hit.distance;
            return true;
        }

        distance = 0f;
        return false;
    }

    private void SetBridgeAimOverride()
    {
        if (interactableBridge != null)
            interactableBridge.SetPriorityRayOriginOverride(xriCamera != null ? xriCamera.transform : null);
    }
}
