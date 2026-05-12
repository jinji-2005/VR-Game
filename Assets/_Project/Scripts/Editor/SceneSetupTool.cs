using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneSetupTool
{
    [MenuItem("Tools/Setup P1 Scene")]
    public static void SetupScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("No active scene. Open Assets/backroom.unity first.");
            return;
        }

        Debug.Log("=== Setting up P1 scene ===");

        SetupPlayer();
        SetupCanvas();
        SetupKeycard();
        SetupDoor();
        SetupTransitionTrigger();
        SetupBuildSettings();

        EditorUtility.SetDirty(GameObject.FindObjectOfType<Transform>());
        Debug.Log("=== P1 scene setup complete ===");
    }

    private static void SetupPlayer()
    {
        var go = GameObject.Find("Player");
        if (go == null)
        {
            Debug.LogError("Player GameObject not found in scene.");
            return;
        }

        EnsureComponent<PlayerController>(go);
        EnsureComponent<PlayerInventory>(go);
        var interactor = EnsureComponent<PlayerInteractor>(go);

        var cam = go.transform.Find("PlayerCamera");
        if (cam != null)
        {
            var ctrl = go.GetComponent<PlayerController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("cameraTransform").objectReferenceValue = cam;
            so.ApplyModifiedProperties();

            var intSo = new SerializedObject(interactor);
            intSo.FindProperty("cameraTransform").objectReferenceValue = cam;
            intSo.ApplyModifiedProperties();

            var prompt = GameObject.Find("PromptText")?.GetComponent<InteractionPrompt>();
            if (prompt != null)
            {
                intSo.FindProperty("prompt").objectReferenceValue = prompt;
                intSo.ApplyModifiedProperties();
            }
        }

        Debug.Log("Player setup complete.");
    }

    private static void SetupCanvas()
    {
        var go = GameObject.Find("PromptText");
        if (go == null)
        {
            Debug.LogError("PromptText not found in scene.");
            return;
        }

        EnsureComponent<InteractionPrompt>(go);

        Debug.Log("Canvas setup complete.");
    }

    private static void SetupKeycard()
    {
        var go = GameObject.Find("Backrooms_Keycard");
        if (go == null)
        {
            Debug.LogWarning("Backrooms_Keycard not found in scene.");
            return;
        }

        EnsureComponent<KeycardPickup>(go);

        Debug.Log("Keycard setup complete.");
    }

    private static void SetupDoor()
    {
        var go = GameObject.Find("Backrooms_Door");
        if (go == null)
        {
            Debug.LogWarning("Backrooms_Door not found in scene.");
            return;
        }

        var door = EnsureComponent<LockedDoor>(go);
        var so = new SerializedObject(door);

        var pivot = go.transform.Find("DoorSlab");
        if (pivot != null)
            so.FindProperty("doorPivot").objectReferenceValue = pivot;

        var blocker = pivot?.GetComponent<Collider>();
        if (blocker != null)
            so.FindProperty("doorBlocker").objectReferenceValue = blocker;

        so.ApplyModifiedProperties();

        Debug.Log("Door setup complete.");
    }

    private static void SetupTransitionTrigger()
    {
        var go = GameObject.Find("LevelTransition");
        if (go == null)
        {
            Debug.LogError("LevelTransition not found in scene.");
            return;
        }

        EnsureComponent<LevelTransitionTrigger>(go);

        Debug.Log("Transition trigger setup complete.");
    }

    private static void SetupBuildSettings()
    {
        var scene = SceneManager.GetActiveScene();
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
        {
            if (s.path == scene.path)
            {
                Debug.Log("Scene already in Build Settings.");
                return;
            }
        }

        var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(newScenes, 0);
        newScenes[scenes.Length] = new EditorBuildSettingsScene(scene.path, true);
        EditorBuildSettings.scenes = newScenes;

        Debug.Log("Scene added to Build Settings.");
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var comp = go.GetComponent<T>();
        if (comp == null)
            comp = go.AddComponent<T>();
        return comp;
    }
}
