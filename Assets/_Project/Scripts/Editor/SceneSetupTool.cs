using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneSetupTool
{
    private const string InteractableLayerName = "Interactable";

    [MenuItem("Tools/Setup Control Guide")]
    public static void SetupControlGuide()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Stop Play mode first, then run Setup Control Guide.");
            return;
        }

        var existing = Object.FindFirstObjectByType<ControlGuideDisplay>();
        if (existing != null)
        {
            Debug.Log("ControlGuideDisplay already exists in scene, skipping.");
            return;
        }

        var canvasGo = CreateControlGuideUI();
        var display = canvasGo.AddComponent<ControlGuideDisplay>();

        var so = new SerializedObject(display);
        var panel = canvasGo.transform.Find("ControlGuidePanel");
        if (panel != null)
            so.FindProperty("guidePanel").objectReferenceValue = panel.gameObject;
        var text = panel?.Find("ControlGuideText")?.GetComponent<TextMeshProUGUI>();
        if (text != null)
            so.FindProperty("guideText").objectReferenceValue = text;
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("ControlGuideDisplay created. Adjust position/text in the Inspector.");
    }

    private static GameObject CreateControlGuideUI()
    {
        var canvasGo = new GameObject("ControlGuideCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Panel with CanvasGroup for fade
        var panelGo = new GameObject("ControlGuidePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGo.layer = LayerMask.NameToLayer("UI");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 1f);
        panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0, -80);
        panelRt.sizeDelta = new Vector2(500, 200);
        panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        // Text
        var textGo = new GameObject("ControlGuideText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.layer = LayerMask.NameToLayer("UI");
        textGo.transform.SetParent(panelGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28;
        tmp.color = Color.white;
        tmp.text = "WASD - Move\nSpace - Jump\nHold Left Shift while moving - Run\nC - Crouch";

        Debug.Log("Created ControlGuideCanvas with panel and text.");
        return canvasGo;
    }

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
        }

        SetLayerMaskProperty(intSo, "interactionMask", ~0);
        SetLayerMaskProperty(intSo, "blockingMask", ~0);
        intSo.ApplyModifiedProperties();

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

        var prompt = EnsureComponent<InteractionPrompt>(promptGo);
        var so = new SerializedObject(prompt);

        var hintPanel = promptGo.transform.Find("HintPanel");
        if (hintPanel != null)
            so.FindProperty("hintPanel").objectReferenceValue = hintPanel.gameObject;

        var hintText = hintPanel?.Find("HintText")?.GetComponent<TextMeshProUGUI>();
        if (hintText != null)
            so.FindProperty("hintText").objectReferenceValue = hintText;

        so.ApplyModifiedProperties();

        Debug.Log("Canvas setup complete.");
    }

    private static GameObject CreatePromptUI()
    {
        var canvasGo = new GameObject("InteractionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);

        var promptGo = new GameObject("PromptText", typeof(RectTransform));
        promptGo.layer = LayerMask.NameToLayer("UI");
        promptGo.transform.SetParent(canvasGo.transform, false);
        var rt = promptGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -120);
        rt.sizeDelta = Vector2.zero;

        // HintPanel — background that wraps text tightly
        var hintPanelGo = new GameObject("HintPanel", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        hintPanelGo.layer = LayerMask.NameToLayer("UI");
        hintPanelGo.transform.SetParent(promptGo.transform, false);
        var hintPanelRt = hintPanelGo.GetComponent<RectTransform>();
        hintPanelRt.anchorMin = new Vector2(0.5f, 0.5f);
        hintPanelRt.anchorMax = new Vector2(0.5f, 0.5f);
        hintPanelRt.anchoredPosition = Vector2.zero;
        hintPanelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var layout = hintPanelGo.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.childAlignment = TextAnchor.MiddleCenter;
        var fitter = hintPanelGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Hint text
        var hintTextGo = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintTextGo.layer = LayerMask.NameToLayer("UI");
        hintTextGo.transform.SetParent(hintPanelGo.transform, false);
        var tmp = hintTextGo.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 24;
        tmp.color = Color.white;

        Debug.Log("Created InteractionCanvas + PromptText + HintPanel + HintText.");

        return promptGo;
    }

    private static void SetupKeycard()
    {
        // PF_Key0 is the real FBX-based keycard model placed in the scene
        var go = FindFirstObjectByName("PF_Key0", "PF Key", "pf key", "PF_Key", "Backrooms_Keycard");
        if (go == null)
        {
            Debug.LogWarning("No PF_Key0 found in scene. Drag PF_Key0.fbx into the scene first.");
            return;
        }

        EnsureComponent<KeycardPickup>(go);
        SetInteractableLayer(go);

        if (go.GetComponent<Collider>() == null)
            go.AddComponent<BoxCollider>();

        Debug.Log("Keycard setup complete.");
    }

    private static void SetupDoor()
    {
        // Prefer the real door panel inside TstLevel (has MeshCollider for raycast)
        var go = GameObject.Find("TstLvl_Door_C_Door");
        if (go == null)
            go = GameObject.Find("TstLvl_Door_C_Grp");
        if (go == null)
            go = GameObject.Find("Backrooms_Door");
        if (go == null)
            go = GameObject.Find("Door_A_Grp");

        if (go == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Backrooms/Level0/Doors/PF_Level0_Door_A.prefab");
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(-14.5f, 5.662343f, -7.5f);
                Debug.Log("Instantiated PF_Level0_Door_A at correct position.");
            }
            else
            {
                Debug.LogWarning("PF_Level0_Door_A prefab not found.");
                return;
            }
        }

        if (go.GetComponent<LockedDoor>() != null)
        {
            SetInteractableLayer(go);
            Debug.Log($"Door '{go.name}' already has LockedDoor, skipping.");
            return;
        }

        var door = EnsureComponent<LockedDoor>(go);
        SetInteractableLayer(go);
        var so = new SerializedObject(door);

        // For TstLvl_Door_C_Door, the door panel IS the pivot
        // For PF_Level0_Door_A, the pivot is the "Door_A_Door" child
        Transform pivot;
        if (go.name == "TstLvl_Door_C_Door")
        {
            pivot = go.transform;
        }
        else
        {
            pivot = go.transform.Find("Door_A_Door");
            if (pivot == null)
                pivot = go.transform.Find("TstLvl_Door_C_Door");
            if (pivot == null)
                pivot = go.transform;
        }

        so.FindProperty("doorPivot").objectReferenceValue = pivot;

        var blocker = pivot.GetComponent<Collider>();
        if (blocker != null)
            so.FindProperty("doorBlocker").objectReferenceValue = blocker;

        so.ApplyModifiedProperties();

        Debug.Log($"Door setup complete on '{go.name}'.");
    }

    private static int GetLayerMask(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning($"Layer '{layerName}' is missing. Falling back to all layers.");
            return ~0;
        }

        return 1 << layer;
    }

    private static void SetLayerMaskProperty(SerializedObject serializedObject, string propertyName, int bits)
    {
        var property = serializedObject.FindProperty(propertyName);
        var maskBits = property?.FindPropertyRelative("m_Bits");
        if (maskBits != null)
            maskBits.intValue = bits;
    }

    private static void SetInteractableLayer(GameObject go)
    {
        int layer = LayerMask.NameToLayer(InteractableLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"Layer '{InteractableLayerName}' is missing. Add it in Project Settings > Tags and Layers.");
            return;
        }

        go.layer = layer;
    }

    private static GameObject FindFirstObjectByName(params string[] names)
    {
        foreach (string name in names)
        {
            var go = GameObject.Find(name);
            if (go != null)
                return go;
        }

        return null;
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
        SetInteractableLayer(go);

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
