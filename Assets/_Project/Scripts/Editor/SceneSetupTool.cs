using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneSetupTool
{
    [MenuItem("Tools/Setup P1 Scene")]
    public static void SetupScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Stop Play mode first, then run Setup P1 Scene.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("No active scene open.");
            return;
        }

        Debug.Log("=== Setting up P1 scene ===");

        SetupCanvas();
        SetupPlayer();
        SetupKeycard();
        SetupDoor();
        SetupTransitionTrigger();
        SetupBuildSettings();

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("=== P1 scene setup complete (saved) ===");
    }

    private static void SetupPlayer()
    {
        var go = GameObject.Find("Player");
        if (go == null)
        {
            Debug.LogWarning("Player GameObject not found in scene.");
            return;
        }

        go.tag = "Player";

        EnsureComponent<PlayerController>(go);
        EnsureComponent<PlayerInventory>(go);
        var interactor = EnsureComponent<PlayerInteractor>(go);

        // wire camera reference
        var cam = FindCameraInChildren(go.transform);
        if (cam == null)
            cam = GameObject.Find("PlayerCamera")?.transform;
        if (cam == null)
            cam = GameObject.Find("Main Camera")?.transform;

        if (cam != null)
        {
            var ctrl = go.GetComponent<PlayerController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("cameraTransform").objectReferenceValue = cam;
            so.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("No camera found. Drag camera to PlayerController manually.");
        }

        // wire interactor
        var intSo = new SerializedObject(interactor);
        if (cam != null)
        {
            intSo.FindProperty("cameraTransform").objectReferenceValue = cam;
            intSo.ApplyModifiedProperties();
        }

        var prompt = GameObject.Find("PromptText")?.GetComponent<InteractionPrompt>();
        if (prompt != null)
        {
            intSo.FindProperty("prompt").objectReferenceValue = prompt;
            intSo.ApplyModifiedProperties();
        }

        Debug.Log("Player setup complete.");
    }

    private static Transform FindCameraInChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.GetComponent<Camera>() != null)
                return child;
            var found = FindCameraInChildren(child);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void SetupCanvas()
    {
        var promptGo = GameObject.Find("PromptText");
        if (promptGo == null)
        {
            promptGo = CreatePromptUI();
        }

        EnsureComponent<InteractionPrompt>(promptGo);

        Debug.Log("Canvas setup complete.");
    }

    private static GameObject CreatePromptUI()
    {
        var canvasGo = new GameObject("InteractionCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);

        var promptGo = new GameObject("PromptText", typeof(RectTransform));
        promptGo.layer = LayerMask.NameToLayer("UI");
        promptGo.transform.SetParent(canvasGo.transform, false);

        var rt = promptGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -120);
        rt.sizeDelta = new Vector2(400, 50);

        Debug.Log("Created InteractionCanvas + PromptText.");

        return promptGo;
    }

    private static void SetupKeycard()
    {
        var go = GameObject.Find("Backrooms_Keycard");
        if (go == null)
            go = GameObject.Find("PF_Key0");
        if (go == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Backrooms/Backrooms_Keycard.prefab");
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(-6f, 1f, -6f);
                Debug.Log("Instantiated Backrooms_Keycard from prefab. Adjust position in scene if needed.");
            }
            else
            {
                Debug.LogWarning("Keycard prefab not found and no keycard in scene.");
                return;
            }
        }

        EnsureComponent<KeycardPickup>(go);

        if (go.GetComponent<Collider>() == null)
            go.AddComponent<BoxCollider>();

        Debug.Log("Keycard setup complete.");
    }

    private static void SetupDoor()
    {
        var go = GameObject.Find("Backrooms_Door");
        if (go == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Backrooms/Backrooms_Door.prefab");
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(0f, 1.45f, 3f);
                Debug.Log("Instantiated Backrooms_Door from prefab. Adjust position in scene if needed.");
            }
            else
            {
                Debug.LogWarning("Door prefab not found and no Backrooms_Door in scene.");
                return;
            }
        }

        var door = EnsureComponent<LockedDoor>(go);
        var so = new SerializedObject(door);

        var pivot = go.transform.Find("DoorSlab");
        if (pivot == null)
            pivot = go.transform;

        so.FindProperty("doorPivot").objectReferenceValue = pivot;

        var blocker = pivot.GetComponent<Collider>();
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
            go = new GameObject("LevelTransition");
            go.transform.position = new Vector3(0f, 1f, 6f);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2f, 2f, 1f);
            Debug.Log("Created LevelTransition trigger. Adjust position/size in scene if needed.");
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
