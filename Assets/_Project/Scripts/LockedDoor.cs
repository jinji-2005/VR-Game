using UnityEngine;

public class LockedDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;
    [SerializeField] private Collider doorBlocker;

    private bool isOpen;
    private float targetAngle;
    private float currentAngle;
    private PlayerInventory cachedPlayerInventory;

    private void Update()
    {
        if (!isOpen)
            return;

        currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * openSpeed);
        doorPivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);

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
        if (inventory == null || !inventory.HasKeycard)
            return;

        isOpen = true;
        targetAngle = openAngle;
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            cachedPlayerInventory = player.GetComponent<PlayerInventory>();
    }

    public string GetInteractionPrompt()
    {
        if (isOpen)
            return string.Empty;

        if (cachedPlayerInventory == null || !cachedPlayerInventory.HasKeycard)
            return "Need keycard to open";

        return "Press E to open door";
    }
}
