using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransitionTrigger : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "Level1";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

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
