using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InteractionPrompt prompt;

    private void Update()
    {
        HandleInteractionDetection();
        HandleInteractionInput();
    }

    private void HandleInteractionDetection()
    {
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, interactDistance))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactable))
            {
                if (prompt != null)
                    prompt.Show(interactable.GetInteractionPrompt());
                return;
            }
        }

        if (prompt != null)
            prompt.Hide();
    }

    private void HandleInteractionInput()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, interactDistance))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactable))
            {
                interactable.Interact(gameObject);
            }
        }
    }
}
