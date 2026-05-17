using UnityEngine;

public class KeycardPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private AudioClip pickupSound;

    public void Interact(GameObject interactor)
    {
        var inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory == null)
            return;

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        inventory.HasKeycard = true;
        gameObject.SetActive(false);
    }

    public string GetInteractionPrompt()
    {
        return "Press E to pick up keycard";
    }
}
