using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransitionTrigger : MonoBehaviour, IInteractable
{
    [SerializeField] private string nextSceneName = "Level1";
    [SerializeField] private bool triggerOnEnter = true;
    [SerializeField] private string interactionPrompt = "<b><color=#FFD700>[E]</color></b> Enter";

    [Header("Void Drop Transition")]
    [SerializeField] private bool useVoidDropTransition = true;
    [SerializeField] private VoidDropTransitionSettings voidDropTransition =
        VoidDropTransitionSettings.CreateDefault();

    private bool transitionRequested;

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
            nextSceneName = "Level45";

        voidDropTransition = VoidDropTransitionSettings.CreateDefault();
    }

    private void OnValidate()
    {
        if (voidDropTransition.dropDuration <= 0f &&
            voidDropTransition.fadeOutDuration <= 0f &&
            voidDropTransition.fadeInDuration <= 0f)
        {
            voidDropTransition = VoidDropTransitionSettings.CreateDefault();
        }
    }

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
        if (transitionRequested || SceneTransitionController.IsTransitionRunning)
            return;

        Debug.Log($"Transition to next level: {nextSceneName}");

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log("Next scene name is empty, transition placeholder triggered.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            transitionRequested = true;

            if (useVoidDropTransition &&
                SceneTransitionController.TryBeginVoidDropTransition(nextSceneName, voidDropTransition))
            {
                return;
            }

            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.Log($"Scene '{nextSceneName}' is not in Build Settings or does not exist. Transition placeholder triggered.");
        }
    }
}
