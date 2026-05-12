using UnityEngine;

public class KeycardPickup : MonoBehaviour, IInteractable
{
    public void Interact(GameObject interactor)
    {
        var inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory == null)
            return;

        inventory.HasKeycard = true;
        gameObject.SetActive(false);
    }

    public string GetInteractionPrompt()
    {
        return "Press E to pick up keycard";
    }
}
