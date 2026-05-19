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
        return "<b><color=#FFD700>[E]</color></b> Pick up key";
    }
}
