using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransitionTrigger : MonoBehaviour, IInteractable
{
    [SerializeField] private string nextSceneName = "Level1";
    [SerializeField] private bool triggerOnEnter = true;
    [SerializeField] private string interactionPrompt = "<b><color=#FFD700>[E]</color></b> Enter";

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnEnter)
            return;

        if (!other.CompareTag("Player"))
            return;

        TryLoadNextScene();
    }

    public void Interact(GameObject interactor)
    {
        TryLoadNextScene();
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }

    private void TryLoadNextScene()
    {
        Debug.Log($"Transition to next level: {nextSceneName}");

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log("Next scene name is empty, transition placeholder triggered.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.Log($"Scene '{nextSceneName}' is not in Build Settings or does not exist. Transition placeholder triggered.");
        }
    }
}
