using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class LockedDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;
    [SerializeField] private Collider doorBlocker;

    [Header("Locked Feedback")]
    [SerializeField] private AudioClip lockedSound;
    [SerializeField] private float lockedJiggleAngle = 6f;
    [SerializeField] private float lockedJiggleDuration = 0.25f;

    [Header("Void Sequence")]
    [SerializeField] private bool triggerVoidSequenceOnOpen;
    [SerializeField] private string transitionSceneName = "Level45";
    [SerializeField] private Transform darkPullTarget;
    [SerializeField] private DoorVoidTransitionSettings voidSequenceSettings =
        DoorVoidTransitionSettings.CreateDefault();

    private bool isOpen;
    private float targetAngle;
    private float currentAngle;
    private PlayerInventory cachedPlayerInventory;
    private AudioSource audioSource;
    private float lockedJiggleTimer;
    private Quaternion closedLocalRotation = Quaternion.identity;
    private bool voidSequenceTriggered;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        if (!isOpen)
        {
            UpdateLockedFeedback();
            return;
        }

        currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * openSpeed);
        if (doorPivot != null)
            doorPivot.localRotation = closedLocalRotation * Quaternion.Euler(0f, currentAngle, 0f);

        if (Mathf.Abs(currentAngle - targetAngle) < 0.1f && doorBlocker != null && doorBlocker.enabled)
        {
            doorBlocker.enabled = false;
        }
    }

    public void Interact(GameObject interactor)
    {
        if (isOpen)
            return;

        var inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory != null)
            cachedPlayerInventory = inventory;

        if (inventory == null || !inventory.HasKeycard)
        {
            PlayLockedFeedback();
            return;
        }

        audioSource.Play();

        isOpen = true;
        targetAngle = openAngle;

        if (triggerVoidSequenceOnOpen && !voidSequenceTriggered)
        {
            voidSequenceTriggered = SceneTransitionController.TryBeginDoorVoidTransition(
                transitionSceneName,
                darkPullTarget != null ? darkPullTarget : doorPivot,
                voidSequenceSettings
            );
        }
    }

    public string GetInteractionPrompt()
    {
        if (isOpen)
            return string.Empty;

        if (cachedPlayerInventory == null)
            CachePlayerInventory();

        if (cachedPlayerInventory == null || !cachedPlayerInventory.HasKeycard)
            return "Need key to open";

        return "<b><color=#FFD700>[E]</color></b> Open";
    }

    private void Start()
    {
        if (doorPivot != null)
            closedLocalRotation = doorPivot.localRotation;

        CachePlayerInventory();
    }

    private void CachePlayerInventory()
    {
        foreach (PlayerInventory inventory in FindObjectsOfType<PlayerInventory>(true))
        {
            if (!inventory.CompareTag("Player"))
                continue;

            cachedPlayerInventory = inventory;
            return;
        }
    }

    private void UpdateLockedFeedback()
    {
        if (lockedJiggleTimer <= 0f || doorPivot == null)
            return;

        lockedJiggleTimer -= Time.deltaTime;
        float jiggleDuration = Mathf.Max(0.01f, lockedJiggleDuration);
        float normalizedTime = Mathf.Clamp01(lockedJiggleTimer / jiggleDuration);
        float jiggleAngle = Mathf.Sin(normalizedTime * Mathf.PI * 4f) * lockedJiggleAngle * normalizedTime;
        doorPivot.localRotation = closedLocalRotation * Quaternion.Euler(0f, jiggleAngle, 0f);

        if (lockedJiggleTimer <= 0f)
            doorPivot.localRotation = closedLocalRotation;
    }

    private void PlayLockedFeedback()
    {
        lockedJiggleTimer = lockedJiggleDuration;

        if (lockedSound != null)
            audioSource.PlayOneShot(lockedSound);
    }
}
