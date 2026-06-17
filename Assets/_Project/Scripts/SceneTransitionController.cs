using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[Serializable]
public struct VoidDropTransitionSettings
{
    public float dropDuration;
    public float fadeOutDelay;
    public float fadeOutDuration;
    public float blackoutHoldDuration;
    public float fadeInDuration;
    public float downwardDistance;
    public float cameraTiltDegrees;
    public float cameraWobbleDegrees;
    public float wobbleFrequency;
    public float desktopFovKick;
    public bool reduceMotionWhenXRActive;

    public static VoidDropTransitionSettings CreateDefault()
    {
        return new VoidDropTransitionSettings
        {
            dropDuration = 1.15f,
            fadeOutDelay = 0.3f,
            fadeOutDuration = 0.5f,
            blackoutHoldDuration = 0.12f,
            fadeInDuration = 0.75f,
            downwardDistance = 6f,
            cameraTiltDegrees = 22f,
            cameraWobbleDegrees = 3.5f,
            wobbleFrequency = 13f,
            desktopFovKick = 10f,
            reduceMotionWhenXRActive = true
        };
    }
}

[Serializable]
public struct DoorVoidTransitionSettings
{
    public float doorOpenDelay;
    public float pullDuration;
    public float darknessFadeDuration;
    public float preSceneBlackoutHold;
    public float pullStrength;
    public float pullLift;
    public float cameraPitchDegrees;
    public float cameraRollDegrees;
    public float wobbleDegrees;
    public float wobbleFrequency;
    public float pullFovKick;
    public float visibleDarknessAlpha;
    public float level45FallHeight;
    public float level45FallDuration;
    public float level45ImpactBlackout;
    public float level45WakeDelay;
    public float level45WakeFadeDuration;
    public bool reduceMotionWhenXRActive;
    public AudioClip pullLoopClip;
    public AudioClip arrivalFallClip;

    public static DoorVoidTransitionSettings CreateDefault()
    {
        return new DoorVoidTransitionSettings
        {
            doorOpenDelay = 0.55f,
            pullDuration = 1.55f,
            darknessFadeDuration = 0.55f,
            preSceneBlackoutHold = 0.2f,
            pullStrength = 8.5f,
            pullLift = 1.2f,
            cameraPitchDegrees = 18f,
            cameraRollDegrees = 24f,
            wobbleDegrees = 4f,
            wobbleFrequency = 12f,
            pullFovKick = 14f,
            visibleDarknessAlpha = 0.2f,
            level45FallHeight = 26f,
            level45FallDuration = 1.2f,
            level45ImpactBlackout = 0.45f,
            level45WakeDelay = 1.15f,
            level45WakeFadeDuration = 1.8f,
            reduceMotionWhenXRActive = true,
            pullLoopClip = null,
            arrivalFallClip = null
        };
    }
}

public class SceneTransitionController : MonoBehaviour
{
    private sealed class TransitionContext
    {
        public Transform playerRoot;
        public Transform cameraTransform;
        public Camera cameraComponent;
        public CharacterController characterController;
        public PlayerController desktopPlayer;
        public VRRigDriver vrRigDriver;
        public XRIOfficialPlayerTuning xriOfficialPlayer;
        public XRIHybridDemoDriver xriHybridDriver;
        public Vector3 rootStartPosition;
        public Quaternion rootStartRotation;
        public Quaternion cameraLocalStartRotation;
        public float cameraStartFieldOfView;
        public bool cameraUsesMutableFov;
    }

    private static SceneTransitionController instance;

    private Canvas overlayCanvas;
    private Image fadeImage;
    private AudioSource transitionAudioSource;
    private Coroutine activeTransition;

    public static bool IsTransitionRunning =>
        instance != null && instance.activeTransition != null;

    public static bool TryBeginVoidDropTransition(
        string nextSceneName,
        VoidDropTransitionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("SceneTransitionController: next scene name is empty.");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogWarning(
                $"SceneTransitionController: scene '{nextSceneName}' is not available in Build Settings.");
            return false;
        }

        SceneTransitionController controller = GetOrCreateInstance();
        if (controller.activeTransition != null)
            return false;

        controller.activeTransition = controller.StartCoroutine(
            controller.RunVoidDropTransition(nextSceneName, settings));
        return true;
    }

    public static bool TryBeginDoorVoidTransition(
        string nextSceneName,
        Transform darkPullTarget,
        DoorVoidTransitionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("SceneTransitionController: next scene name is empty.");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogWarning(
                $"SceneTransitionController: scene '{nextSceneName}' is not available in Build Settings.");
            return false;
        }

        SceneTransitionController controller = GetOrCreateInstance();
        if (controller.activeTransition != null)
            return false;

        controller.activeTransition = controller.StartCoroutine(
            controller.RunDoorVoidTransition(nextSceneName, darkPullTarget, settings));
        return true;
    }

    private static SceneTransitionController GetOrCreateInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<SceneTransitionController>();
        if (instance != null)
        {
            instance.EnsureOverlay();
            return instance;
        }

        GameObject controllerObject = new GameObject("SceneTransitionController");
        DontDestroyOnLoad(controllerObject);
        instance = controllerObject.AddComponent<SceneTransitionController>();
        instance.EnsureOverlay();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
    }

    private IEnumerator RunVoidDropTransition(
        string nextSceneName,
        VoidDropTransitionSettings settings)
    {
        EnsureOverlay();
        SetFadeAlpha(0f);
        overlayCanvas.enabled = true;

        TransitionContext context = ResolveTransitionContext();
        FreezeContext(context);

        bool reduceMotion = settings.reduceMotionWhenXRActive && IsXRActive();
        float dropDuration = Mathf.Max(0.05f, settings.dropDuration);
        float fadeDelay = Mathf.Clamp(settings.fadeOutDelay, 0f, dropDuration);
        float fadeOutDuration = Mathf.Max(0.05f, settings.fadeOutDuration);

        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / dropDuration);
            float easedDrop = EaseInCubic(normalized);

            if (!reduceMotion)
                ApplyDropMotion(context, settings, easedDrop, elapsed);

            float fadeProgress = Mathf.Clamp01((elapsed - fadeDelay) / fadeOutDuration);
            SetFadeAlpha(EaseInOutQuad(fadeProgress));
            yield return null;
        }

        SetFadeAlpha(1f);

        if (settings.blackoutHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(settings.blackoutHoldDuration);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError($"SceneTransitionController: failed to load scene '{nextSceneName}'.");
            overlayCanvas.enabled = false;
            activeTransition = null;
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        yield return null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        float fadeInDuration = Mathf.Max(0.05f, settings.fadeInDuration);
        float fadeInElapsed = 0f;
        while (fadeInElapsed < fadeInDuration)
        {
            fadeInElapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(fadeInElapsed / fadeInDuration);
            SetFadeAlpha(1f - EaseInOutQuad(normalized));
            yield return null;
        }

        SetFadeAlpha(0f);
        overlayCanvas.enabled = false;
        activeTransition = null;
    }

    private IEnumerator RunDoorVoidTransition(
        string nextSceneName,
        Transform darkPullTarget,
        DoorVoidTransitionSettings settings)
    {
        EnsureOverlay();
        SetFadeAlpha(0f);
        overlayCanvas.enabled = true;

        if (settings.doorOpenDelay > 0f)
            yield return new WaitForSecondsRealtime(settings.doorOpenDelay);

        TransitionContext context = ResolveTransitionContext();
        FreezeContext(context);

        bool reduceMotion = settings.reduceMotionWhenXRActive && IsXRActive();
        Vector3 pullTargetPosition = darkPullTarget != null
            ? darkPullTarget.position
            : (context.playerRoot != null
                ? context.playerRoot.position + context.playerRoot.forward * 4f
                : Vector3.zero);

        if (settings.pullLoopClip != null)
            PlayTransitionClip(settings.pullLoopClip, false, 0.85f);

        float pullDuration = Mathf.Max(0.05f, settings.pullDuration);
        float darknessDuration = Mathf.Max(0.05f, settings.darknessFadeDuration);
        float elapsed = 0f;

        while (elapsed < pullDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / pullDuration);
            float eased = EaseInOutQuad(normalized);
            float darknessProgress = Mathf.Clamp01(elapsed / darknessDuration);

            if (!reduceMotion)
                ApplyDoorPullMotion(context, pullTargetPosition, settings, eased, elapsed);

            SetFadeAlpha(Mathf.Lerp(0f, 1f, darknessProgress));
            yield return null;
        }

        StopTransitionAudio();
        SetFadeAlpha(1f);

        if (settings.preSceneBlackoutHold > 0f)
            yield return new WaitForSecondsRealtime(settings.preSceneBlackoutHold);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError($"SceneTransitionController: failed to load scene '{nextSceneName}'.");
            overlayCanvas.enabled = false;
            activeTransition = null;
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        yield return null;

        if (string.Equals(nextSceneName, "Level45", StringComparison.OrdinalIgnoreCase))
        {
            yield return null;
            PlayArrivalFallOneShot(settings.arrivalFallClip, 1f);
            yield return RunLevel45ArrivalSequence(settings);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            yield return FadeInOverlay(Mathf.Max(0.05f, settings.level45WakeFadeDuration));
        }

        SetFadeAlpha(0f);
        overlayCanvas.enabled = false;
        activeTransition = null;
    }

    private TransitionContext ResolveTransitionContext()
    {
        TransitionContext context = new TransitionContext();

        XRIOfficialPlayerTuning officialXriPlayer = FindFirstObjectByType<XRIOfficialPlayerTuning>();
        if (officialXriPlayer != null &&
            officialXriPlayer.isActiveAndEnabled &&
            officialXriPlayer.PlayerTarget != null &&
            officialXriPlayer.PlayerTarget.activeInHierarchy)
        {
            context.playerRoot = officialXriPlayer.PlayerTarget.transform;
            context.characterController = officialXriPlayer.BodyController;
            context.xriOfficialPlayer = officialXriPlayer;
            context.xriHybridDriver =
                officialXriPlayer.GetComponent<XRIHybridDemoDriver>() ??
                officialXriPlayer.GetComponentInParent<XRIHybridDemoDriver>() ??
                officialXriPlayer.GetComponentInChildren<XRIHybridDemoDriver>(true);
        }
        else
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                context.playerRoot = playerObject.transform;
                context.characterController = playerObject.GetComponent<CharacterController>();
                context.desktopPlayer = playerObject.GetComponent<PlayerController>();
                context.vrRigDriver =
                    playerObject.GetComponent<VRRigDriver>() ??
                    playerObject.GetComponentInParent<VRRigDriver>() ??
                    playerObject.GetComponentInChildren<VRRigDriver>(true);
                context.xriOfficialPlayer =
                    playerObject.GetComponentInParent<XRIOfficialPlayerTuning>() ??
                    playerObject.GetComponentInChildren<XRIOfficialPlayerTuning>(true);
                context.xriHybridDriver =
                    playerObject.GetComponentInParent<XRIHybridDemoDriver>() ??
                    playerObject.GetComponentInChildren<XRIHybridDemoDriver>(true);
            }
        }

        context.cameraComponent = Camera.main;
        if (context.cameraComponent == null && context.playerRoot != null)
            context.cameraComponent = context.playerRoot.GetComponentInChildren<Camera>(true);

        if (context.cameraComponent == null)
            context.cameraComponent = FindFirstObjectByType<Camera>();

        context.cameraTransform = context.cameraComponent != null
            ? context.cameraComponent.transform
            : null;

        if (context.playerRoot != null)
        {
            context.rootStartPosition = context.playerRoot.position;
            context.rootStartRotation = context.playerRoot.rotation;
        }

        if (context.cameraTransform != null)
            context.cameraLocalStartRotation = context.cameraTransform.localRotation;

        if (context.cameraComponent != null)
        {
            context.cameraStartFieldOfView = context.cameraComponent.fieldOfView;
            context.cameraUsesMutableFov = !context.cameraComponent.orthographic && !IsXRActive();
        }

        return context;
    }

    private void FreezeContext(TransitionContext context)
    {
        if (context.desktopPlayer != null)
        {
            context.desktopPlayer.StopAllNoise();
            context.desktopPlayer.enabled = false;
        }

        if (context.vrRigDriver != null)
            context.vrRigDriver.enabled = false;

        if (context.xriOfficialPlayer != null)
            context.xriOfficialPlayer.DisableLocomotion();

        if (context.xriHybridDriver != null)
            context.xriHybridDriver.FreezeForDeath();

        foreach (ContinuousMoveProviderBase moveProvider in
            FindObjectsByType<ContinuousMoveProviderBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            if (moveProvider != null)
                moveProvider.enabled = false;
        }

        foreach (SnapTurnProviderBase snapTurnProvider in
            FindObjectsByType<SnapTurnProviderBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            if (snapTurnProvider != null)
                snapTurnProvider.enabled = false;
        }

        foreach (ContinuousTurnProviderBase continuousTurnProvider in
            FindObjectsByType<ContinuousTurnProviderBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            if (continuousTurnProvider != null)
                continuousTurnProvider.enabled = false;
        }

        XRIHybridDemoDriver.FreezeActiveForDeath();

        if (context.characterController != null)
            context.characterController.enabled = false;
    }

    private void ApplyDoorPullMotion(
        TransitionContext context,
        Vector3 pullTargetPosition,
        DoorVoidTransitionSettings settings,
        float normalized,
        float elapsed)
    {
        if (context.playerRoot != null)
        {
            Vector3 startPosition = context.rootStartPosition;
            Vector3 targetPosition = pullTargetPosition + (Vector3.up * settings.pullLift);
            context.playerRoot.position = Vector3.Lerp(startPosition, targetPosition, normalized);
        }

        if (context.cameraTransform != null)
        {
            float roll =
                Mathf.Sin(elapsed * settings.wobbleFrequency) * settings.wobbleDegrees +
                (settings.cameraRollDegrees * normalized);
            float pitch = settings.cameraPitchDegrees * normalized;
            Quaternion tilt = Quaternion.Euler(pitch, 0f, roll);
            context.cameraTransform.localRotation =
                context.cameraLocalStartRotation * tilt;
        }

        if (context.cameraComponent != null && context.cameraUsesMutableFov)
        {
            context.cameraComponent.fieldOfView = Mathf.Lerp(
                context.cameraStartFieldOfView,
                context.cameraStartFieldOfView + settings.pullFovKick,
                EaseOutQuad(normalized));
        }
    }

    private IEnumerator RunLevel45ArrivalSequence(DoorVoidTransitionSettings settings)
    {
        yield return null;

        TransitionContext context = ResolveTransitionContext();
        List<Behaviour> disabledBehaviours = new List<Behaviour>();
        PlayerRespawnController respawnController = null;

        if (context.playerRoot != null)
        {
            respawnController = context.playerRoot.GetComponent<PlayerRespawnController>();
            DisableForArrival(context, disabledBehaviours, respawnController);
        }

        bool reduceMotion = settings.reduceMotionWhenXRActive && IsXRActive();

        if (context.playerRoot != null)
        {
            Vector3 landingPosition = ResolveGroundAlignedLandingPosition(
                context.rootStartPosition,
                context.characterController);
            Quaternion landingRotation = context.rootStartRotation;
            Vector3 fallStartPosition = landingPosition + Vector3.up * settings.level45FallHeight;

            if (context.characterController != null)
                context.characterController.enabled = false;

            context.playerRoot.SetPositionAndRotation(fallStartPosition, landingRotation);

            float fallDuration = Mathf.Max(0.1f, settings.level45FallDuration);
            float elapsed = 0f;
            float targetAlpha = Mathf.Clamp01(settings.visibleDarknessAlpha);

            while (elapsed < fallDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / fallDuration);
                float eased = EaseInCubic(normalized);

                context.playerRoot.position = Vector3.Lerp(
                    fallStartPosition,
                    landingPosition,
                    eased);

                if (context.cameraTransform != null && !reduceMotion)
                {
                    float roll = Mathf.Sin(elapsed * 16f) * (1.5f + ((1f - normalized) * 1.5f));
                    float pitch = Mathf.Lerp(8f, -18f, normalized);
                    context.cameraTransform.localRotation =
                        context.cameraLocalStartRotation *
                        Quaternion.Euler(pitch, 0f, roll);
                }

                SetFadeAlpha(Mathf.Lerp(1f, targetAlpha, EaseOutQuad(normalized)));
                yield return null;
            }

            context.playerRoot.SetPositionAndRotation(landingPosition, landingRotation);
            SetFadeAlpha(1f);

            if (settings.level45ImpactBlackout > 0f)
                yield return new WaitForSecondsRealtime(settings.level45ImpactBlackout);

            if (settings.level45WakeDelay > 0f)
                yield return new WaitForSecondsRealtime(settings.level45WakeDelay);

            if (context.desktopPlayer != null)
            {
                context.desktopPlayer.enabled = true;
                context.desktopPlayer.TeleportTo(
                    landingPosition,
                    landingRotation,
                    resetLookPitch: true);
                context.desktopPlayer.enabled = false;
            }
            else if (context.playerRoot != null)
            {
                context.playerRoot.SetPositionAndRotation(landingPosition, landingRotation);
            }

            if (context.characterController != null)
                context.characterController.enabled = true;

            yield return FadeInOverlay(Mathf.Max(0.1f, settings.level45WakeFadeDuration));
        }

        RestoreDisabledBehaviours(disabledBehaviours);

        if (respawnController != null)
            respawnController.enabled = true;

        if (context.characterController != null)
            context.characterController.enabled = true;

        if (context.desktopPlayer != null)
            context.desktopPlayer.enabled = true;

        if (context.vrRigDriver != null)
            context.vrRigDriver.enabled = true;

        if (context.xriHybridDriver != null)
            context.xriHybridDriver.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ApplyDropMotion(
        TransitionContext context,
        VoidDropTransitionSettings settings,
        float normalizedDrop,
        float elapsed)
    {
        if (context.playerRoot != null)
        {
            Vector3 dropOffset = Vector3.down * (settings.downwardDistance * normalizedDrop);
            context.playerRoot.SetPositionAndRotation(
                context.rootStartPosition + dropOffset,
                context.rootStartRotation);
        }

        if (context.cameraTransform != null)
        {
            float wobble = Mathf.Sin(elapsed * settings.wobbleFrequency) *
                settings.cameraWobbleDegrees *
                (1f - (normalizedDrop * 0.35f));
            Quaternion tilt = Quaternion.Euler(
                settings.cameraTiltDegrees * normalizedDrop,
                0f,
                wobble);

            context.cameraTransform.localRotation =
                context.cameraLocalStartRotation * tilt;
        }

        if (context.cameraComponent != null && context.cameraUsesMutableFov)
        {
            context.cameraComponent.fieldOfView = Mathf.Lerp(
                context.cameraStartFieldOfView,
                context.cameraStartFieldOfView + settings.desktopFovKick,
                EaseOutQuad(normalizedDrop));
        }
    }

    private Vector3 ResolveGroundAlignedLandingPosition(
        Vector3 approximatePosition,
        CharacterController controller)
    {
        float controllerOffset = 1.05f;
        float probeRadius = 0.16f;

        if (controller != null)
        {
            controllerOffset =
                (controller.height * 0.5f) - controller.center.y + 0.04f;
            probeRadius = Mathf.Max(0.1f, controller.radius * 0.75f);
        }

        Vector3 probeOrigin = approximatePosition + Vector3.up * 3.5f;

        if (Physics.SphereCast(
                probeOrigin,
                probeRadius,
                Vector3.down,
                out RaycastHit hit,
                10f,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            return new Vector3(
                approximatePosition.x,
                hit.point.y + controllerOffset,
                approximatePosition.z);
        }

        if (Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out RaycastHit rayHit,
                10f,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            return new Vector3(
                approximatePosition.x,
                rayHit.point.y + controllerOffset,
                approximatePosition.z);
        }

        return approximatePosition;
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null && fadeImage != null)
            return;

        GameObject canvasObject = new GameObject(
            "SceneTransitionOverlay",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;
        overlayCanvas.enabled = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject fadeObject = new GameObject(
            "Fade",
            typeof(RectTransform),
            typeof(Image));
        fadeObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = fadeObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.raycastTarget = false;
        SetFadeAlpha(0f);

        transitionAudioSource = gameObject.GetComponent<AudioSource>();
        if (transitionAudioSource == null)
            transitionAudioSource = gameObject.AddComponent<AudioSource>();

        transitionAudioSource.playOnAwake = false;
        transitionAudioSource.loop = false;
        transitionAudioSource.spatialBlend = 0f;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    private IEnumerator FadeInOverlay(float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(1f - EaseInOutQuad(normalized));
            yield return null;
        }

        SetFadeAlpha(0f);
    }

    private void DisableForArrival(
        TransitionContext context,
        List<Behaviour> disabledBehaviours,
        PlayerRespawnController respawnController)
    {
        DisableBehaviour(context.desktopPlayer, disabledBehaviours);
        DisableBehaviour(context.vrRigDriver, disabledBehaviours);
        DisableBehaviour(context.xriHybridDriver, disabledBehaviours);
        DisableBehaviour(respawnController, disabledBehaviours);

        foreach (ContinuousMoveProviderBase moveProvider in
            FindObjectsByType<ContinuousMoveProviderBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            DisableBehaviour(moveProvider, disabledBehaviours);
        }

        foreach (SnapTurnProviderBase snapTurnProvider in
            FindObjectsByType<SnapTurnProviderBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            DisableBehaviour(snapTurnProvider, disabledBehaviours);
        }

        foreach (ContinuousTurnProviderBase continuousTurnProvider in
            FindObjectsByType<ContinuousTurnProviderBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            DisableBehaviour(continuousTurnProvider, disabledBehaviours);
        }
    }

    private static void DisableBehaviour(Behaviour behaviour, List<Behaviour> disabledBehaviours)
    {
        if (behaviour == null || !behaviour.enabled)
            return;

        behaviour.enabled = false;
        disabledBehaviours.Add(behaviour);
    }

    private static void RestoreDisabledBehaviours(List<Behaviour> disabledBehaviours)
    {
        if (disabledBehaviours == null)
            return;

        for (int i = 0; i < disabledBehaviours.Count; i++)
        {
            Behaviour behaviour = disabledBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = true;
        }
    }

    private void PlayTransitionClip(AudioClip clip, bool loop, float volume)
    {
        if (transitionAudioSource == null || clip == null)
            return;

        transitionAudioSource.Stop();
        transitionAudioSource.clip = clip;
        transitionAudioSource.loop = loop;
        transitionAudioSource.volume = Mathf.Clamp01(volume);
        transitionAudioSource.Play();
    }

    private void StopTransitionAudio()
    {
        if (transitionAudioSource == null)
            return;

        transitionAudioSource.Stop();
        transitionAudioSource.clip = null;
        transitionAudioSource.loop = false;
    }

    private void PlayArrivalFallOneShot(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        AudioListener listener = FindFirstObjectByType<AudioListener>();
        GameObject audioObject = listener != null
            ? listener.gameObject
            : new GameObject("ArrivalFallOneShot");

        if (listener == null)
            DontDestroyOnLoad(audioObject);

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(volume);
        source.ignoreListenerPause = true;
        source.priority = 0;
        source.bypassEffects = true;
        source.bypassListenerEffects = true;
        source.bypassReverbZones = true;
        source.clip = clip;
        source.PlayOneShot(clip, 1f);

        Destroy(source, Mathf.Max(clip.length, 0.1f) + 0.25f);

        if (listener == null)
            Destroy(audioObject, Mathf.Max(clip.length, 0.1f) + 0.4f);
    }

    private static bool IsXRActive()
    {
        return XRSettings.enabled && XRSettings.isDeviceActive;
    }

    private static float EaseInCubic(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value;
    }

    private static float EaseOutQuad(float value)
    {
        value = Mathf.Clamp01(value);
        return 1f - ((1f - value) * (1f - value));
    }

    private static float EaseInOutQuad(float value)
    {
        value = Mathf.Clamp01(value);
        if (value < 0.5f)
            return 2f * value * value;

        return 1f - Mathf.Pow(-2f * value + 2f, 2f) * 0.5f;
    }
}
