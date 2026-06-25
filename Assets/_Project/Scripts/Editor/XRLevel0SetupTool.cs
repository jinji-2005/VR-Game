using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

[InitializeOnLoad]
public static class XRLevel0SetupTool
{
    private const string ScenePath = "Assets/_Project/Scenes/Level0.unity";
    private const string Level45ScenePath = "Assets/_Project/Scenes/Level45/Level45.unity";
    private const string OutputPath = "Logs/VRRuntimeValidation.json";
    private const string RayMaterialPath = "Assets/_Project/Materials/M_VR_Ray.mat";
    private const string ControllerMaterialPath = "Assets/_Project/Materials/M_XRI_Controller_Demo.mat";
    private const string XRSettingsFolder = "Assets/XR";
    private const string XRGeneralSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
    private const string OpenXRLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";
    private const string XRIPackageName = "com.unity.xr.interaction.toolkit";
    private const string XRIPackageVersion = "2.5.4";
    private const string XRIStarterAssetsSampleName = "Starter Assets";
    private const string XRIDeviceSimulatorSampleName = "XR Device Simulator";
    private const string XRIDeviceSimulatorObjectName = "XR Device Simulator (Optional Trigger Test)";
    private const string XRIOfficialPreviewSetupName = "XR Interaction Setup (Official Preview)";
    private const string XRIOfficialPreviewSimulatorName = "XR Device Simulator (Official Preview)";
    private const string XRIRightActivateActionPath = "XRI RightHand Interaction/Activate";
    private const string PreferredModeKeyPrefix = "VRGame.Scene.PreferredPlayMode.";
    private const string Level0PreferredModeKey = "VRGame.Level0.PreferredPlayMode";
    private const string Level0KeyboardModeValue = "Keyboard";
    private const string Level0OfficialXRIModeValue = "OfficialXRI";
    private const string Level0HybridXRIModeValue = "HybridXRI";
    private const float DefaultHybridCameraHeightOffset = 0.18f;
    private const float ScaledPlayerHybridCameraHeightOffset = 2.28f;
    private const float DefaultHybridMinimumCameraHeight = 1.82f;
    private const float ScaledPlayerHybridMinimumCameraHeight = 4.0f;
    private const float ScaledPlayerHybridOffsetThreshold = 1.1f;
    private const float DefaultHybridControllerVisualPoseScale = 1.0f;
    private const float Level45HybridControllerVisualPoseScale = 1.55f;
    private const float DefaultHybridControllerVisualScale = 1.0f;
    private const float Level45HybridControllerVisualScale = 1.12f;
    private const float DefaultHybridRayVisualDistanceScale = 1.0f;
    private const float Level45HybridRayVisualDistanceScale = 1.55f;
    private const bool UseHybridDesktopCameraPoseProfile = true;

    private sealed class OfficialPlayerEffectBundle
    {
        public AudioSource RunningAudioSource;
        public AudioSource JumpAudioSource;
        public AudioSource DeathAudioSource;
    }

    static XRLevel0SetupTool()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [MenuItem("VR Game/XR/Configure Level0 VR Rig")]
    public static void ConfigureLevel0ForVR()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Stop Play mode first, then configure the VR rig.");
            return;
        }

        ConfigureOpenXRForStandalone();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("Player GameObject was not found in Level0.");
            return;
        }

        player.tag = "Player";

        CharacterController controller = EnsureComponent<CharacterController>(player);
        controller.radius = Mathf.Max(controller.radius, 0.32f);
        controller.height = Mathf.Max(controller.height, 1.7f);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        if (playerCamera == null)
        {
            GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.transform.SetParent(player.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            playerCamera = cameraGo.GetComponent<Camera>();
        }

        playerCamera.tag = "MainCamera";
        playerCamera.stereoTargetEye = StereoTargetEyeMask.Both;
        playerCamera.nearClipPlane = 0.05f;
        Transform cameraTransform = playerCamera.transform;

        Transform leftHand = EnsureChild(player.transform, "XR Left Hand", new Vector3(-0.22f, 1.25f, 0.35f));
        Transform rightHand = EnsureChild(player.transform, "XR Right Hand Ray", new Vector3(0.22f, 1.25f, 0.35f));

        LineRenderer rayLine = EnsureComponent<LineRenderer>(rightHand.gameObject);
        rayLine.useWorldSpace = true;
        rayLine.widthMultiplier = 0.012f;
        rayLine.positionCount = 2;
        rayLine.enabled = false;
        rayLine.material = GetOrCreateRayMaterial();

        InteractionPrompt prompt = FindPrompt();
        AudioSource runningAudioSource = player.GetComponentInChildren<AudioSource>(true);
        PlayerController desktopController = player.GetComponent<PlayerController>();
        PlayerInteractor desktopInteractor = player.GetComponent<PlayerInteractor>();

        VRRigDriver rigDriver = EnsureComponent<VRRigDriver>(player);
        SerializedObject rigSo = new SerializedObject(rigDriver);
        SetObject(rigSo, "cameraTransform", cameraTransform);
        SetObject(rigSo, "leftHandTransform", leftHand);
        SetObject(rigSo, "rightHandTransform", rightHand);
        SetObject(rigSo, "runningAudioSource", runningAudioSource);
        SetObject(rigSo, "desktopMovementSettings", desktopController);
        SetVector3(rigSo, "demoLeftHandLocalPosition", new Vector3(-0.22f, 1.42f, 0.42f));
        SetVector3(rigSo, "demoRightHandLocalPosition", new Vector3(0.22f, 1.42f, 0.42f));
        rigSo.ApplyModifiedProperties();

        VRInteractor vrInteractor = EnsureComponent<VRInteractor>(player);
        SerializedObject interactorSo = new SerializedObject(vrInteractor);
        SetObject(interactorSo, "rayOrigin", rightHand);
        SetObject(interactorSo, "fallbackCameraTransform", cameraTransform);
        SetObject(interactorSo, "prompt", prompt);
        SetObject(interactorSo, "rayLine", rayLine);
        SetLayerMask(interactorSo, "interactionMask", ~0);
        SetLayerMask(interactorSo, "blockingMask", ~0);
        EnsureXRIStarterAssetsImported();
        SetObject(interactorSo, "xriInputActions", FindImportedXRIInputActions());
        interactorSo.ApplyModifiedProperties();

        VRDemoSimulator demoSimulator = EnsureComponent<VRDemoSimulator>(player);
        SerializedObject demoSo = new SerializedObject(demoSimulator);
        SetObject(demoSo, "desktopController", desktopController);
        SetObject(demoSo, "desktopInteractor", desktopInteractor);
        SetObject(demoSo, "vrRigDriver", rigDriver);
        SetObject(demoSo, "vrInteractor", vrInteractor);
        SetObject(demoSo, "leftHandTransform", leftHand);
        SetObject(demoSo, "rightHandTransform", rightHand);
        SetObject(demoSo, "leftControllerVisualPrefab", FindImportedXRIControllerPrefab("XR Controller Left"));
        SetObject(demoSo, "rightControllerVisualPrefab", FindImportedXRIControllerPrefab("XR Controller Right"));
        SetObject(demoSo, "controllerDisplayMaterial", GetOrCreateControllerDisplayMaterial());
        demoSo.ApplyModifiedProperties();

        EnsureXRIDeviceSimulatorImported();
        EnsureXRDeviceSimulatorInScene();

        if (desktopController != null)
        {
            SerializedObject desktopSo = new SerializedObject(desktopController);
            SetObject(desktopSo, "cameraTransform", cameraTransform);
            SetBool(desktopSo, "disableDesktopInputWhenXRActive", true);
            desktopSo.ApplyModifiedProperties();
        }

        if (desktopInteractor != null)
        {
            SerializedObject desktopInteractorSo = new SerializedObject(desktopInteractor);
            SetObject(desktopInteractorSo, "cameraTransform", cameraTransform);
            SetObject(desktopInteractorSo, "prompt", prompt);
            desktopInteractorSo.ApplyModifiedProperties();
        }

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Level0 VR rig configured with XRI controller models, Trigger input bridge, and optional XR Device Simulator.");
    }

    [MenuItem("VR Game/XR/Configure Official XRI Rig in Level0 (Persistent Setup)")]
    public static void ConfigureOfficialXRIRigInLevel0()
    {
        ConfigureOfficialXRIRigInScene(ScenePath, "Level0");
    }

    [MenuItem("VR Game/XR/Configure Official XRI Rig in Level45 (Persistent Setup)")]
    public static void ConfigureOfficialXRIRigInLevel45()
    {
        ConfigureOfficialXRIRigInScene(Level45ScenePath, "Level45");
    }

    [MenuItem("VR Game/XR/Preview Official XRI Rig in Level0")]
    public static void PreviewOfficialXRIRigInLevel0()
    {
        SetPreferredLevel0PlayMode(Level0OfficialXRIModeValue);
        Debug.Log("Level0 will use the official XRI rig during Play Mode. This preference does not modify or save the scene.");
    }

    [MenuItem("VR Game/XR/Preview Hybrid XRI Demo in Level0")]
    public static void PreviewHybridXRIDemoInLevel0()
    {
        SetPreferredLevel0PlayMode(Level0HybridXRIModeValue);
        Debug.Log("Level0 will use the official XRI rig visuals with simplified keyboard/mouse demo controls during Play Mode. This preference does not modify or save the scene.");
    }

    [MenuItem("VR Game/XR/Preview Official XRI Rig in Level45")]
    public static void PreviewOfficialXRIRigInLevel45()
    {
        SetPreferredPlayMode(Level45ScenePath, Level0OfficialXRIModeValue);
        Debug.Log("Level45 will use the official XRI rig during Play Mode. This preference does not modify or save the scene.");
    }

    [MenuItem("VR Game/XR/Preview Hybrid XRI Demo in Level45")]
    public static void PreviewHybridXRIDemoInLevel45()
    {
        SetPreferredPlayMode(Level45ScenePath, Level0HybridXRIModeValue);
        Debug.Log("Level45 will use the official XRI rig visuals with simplified keyboard/mouse demo controls during Play Mode. This preference does not modify or save the scene.");
    }

    private static void ConfigureOfficialXRIRigInScene(string scenePath, string levelName)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Stop Play mode first, then configure the official XRI rig.");
            return;
        }

        ConfigureOpenXRForStandalone();
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        EnsureSceneInBuildSettings(scenePath);
        EnsureXRIStarterAssetsImported();
        EnsureXRIDeviceSimulatorImported();

        GameObject desktopPlayer = FindDesktopPlayerInScene();
        GameObject officialSetup = EnsureOfficialXRISetupInScene();
        GameObject simulator = EnsureOfficialXRDeviceSimulatorInScene();
        if (officialSetup == null || simulator == null)
        {
            Debug.LogError("Official XRI preview could not be created. Import the XRI Starter Assets and XR Device Simulator samples, then try again.");
            return;
        }

        if (desktopPlayer != null)
        {
            desktopPlayer.tag = "Player";
            officialSetup.transform.SetPositionAndRotation(desktopPlayer.transform.position, desktopPlayer.transform.rotation);
            ConfigureOfficialXRIIntegration(officialSetup, desktopPlayer);
            EnsureOfficialInteractablesForExistingGameplay();
        }
        else
        {
            Debug.LogWarning($"{levelName} does not contain a desktop PlayerController root. The official XRI rig was created, but hybrid controls could not copy desktop movement settings.");
        }

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"Official XRI rig configured in {levelName}. Use the {levelName} Preview Hybrid/Official menu item, or restore keyboard mode, to choose the next Play Mode without saving scene state.");
    }

    [MenuItem("VR Game/XR/Restore Existing Player Mode")]
    public static void RestoreExistingPlayerMode()
    {
        SetPreferredLevel0PlayMode(Level0KeyboardModeValue);
        Debug.Log("Level0 will use the existing keyboard player during Play Mode. This preference does not modify or save the scene.");
    }

    [MenuItem("VR Game/XR/Restore Existing Player Mode in Level45")]
    public static void RestoreExistingPlayerModeInLevel45()
    {
        SetPreferredPlayMode(Level45ScenePath, Level0KeyboardModeValue);
        Debug.Log("Level45 will use the existing keyboard player during Play Mode. This preference does not modify or save the scene.");
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
            return;

        if (!IsSupportedPreviewScene(SceneManager.GetActiveScene().path))
            return;

        ApplyPreferredLevelPlayModeInPlay();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
            return;

        if (!IsSupportedPreviewScene(scene.path))
            return;

        EditorApplication.delayCall += ApplyPreferredLevelPlayModeInPlay;
    }

    private static void SetPreferredLevel0PlayMode(string mode)
    {
        SetPreferredPlayMode(ScenePath, mode);
        EditorPrefs.SetString(Level0PreferredModeKey, mode);
    }

    private static void SetPreferredPlayMode(string scenePath, string mode)
    {
        EditorPrefs.SetString(GetPreferredPlayModeKey(scenePath), mode);
    }

    private static string GetPreferredPlayMode(string scenePath)
    {
        string key = GetPreferredPlayModeKey(scenePath);
        if (EditorPrefs.HasKey(key))
            return EditorPrefs.GetString(key, Level0KeyboardModeValue);

        if (string.Equals(scenePath, ScenePath, StringComparison.OrdinalIgnoreCase) &&
            EditorPrefs.HasKey(Level0PreferredModeKey))
        {
            return EditorPrefs.GetString(Level0PreferredModeKey, Level0KeyboardModeValue);
        }

        return Level0KeyboardModeValue;
    }

    private static string GetPreferredPlayModeKey(string scenePath)
    {
        return PreferredModeKeyPrefix + scenePath.Replace('/', '.').Replace('\\', '.');
    }

    private static bool IsSupportedPreviewScene(string scenePath)
    {
        return string.Equals(scenePath, ScenePath, StringComparison.OrdinalIgnoreCase) ||
            IsLevel45ScenePath(scenePath);
    }

    private static bool IsLevel45ScenePath(string scenePath)
    {
        return string.Equals(scenePath, Level45ScenePath, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                return;
        }

        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(newScenes, 0);
        newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = newScenes;
    }

    private static void ApplyPreferredLevelPlayModeInPlay()
    {
        if (!Application.isPlaying)
            return;

        string scenePath = SceneManager.GetActiveScene().path;
        if (!IsSupportedPreviewScene(scenePath))
            return;

        ApplyLevelPlayMode(scenePath, GetPreferredPlayMode(scenePath));
    }

    private static void ApplyLevelPlayMode(string scenePath, string mode)
    {
        bool useOfficialXRI = string.Equals(mode, Level0OfficialXRIModeValue, StringComparison.Ordinal);
        bool useHybridXRI = string.Equals(mode, Level0HybridXRIModeValue, StringComparison.Ordinal);
        bool useOfficialRig = useOfficialXRI || useHybridXRI;
        string levelName = Path.GetFileNameWithoutExtension(scenePath);

        GameObject desktopPlayer = FindDesktopPlayerInScene();
        GameObject officialSetup = FindSceneRootStartingWith("XR Interaction Setup");
        GameObject simulator = FindSceneRootStartingWith("XR Device Simulator");
        bool hasOfficialRig = officialSetup != null && simulator != null;

        if (useOfficialRig && !hasOfficialRig)
        {
            TryCreateRuntimeOfficialRigForPreview(levelName);
            officialSetup = FindSceneRootStartingWith("XR Interaction Setup");
            simulator = FindSceneRootStartingWith("XR Device Simulator");
            hasOfficialRig = officialSetup != null && simulator != null;
        }

        if (useOfficialRig && !hasOfficialRig)
        {
            if (desktopPlayer != null)
                desktopPlayer.SetActive(true);

            Debug.LogWarning($"Official XRI preview is selected, but {levelName} does not contain the official XRI setup or simulator. Keeping the existing keyboard player active. Run VR Game/XR/Configure Official XRI Rig in {levelName} (Persistent Setup), then enter Play Mode again.");
            return;
        }

        if (useOfficialRig && hasOfficialRig && desktopPlayer != null && officialSetup != null)
        {
            officialSetup.transform.SetPositionAndRotation(
                desktopPlayer.transform.position,
                desktopPlayer.transform.rotation);
            ConfigureOfficialXRIIntegration(officialSetup, desktopPlayer);
            EnsureOfficialInteractablesForExistingGameplay();
        }

        if (desktopPlayer != null)
            desktopPlayer.SetActive(!useOfficialRig);

        if (officialSetup != null)
        {
            officialSetup.SetActive(useOfficialRig);
            XRIHybridDemoDriver hybridDriver = officialSetup.GetComponent<XRIHybridDemoDriver>();
            if (hybridDriver == null && useHybridXRI)
                hybridDriver = GetOrCreateDisabledHybridDriver(officialSetup);

            if (hybridDriver != null)
                hybridDriver.enabled = useHybridXRI;
        }

        if (simulator != null)
        {
            simulator.SetActive(useOfficialRig);
            SetXRDeviceSimulatorInputEnabled(simulator, !useHybridXRI);
            SetXRDeviceSimulatorUIVisible(simulator, useOfficialXRI);
        }

        if (useOfficialRig && !hasOfficialRig)
            Debug.LogWarning($"Official XRI preview is selected, but {levelName} does not contain the official XRI setup or simulator. Run VR Game/XR/Configure Official XRI Rig in {levelName} (Persistent Setup).");
    }

    private static void TryCreateRuntimeOfficialRigForPreview(string levelName)
    {
        GameObject desktopPlayer = FindDesktopPlayerInScene();
        if (desktopPlayer == null)
            return;

        EnsureXRIStarterAssetsImported();
        EnsureXRIDeviceSimulatorImported();

        GameObject officialSetup = EnsureOfficialXRISetupInScene();
        GameObject simulator = EnsureOfficialXRDeviceSimulatorInScene();
        if (officialSetup == null || simulator == null)
            return;

        officialSetup.transform.SetPositionAndRotation(desktopPlayer.transform.position, desktopPlayer.transform.rotation);
        ConfigureOfficialXRIIntegration(officialSetup, desktopPlayer);
        EnsureOfficialInteractablesForExistingGameplay();

        Debug.Log($"{levelName} runtime official XRI preview rig created for this Play Mode session.");
    }

    private static void SetXRDeviceSimulatorInputEnabled(GameObject simulator, bool enabled)
    {
        foreach (MonoBehaviour behaviour in simulator.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            Type behaviourType = behaviour.GetType();
            if (string.Equals(behaviourType.Name, "XRDeviceSimulator", StringComparison.Ordinal) ||
                string.Equals(behaviourType.FullName, "UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator", StringComparison.Ordinal))
            {
                behaviour.enabled = enabled;
            }
        }
    }

    private static void SetXRDeviceSimulatorUIVisible(GameObject simulator, bool visible)
    {
        if (simulator == null)
            return;

        foreach (MonoBehaviour behaviour in simulator.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            Type behaviourType = behaviour.GetType();
            if (string.Equals(behaviourType.Name, "XRDeviceSimulatorUI", StringComparison.Ordinal) ||
                behaviourType.Name.StartsWith("XRDeviceSimulator", StringComparison.Ordinal) &&
                behaviourType.Name.EndsWith("UI", StringComparison.Ordinal))
            {
                behaviour.enabled = visible;
            }
        }

        foreach (Canvas canvas in simulator.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = visible;

        foreach (GraphicRaycaster raycaster in simulator.GetComponentsInChildren<GraphicRaycaster>(true))
            raycaster.enabled = visible;

        foreach (CanvasGroup canvasGroup in simulator.GetComponentsInChildren<CanvasGroup>(true))
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private static void EnsureXRIStarterAssetsImported()
    {
        if (FindImportedXRIControllerPrefab("XR Controller Left") != null &&
            FindImportedXRIControllerPrefab("XR Controller Right") != null &&
            FindImportedXRInteractionSetupPrefab() != null)
            return;

        foreach (Sample sample in Sample.FindByPackage(XRIPackageName, XRIPackageVersion))
        {
            if (!string.Equals(sample.displayName, XRIStarterAssetsSampleName, StringComparison.Ordinal))
                continue;

            if (!sample.Import(Sample.ImportOptions.OverridePreviousImports))
            {
                Debug.LogWarning("Unable to import XR Interaction Toolkit Starter Assets automatically. Import it from Package Manager > XR Interaction Toolkit > Samples.");
                return;
            }

            AssetDatabase.Refresh();
            return;
        }

        Debug.LogWarning("XR Interaction Toolkit Starter Assets sample was not found. Import it from Package Manager and run this setup menu again.");
    }

    private static GameObject FindImportedXRIControllerPrefab(string prefabName)
    {
        string[] prefabGuids = AssetDatabase.FindAssets($"{prefabName} t:Prefab", new[] { "Assets/Samples/XR Interaction Toolkit" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith($"/Prefabs/Controllers/{prefabName}.prefab", StringComparison.OrdinalIgnoreCase))
                continue;

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return null;
    }

    private static InputActionAsset FindImportedXRIInputActions()
    {
        string[] actionAssetGuids = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset", new[] { "Assets/Samples/XR Interaction Toolkit" });
        foreach (string guid in actionAssetGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            InputActionAsset actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (actionAsset != null && actionAsset.FindAction(XRIRightActivateActionPath) != null)
                return actionAsset;
        }

        Debug.LogWarning($"XRI input action asset containing {XRIRightActivateActionPath} was not found.");
        return null;
    }

    private static GameObject FindImportedXRInteractionSetupPrefab()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("XR Interaction Setup t:Prefab", new[] { "Assets/Samples/XR Interaction Toolkit" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/Starter Assets/Prefabs/XR Interaction Setup.prefab", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return null;
    }

    private static void EnsureXRIDeviceSimulatorImported()
    {
        if (FindImportedXRDeviceSimulatorPrefab() != null)
            return;

        foreach (Sample sample in Sample.FindByPackage(XRIPackageName, XRIPackageVersion))
        {
            if (!string.Equals(sample.displayName, XRIDeviceSimulatorSampleName, StringComparison.Ordinal))
                continue;

            if (!sample.Import(Sample.ImportOptions.OverridePreviousImports))
                Debug.LogWarning("Unable to import the XR Device Simulator sample automatically. Import it from Package Manager > XR Interaction Toolkit > Samples.");

            AssetDatabase.Refresh();
            return;
        }

        Debug.LogWarning("XR Device Simulator sample was not found. Import it from Package Manager and run this setup menu again.");
    }

    private static GameObject FindImportedXRDeviceSimulatorPrefab()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("XR Device Simulator t:Prefab", new[] { "Assets/Samples/XR Interaction Toolkit" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/XR Device Simulator/XR Device Simulator.prefab", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return null;
    }

    private static void EnsureXRDeviceSimulatorInScene()
    {
        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObject.name.StartsWith("XR Device Simulator", StringComparison.Ordinal))
                return;
        }

        GameObject simulatorPrefab = FindImportedXRDeviceSimulatorPrefab();
        if (simulatorPrefab == null)
            return;

        GameObject simulator = PrefabUtility.InstantiatePrefab(simulatorPrefab, SceneManager.GetActiveScene()) as GameObject;
        if (simulator != null)
        {
            simulator.name = XRIDeviceSimulatorObjectName;
            simulator.SetActive(false);
        }
    }

    private static GameObject EnsureOfficialXRISetupInScene()
    {
        GameObject existing = FindSceneRootStartingWith("XR Interaction Setup");
        if (existing != null)
            return existing;

        GameObject setupPrefab = FindImportedXRInteractionSetupPrefab();
        if (setupPrefab == null)
            return null;

        GameObject setup = PrefabUtility.InstantiatePrefab(setupPrefab, SceneManager.GetActiveScene()) as GameObject;
        if (setup != null)
            setup.name = XRIOfficialPreviewSetupName;

        return setup;
    }

    private static GameObject EnsureOfficialXRDeviceSimulatorInScene()
    {
        GameObject existing = FindSceneRootStartingWith("XR Device Simulator");
        if (existing != null)
        {
            existing.name = XRIOfficialPreviewSimulatorName;
            return existing;
        }

        GameObject simulatorPrefab = FindImportedXRDeviceSimulatorPrefab();
        if (simulatorPrefab == null)
            return null;

        GameObject simulator = PrefabUtility.InstantiatePrefab(simulatorPrefab, SceneManager.GetActiveScene()) as GameObject;
        if (simulator != null)
            simulator.name = XRIOfficialPreviewSimulatorName;

        return simulator;
    }

    private static void ConfigureOfficialXRIIntegration(GameObject officialSetup, GameObject desktopPlayer)
    {
        ConfigureDesktopPlayerIntegration(desktopPlayer);
        bool isLevel45 = IsLevel45ScenePath(SceneManager.GetActiveScene().path);

        PlayerController desktopController = desktopPlayer.GetComponent<PlayerController>();
        CharacterController desktopCharacter = desktopPlayer.GetComponent<CharacterController>();
        Camera desktopCamera = desktopPlayer.GetComponentInChildren<Camera>(true);
        InputActionAsset actions = FindImportedXRIInputActions();
        InteractionPrompt prompt = FindOrCreatePrompt();

        XRRayInteractor leftHandRay = FindHandRayInteractor(officialSetup, "Left");
        ActionBasedController leftHandController = leftHandRay != null
            ? leftHandRay.GetComponentInParent<ActionBasedController>(true)
            : null;
        XRRayInteractor rightHandRay = FindHandRayInteractor(officialSetup, "Right");
        ActionBasedController rightHandController = rightHandRay != null
            ? rightHandRay.GetComponentInParent<ActionBasedController>(true)
            : null;

        XRIOfficialInteractableBridge bridge = EnsureComponent<XRIOfficialInteractableBridge>(officialSetup);
        SerializedObject bridgeSo = new SerializedObject(bridge);
        SetObject(bridgeSo, "leftHandRayInteractor", leftHandRay);
        SetObject(bridgeSo, "leftHandController", leftHandController);
        SetObject(bridgeSo, "rightHandRayInteractor", rightHandRay);
        SetObject(bridgeSo, "rightHandController", rightHandController);
        SetObject(bridgeSo, "interactionOwner", desktopPlayer);
        SetObject(bridgeSo, "prompt", prompt);
        SetLayerMask(bridgeSo, "interactionMask", ~0);
        SetLayerMask(bridgeSo, "blockingMask", ~0);
        bridgeSo.ApplyModifiedProperties();

        ContinuousMoveProviderBase moveProvider = officialSetup.GetComponentInChildren<ContinuousMoveProviderBase>(true);
        CharacterController xriCharacter = officialSetup.GetComponentInChildren<CharacterController>(true);
        CharacterControllerDriver driver = officialSetup.GetComponentInChildren<CharacterControllerDriver>(true);
        Camera xriCamera = officialSetup.GetComponentInChildren<Camera>(true);

        NormalizeOfficialRigPose(officialSetup, desktopPlayer, desktopCharacter, desktopCamera, xriCharacter, xriCamera);

        if (xriCharacter != null)
            xriCharacter.gameObject.tag = "Player";

        if (desktopController != null && moveProvider != null)
            moveProvider.moveSpeed = desktopController.WalkSpeed;

        if (desktopCharacter != null && xriCharacter != null)
        {
            xriCharacter.radius = desktopCharacter.radius;
            xriCharacter.stepOffset = desktopCharacter.stepOffset;
            xriCharacter.slopeLimit = desktopCharacter.slopeLimit;
            xriCharacter.skinWidth = desktopCharacter.skinWidth;

            if (!isLevel45)
            {
                xriCharacter.height = desktopCharacter.height;
                xriCharacter.center = desktopCharacter.center;
            }
        }

        if (desktopController != null && driver != null)
        {
            driver.minHeight = desktopController.CrouchHeight;
            if (!isLevel45 && desktopCharacter != null)
                driver.maxHeight = desktopCharacter.height;
        }

        if (xriCamera != null)
            xriCamera.nearClipPlane = 0.05f;

        OfficialPlayerEffectBundle effects = SynchronizeOfficialPlayerEffects(
            officialSetup,
            desktopPlayer,
            desktopController,
            xriCharacter,
            xriCamera);

        XRIOfficialPlayerTuning tuning = EnsureComponent<XRIOfficialPlayerTuning>(officialSetup);
        SerializedObject tuningSo = new SerializedObject(tuning);
        SetObject(tuningSo, "moveProvider", moveProvider);
        SetObject(tuningSo, "bodyController", xriCharacter);
        SetObject(tuningSo, "xriInputActions", actions);
        SetObject(tuningSo, "runningAudioSource", effects.RunningAudioSource);
        if (desktopController != null)
        {
            SetFloat(tuningSo, "walkSpeed", desktopController.WalkSpeed);
            SetFloat(tuningSo, "sprintMultiplier", desktopController.SprintMultiplier);
            SetFloat(tuningSo, "crouchHeight", desktopController.CrouchHeight);
            SetFloat(tuningSo, "crouchSpeed", desktopController.CrouchSpeed);
        }
        tuningSo.ApplyModifiedProperties();

        XRIHybridDemoDriver hybridDriver = GetOrCreateDisabledHybridDriver(officialSetup);
        SerializedObject hybridSo = new SerializedObject(hybridDriver);
        SetObject(hybridSo, "bodyController", xriCharacter);
        SetObject(hybridSo, "xriCamera", xriCamera);
        SetObject(hybridSo, "leftHandController", leftHandController);
        SetObject(hybridSo, "rightHandController", rightHandController);
        SetObject(hybridSo, "leftHandTransform", leftHandController != null ? leftHandController.transform : null);
        SetObject(hybridSo, "rightHandTransform", rightHandController != null ? rightHandController.transform : null);
        SetObject(hybridSo, "interactableBridge", bridge);
        SetObject(hybridSo, "interactionOwner", desktopPlayer);
        SetObject(hybridSo, "leftControllerVisualPrefab", FindImportedXRIControllerPrefab("XR Controller Left"));
        SetObject(hybridSo, "rightControllerVisualPrefab", FindImportedXRIControllerPrefab("XR Controller Right"));
        SetObject(hybridSo, "runningAudioSource", effects.RunningAudioSource);
        SetObject(hybridSo, "jumpAudioSource", effects.JumpAudioSource);
        SetBool(hybridSo, "lockCameraLocalPose", true);
        SetBool(hybridSo, "resetCameraOffsetParent", true);
        SetFloat(hybridSo, "cameraHeightOffset", ResolveHybridCameraHeightOffset(desktopPlayer));
        SetFloat(hybridSo, "minimumCameraHeight", ResolveHybridMinimumCameraHeight(desktopPlayer));
        SetBool(hybridSo, "logPoseHeightsOnEnable", true);
        SetVector3(hybridSo, "cameraLocalPosition", ResolveHybridCameraLocalPosition(desktopPlayer, desktopCamera, desktopCharacter, isLevel45));
        SetVector3(hybridSo, "cameraLocalEuler", ResolveHybridCameraLocalEuler(desktopPlayer, desktopCamera, isLevel45));
        SetFloat(hybridSo, "controllerVisualPoseScale", ResolveHybridControllerVisualPoseScale(isLevel45));
        SetFloat(hybridSo, "controllerVisualScale", ResolveHybridControllerVisualScale(isLevel45));
        SetFloat(hybridSo, "rayVisualDistanceScale", ResolveHybridRayVisualDistanceScale(isLevel45));
        if (desktopController != null)
        {
            SetFloat(hybridSo, "walkSpeed", desktopController.WalkSpeed);
            SetFloat(hybridSo, "sprintMultiplier", desktopController.SprintMultiplier);
            SetFloat(hybridSo, "gravity", desktopController.Gravity);
            SetFloat(hybridSo, "jumpForce", desktopController.JumpForce);
            SetFloat(hybridSo, "crouchHeight", desktopController.CrouchHeight);
            SetFloat(hybridSo, "crouchSpeed", desktopController.CrouchSpeed);
        }
        hybridSo.ApplyModifiedProperties();

        if (rightHandRay == null || rightHandController == null || leftHandRay == null || leftHandController == null)
            Debug.LogWarning("One or more official XRI hand rays/controllers were not found. Key and door Trigger bridging may be incomplete.");
    }

    private static void NormalizeOfficialRigPose(
        GameObject officialSetup,
        GameObject desktopPlayer,
        CharacterController desktopCharacter,
        Camera desktopCamera,
        CharacterController xriCharacter,
        Camera xriCamera)
    {
        if (officialSetup != null && desktopPlayer != null)
        {
            officialSetup.transform.SetPositionAndRotation(
                desktopPlayer.transform.position,
                desktopPlayer.transform.rotation);
        }

        if (xriCharacter != null && officialSetup != null && xriCharacter.transform != officialSetup.transform)
        {
            xriCharacter.transform.localPosition = Vector3.zero;
            xriCharacter.transform.localRotation = Quaternion.identity;
            xriCharacter.transform.localScale = Vector3.one;
        }

        if (xriCamera == null)
            return;

        Transform cameraParent = xriCamera.transform.parent;
        if (IsCameraOffsetTransform(cameraParent))
        {
            cameraParent.localPosition = Vector3.zero;
            cameraParent.localRotation = Quaternion.identity;
            cameraParent.localScale = Vector3.one;
        }

        xriCamera.transform.localPosition = ResolveDesktopCameraLocalPosition(
            desktopPlayer,
            desktopCamera,
            desktopCharacter);
        xriCamera.transform.localRotation = Quaternion.identity;
        xriCamera.transform.localScale = Vector3.one;
    }

    private static Vector3 ResolveDesktopCameraLocalPosition(
        GameObject desktopPlayer,
        Camera desktopCamera,
        CharacterController desktopCharacter)
    {
        if (desktopPlayer != null && desktopCamera != null)
        {
            float worldEyeHeight = desktopCamera.transform.position.y - desktopPlayer.transform.position.y;
            if (!float.IsNaN(worldEyeHeight) && worldEyeHeight > 0.1f)
                return new Vector3(0f, worldEyeHeight, 0f);
        }

        if (desktopCamera != null)
            return new Vector3(0f, Mathf.Max(0.1f, desktopCamera.transform.localPosition.y), 0f);

        if (desktopCharacter != null)
            return new Vector3(0f, Mathf.Max(0.1f, desktopCharacter.height * 0.9f), 0f);

        return new Vector3(0f, 1.4f, 0f);
    }

    private static Vector3 ResolveHybridCameraLocalPosition(
        GameObject desktopPlayer,
        Camera desktopCamera,
        CharacterController desktopCharacter,
        bool isLevel45)
    {
        Vector3 fallback = ResolveDesktopCameraLocalPosition(desktopPlayer, desktopCamera, desktopCharacter);
        if (!isLevel45 || !UseHybridDesktopCameraPoseProfile || desktopPlayer == null || desktopCamera == null)
            return fallback;

        Vector3 localFromDesktopCamera =
            Quaternion.Inverse(desktopPlayer.transform.rotation) *
            (desktopCamera.transform.position - desktopPlayer.transform.position);
        if (float.IsNaN(localFromDesktopCamera.x) ||
            float.IsNaN(localFromDesktopCamera.y) ||
            float.IsNaN(localFromDesktopCamera.z))
        {
            return fallback;
        }

        return localFromDesktopCamera;
    }

    private static Vector3 ResolveHybridCameraLocalEuler(GameObject desktopPlayer, Camera desktopCamera, bool isLevel45)
    {
        if (!isLevel45 || !UseHybridDesktopCameraPoseProfile || desktopPlayer == null || desktopCamera == null)
            return Vector3.zero;

        return desktopCamera.transform.localEulerAngles;
    }

    private static float ResolveHybridCameraHeightOffset(GameObject desktopPlayer)
    {
        if (desktopPlayer != null && desktopPlayer.transform.lossyScale.y > ScaledPlayerHybridOffsetThreshold)
            return ScaledPlayerHybridCameraHeightOffset;

        return DefaultHybridCameraHeightOffset;
    }

    private static float ResolveHybridMinimumCameraHeight(GameObject desktopPlayer)
    {
        if (desktopPlayer != null && desktopPlayer.transform.lossyScale.y > ScaledPlayerHybridOffsetThreshold)
            return ScaledPlayerHybridMinimumCameraHeight;

        return DefaultHybridMinimumCameraHeight;
    }

    private static float ResolveHybridControllerVisualPoseScale(bool isLevel45)
    {
        return isLevel45 ? Level45HybridControllerVisualPoseScale : DefaultHybridControllerVisualPoseScale;
    }

    private static float ResolveHybridControllerVisualScale(bool isLevel45)
    {
        return isLevel45 ? Level45HybridControllerVisualScale : DefaultHybridControllerVisualScale;
    }

    private static float ResolveHybridRayVisualDistanceScale(bool isLevel45)
    {
        return isLevel45 ? Level45HybridRayVisualDistanceScale : DefaultHybridRayVisualDistanceScale;
    }

    private static bool IsCameraOffsetTransform(Transform candidate)
    {
        if (candidate == null)
            return false;

        return candidate.name.IndexOf("Camera Offset", StringComparison.OrdinalIgnoreCase) >= 0 ||
            candidate.name.IndexOf("CameraFloorOffset", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static OfficialPlayerEffectBundle SynchronizeOfficialPlayerEffects(
        GameObject officialSetup,
        GameObject desktopPlayer,
        PlayerController desktopController,
        CharacterController xriCharacter,
        Camera xriCamera)
    {
        OfficialPlayerEffectBundle effects = new OfficialPlayerEffectBundle();
        if (officialSetup == null || desktopPlayer == null)
            return effects;

        Camera desktopCamera = desktopPlayer.GetComponentInChildren<Camera>(true);
        SynchronizeCameraPresentation(desktopCamera, xriCamera, officialSetup);

        Transform effectsRoot = EnsureMirroredEffectsRoot(
            xriCharacter != null ? xriCharacter.transform : officialSetup.transform);
        if (effectsRoot == null)
            return effects;

        effects.RunningAudioSource = CloneSerializedAudioSource(
            desktopController,
            "runningAudioSource",
            effectsRoot,
            "Running Audio Source");
        effects.JumpAudioSource = CloneSerializedAudioSource(
            desktopController,
            "jumpAudioSource",
            effectsRoot,
            "Jump Audio Source");

        PlayerRespawnController desktopRespawn = desktopPlayer.GetComponent<PlayerRespawnController>();
        if (desktopRespawn != null && xriCharacter != null)
        {
            effects.DeathAudioSource = CloneSerializedAudioSource(
                desktopRespawn,
                "deathAudioSource",
                effectsRoot,
                "Death Audio Source");

            PlayerRespawnController xriRespawn = EnsureComponent<PlayerRespawnController>(xriCharacter.gameObject);
            CopyComponentSerializedValues(desktopRespawn, xriRespawn);
            SerializedObject respawnSo = new SerializedObject(xriRespawn);
            SetObject(respawnSo, "deathAudioSource", effects.DeathAudioSource);
            respawnSo.ApplyModifiedProperties();
        }

        return effects;
    }

    private static void SynchronizeCameraPresentation(Camera desktopCamera, Camera xriCamera, GameObject officialSetup)
    {
        if (desktopCamera == null || xriCamera == null)
            return;

        CopyComponentSerializedValues(desktopCamera, xriCamera);
        xriCamera.name = "Main Camera";
        xriCamera.tag = "MainCamera";
        xriCamera.stereoTargetEye = StereoTargetEyeMask.Both;
        xriCamera.nearClipPlane = Mathf.Min(xriCamera.nearClipPlane, 0.05f);

        foreach (Component sourceComponent in desktopCamera.GetComponents<Component>())
        {
            if (sourceComponent == null ||
                sourceComponent is Transform ||
                sourceComponent is Camera)
            {
                continue;
            }

            if (sourceComponent is AudioListener sourceListener)
            {
                AudioListener targetListener = EnsureComponent<AudioListener>(xriCamera.gameObject);
                targetListener.enabled = sourceListener.enabled;
                continue;
            }

            Type componentType = sourceComponent.GetType();
            Component targetComponent = xriCamera.GetComponent(componentType);
            if (targetComponent != null)
            {
                CopyComponentSerializedValues(sourceComponent, targetComponent);
                continue;
            }

            UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(xriCamera.gameObject);
        }

        EnsureSingleOfficialAudioListener(officialSetup, xriCamera);
    }

    private static void EnsureSingleOfficialAudioListener(GameObject officialSetup, Camera xriCamera)
    {
        if (officialSetup == null || xriCamera == null)
            return;

        AudioListener activeListener = xriCamera.GetComponent<AudioListener>();
        if (activeListener == null)
            return;

        foreach (AudioListener listener in officialSetup.GetComponentsInChildren<AudioListener>(true))
        {
            if (listener != null)
                listener.enabled = listener == activeListener;
        }
    }

    private static Transform EnsureMirroredEffectsRoot(Transform parent)
    {
        if (parent == null)
            return null;

        Transform root = parent.Find("__HybridMirroredPlayerEffects");
        if (root != null)
            return root;

        GameObject rootGo = new GameObject("__HybridMirroredPlayerEffects");
        rootGo.transform.SetParent(parent, false);
        rootGo.transform.localPosition = Vector3.zero;
        rootGo.transform.localRotation = Quaternion.identity;
        rootGo.transform.localScale = Vector3.one;
        return rootGo.transform;
    }

    private static AudioSource CloneSerializedAudioSource(
        UnityEngine.Object sourceOwner,
        string propertyName,
        Transform parent,
        string cloneName)
    {
        AudioSource source = GetSerializedObjectReference<AudioSource>(sourceOwner, propertyName);
        return CloneAudioSource(source, parent, cloneName);
    }

    private static AudioSource CloneAudioSource(AudioSource source, Transform parent, string cloneName)
    {
        if (source == null || parent == null)
            return null;

        Transform child = parent.Find(cloneName);
        if (child == null)
        {
            GameObject childGo = new GameObject(cloneName);
            childGo.transform.SetParent(parent, false);
            child = childGo.transform;
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;

        AudioSource target = child.GetComponent<AudioSource>();
        if (target == null)
            target = child.gameObject.AddComponent<AudioSource>();

        CopyAudioSourceSettings(source, target);
        target.playOnAwake = false;
        target.Stop();
        return target;
    }

    private static void CopyAudioSourceSettings(AudioSource source, AudioSource target)
    {
        if (source == null || target == null)
            return;

        target.clip = source.clip;
        target.outputAudioMixerGroup = source.outputAudioMixerGroup;
        target.mute = source.mute;
        target.bypassEffects = source.bypassEffects;
        target.bypassListenerEffects = source.bypassListenerEffects;
        target.bypassReverbZones = source.bypassReverbZones;
        target.loop = source.loop;
        target.priority = source.priority;
        target.volume = source.volume;
        target.pitch = source.pitch;
        target.panStereo = source.panStereo;
        target.spatialBlend = source.spatialBlend;
        target.reverbZoneMix = source.reverbZoneMix;
        target.dopplerLevel = source.dopplerLevel;
        target.spread = source.spread;
        target.rolloffMode = source.rolloffMode;
        target.minDistance = source.minDistance;
        target.maxDistance = source.maxDistance;
    }

    private static T GetSerializedObjectReference<T>(UnityEngine.Object sourceOwner, string propertyName)
        where T : UnityEngine.Object
    {
        if (sourceOwner == null)
            return null;

        SerializedObject serializedObject = new SerializedObject(sourceOwner);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static void CopyComponentSerializedValues(Component source, Component target)
    {
        if (source == null || target == null)
            return;

        UnityEditorInternal.ComponentUtility.CopyComponent(source);
        UnityEditorInternal.ComponentUtility.PasteComponentValues(target);
    }

    private static void ConfigureDesktopPlayerIntegration(GameObject desktopPlayer)
    {
        if (desktopPlayer == null)
            return;

        desktopPlayer.tag = "Player";

        CharacterController characterController = EnsureComponent<CharacterController>(desktopPlayer);
        characterController.radius = Mathf.Max(characterController.radius, 0.32f);
        characterController.height = Mathf.Max(characterController.height, 1.7f);
        characterController.center = new Vector3(
            characterController.center.x,
            Mathf.Approximately(characterController.center.y, 0f) ? characterController.height * 0.5f : characterController.center.y,
            characterController.center.z);

        Camera playerCamera = desktopPlayer.GetComponentInChildren<Camera>(true);
        if (playerCamera == null)
        {
            GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.transform.SetParent(desktopPlayer.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            playerCamera = cameraGo.GetComponent<Camera>();
        }

        playerCamera.name = "Main Camera";
        playerCamera.tag = "MainCamera";
        playerCamera.stereoTargetEye = StereoTargetEyeMask.Both;
        playerCamera.nearClipPlane = 0.05f;

        InteractionPrompt prompt = FindOrCreatePrompt();
        PlayerController desktopController = EnsureComponent<PlayerController>(desktopPlayer);
        SerializedObject desktopSo = new SerializedObject(desktopController);
        SetObject(desktopSo, "cameraTransform", playerCamera.transform);
        SetBool(desktopSo, "disableDesktopInputWhenXRActive", true);
        desktopSo.ApplyModifiedProperties();

        PlayerInteractor desktopInteractor = EnsureComponent<PlayerInteractor>(desktopPlayer);
        SerializedObject interactorSo = new SerializedObject(desktopInteractor);
        SetObject(interactorSo, "cameraTransform", playerCamera.transform);
        SetObject(interactorSo, "prompt", prompt);
        SetLayerMask(interactorSo, "interactionMask", ~0);
        SetLayerMask(interactorSo, "blockingMask", ~0);
        interactorSo.ApplyModifiedProperties();

        EnsureComponent<PlayerInventory>(desktopPlayer);
    }

    private static XRRayInteractor FindHandRayInteractor(GameObject officialSetup, string handedness)
    {
        foreach (XRRayInteractor interactor in officialSetup.GetComponentsInChildren<XRRayInteractor>(true))
        {
            if (!string.Equals(interactor.gameObject.name, "Ray Interactor", StringComparison.OrdinalIgnoreCase))
                continue;

            Transform current = interactor.transform;
            while (current != null && current.IsChildOf(officialSetup.transform))
            {
                if (current.name.IndexOf(handedness, StringComparison.OrdinalIgnoreCase) >= 0)
                    return interactor;

                current = current.parent;
            }
        }

        return null;
    }

    private static void EnsureOfficialInteractablesForExistingGameplay()
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(behaviour is IInteractable))
                continue;

            EnsureComponent<XRSimpleInteractable>(behaviour.gameObject);
        }
    }

    private static GameObject FindDesktopPlayerInScene()
    {
        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObject.GetComponent<PlayerController>() != null)
                return rootObject;
        }

        return null;
    }

    private static GameObject FindSceneRootStartingWith(string name)
    {
        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObject.name.StartsWith(name, StringComparison.Ordinal))
                return rootObject;
        }

        return null;
    }

    public static void ValidateLevel0VRFromCommandLine()
    {
        ValidationResult result = ValidateLevel0VR();
        WriteResult(result);
        EditorApplication.Exit(result.Errors.Count > 0 ? 1 : 0);
    }

    public static void ValidateLevel0VRFromMenu()
    {
        ValidationResult result = ValidateLevel0VR();
        WriteResult(result);

        if (result.Errors.Count > 0)
            Debug.LogError($"Level0 VR validation failed. See {OutputPath}");
        else
            Debug.Log($"Level0 VR validation passed. See {OutputPath}");
    }

    private static ValidationResult ValidateLevel0VR()
    {
        ValidationResult result = new ValidationResult
        {
            CheckedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
            ScenePath = ScenePath,
        };

        if (!File.Exists(ScenePath))
        {
            result.Errors.Add($"Scene not found: {ScenePath}");
            return result;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
        if (player == null)
        {
            result.Errors.Add("Player GameObject was not found.");
            return result;
        }

        result.PlayerName = player.name;
        result.HasCharacterController = player.GetComponent<CharacterController>() != null;
        result.HasVRRigDriver = player.GetComponent<VRRigDriver>() != null;
        result.HasVRInteractor = player.GetComponent<VRInteractor>() != null;
        result.HasVRDemoSimulator = player.GetComponent<VRDemoSimulator>() != null;
        result.HasCamera = player.GetComponentInChildren<Camera>(true) != null;
        result.HasRightHandRay = player.transform.Find("XR Right Hand Ray") != null;
        result.HasLeftHand = player.transform.Find("XR Left Hand") != null;
        result.HasRayLine = player.GetComponentInChildren<LineRenderer>(true) != null;
        result.HasPrompt = UnityEngine.Object.FindFirstObjectByType<InteractionPrompt>() != null;
        result.HasOpenXRLoader = XRPackageMetadataStore.IsLoaderAssigned(OpenXRLoaderTypeName, BuildTargetGroup.Standalone);
        result.HasOpenXRInteractionProfile = HasEnabledOpenXRInteractionProfile();

        AddMissing(result, result.HasCharacterController, "Player has no CharacterController.");
        AddMissing(result, result.HasVRRigDriver, "Player has no VRRigDriver.");
        AddMissing(result, result.HasVRInteractor, "Player has no VRInteractor.");
        AddMissing(result, result.HasVRDemoSimulator, "Player has no VRDemoSimulator.");
        AddMissing(result, result.HasCamera, "Player has no child Camera.");
        AddMissing(result, result.HasRightHandRay, "Player has no 'XR Right Hand Ray' child.");
        AddMissing(result, result.HasRayLine, "No LineRenderer was found for the VR interaction ray.");
        AddMissing(result, result.HasOpenXRLoader, "OpenXR loader is not assigned for Standalone.");

        if (!result.HasPrompt)
            result.Warnings.Add("No InteractionPrompt was found; VR interaction still works but has no on-screen hint.");

        if (!result.HasOpenXRInteractionProfile)
            result.Warnings.Add("No OpenXR interaction profile is enabled; enable a profile matching the demo headset/controllers.");

        return result;
    }

    private static void ConfigureOpenXRForStandalone()
    {
        XRManagerSettings managerSettings = GetOrCreateXRManagerSettings(BuildTargetGroup.Standalone);
        bool assigned = XRPackageMetadataStore.AssignLoader(managerSettings, OpenXRLoaderTypeName, BuildTargetGroup.Standalone);
        if (!assigned)
        {
            Debug.LogWarning("OpenXR loader could not be assigned automatically. Enable it in Project Settings > XR Plug-in Management.");
            return;
        }

        OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        if (settings == null)
        {
            Debug.LogWarning("OpenXR settings were not available after assigning the loader.");
            return;
        }

        EnableFeature<OculusTouchControllerProfile>(settings);
        EnableFeature<MetaQuestTouchProControllerProfile>(settings);
        EnableFeature<ValveIndexControllerProfile>(settings);
        EnableFeature<HTCViveControllerProfile>(settings);
        EnableFeature<MicrosoftMotionControllerProfile>(settings);
        EnableFeature<HPReverbG2ControllerProfile>(settings);
        EnableFeature<KHRSimpleControllerProfile>(settings);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static XRManagerSettings GetOrCreateXRManagerSettings(BuildTargetGroup buildTargetGroup)
    {
        XRGeneralSettingsPerBuildTarget buildTargetSettings = LoadOrCreateXRGeneralSettings();
        if (!buildTargetSettings.HasManagerSettingsForBuildTarget(buildTargetGroup))
            buildTargetSettings.CreateDefaultManagerSettingsForBuildTarget(buildTargetGroup);

        return buildTargetSettings.ManagerSettingsForBuildTarget(buildTargetGroup);
    }

    private static XRGeneralSettingsPerBuildTarget LoadOrCreateXRGeneralSettings()
    {
        if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget existing) && existing != null)
            return existing;

        string[] guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
        if (guids.Length > 0)
        {
            string existingPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            existing = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(existingPath);
            if (existing != null)
            {
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, existing, true);
                return existing;
            }
        }

        if (!AssetDatabase.IsValidFolder(XRSettingsFolder))
            AssetDatabase.CreateFolder("Assets", "XR");

        XRGeneralSettingsPerBuildTarget created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
        AssetDatabase.CreateAsset(created, XRGeneralSettingsPath);
        AssetDatabase.SaveAssets();
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, created, true);
        return created;
    }

    private static void EnableFeature<TFeature>(OpenXRSettings settings) where TFeature : OpenXRFeature
    {
        TFeature feature = settings.GetFeature<TFeature>();
        if (feature != null)
            feature.enabled = true;
    }

    private static bool HasEnabledOpenXRInteractionProfile()
    {
        OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        if (settings == null)
            return false;

        foreach (OpenXRFeature feature in settings.GetFeatures<OpenXRInteractionFeature>())
        {
            if (feature != null && feature.enabled)
                return true;
        }

        return false;
    }

    private static InteractionPrompt FindPrompt()
    {
        InteractionPrompt prompt = UnityEngine.Object.FindFirstObjectByType<InteractionPrompt>();
        if (prompt != null)
            return prompt;

        GameObject promptGo = GameObject.Find("PromptText");
        return promptGo != null ? promptGo.GetComponent<InteractionPrompt>() : null;
    }

    private static InteractionPrompt FindOrCreatePrompt()
    {
        InteractionPrompt prompt = FindPrompt();
        if (prompt == null)
        {
            GameObject promptGo = CreatePromptUI();
            prompt = EnsureComponent<InteractionPrompt>(promptGo);
        }

        ConfigurePromptReferences(prompt);
        return prompt;
    }

    private static void ConfigurePromptReferences(InteractionPrompt prompt)
    {
        if (prompt == null)
            return;

        SerializedObject promptSo = new SerializedObject(prompt);
        Transform hintPanel = prompt.transform.Find("HintPanel");
        if (hintPanel != null)
            SetObject(promptSo, "hintPanel", hintPanel.gameObject);

        TextMeshProUGUI hintText = hintPanel != null
            ? hintPanel.Find("HintText")?.GetComponent<TextMeshProUGUI>()
            : null;
        if (hintText != null)
            SetObject(promptSo, "hintText", hintText);

        Image crosshair = prompt.transform.Find("Crosshair")?.GetComponent<Image>();
        if (crosshair != null)
            SetObject(promptSo, "Crosshair", crosshair);

        promptSo.ApplyModifiedProperties();
    }

    private static GameObject CreatePromptUI()
    {
        int uiLayer = GetUILayer();
        GameObject canvasGo = new GameObject("InteractionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = uiLayer;

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);

        GameObject promptGo = new GameObject("PromptText", typeof(RectTransform));
        promptGo.layer = uiLayer;
        promptGo.transform.SetParent(canvasGo.transform, false);

        RectTransform promptRect = promptGo.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(0f, -120f);
        promptRect.sizeDelta = Vector2.zero;

        GameObject crosshairGo = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
        crosshairGo.layer = uiLayer;
        crosshairGo.transform.SetParent(promptGo.transform, false);
        RectTransform crosshairRect = crosshairGo.GetComponent<RectTransform>();
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.anchoredPosition = new Vector2(0f, 120f);
        crosshairRect.sizeDelta = new Vector2(6f, 6f);
        crosshairGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.55f);

        GameObject hintPanelGo = new GameObject("HintPanel", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        hintPanelGo.layer = uiLayer;
        hintPanelGo.transform.SetParent(promptGo.transform, false);

        RectTransform hintPanelRect = hintPanelGo.GetComponent<RectTransform>();
        hintPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintPanelRect.anchoredPosition = Vector2.zero;

        hintPanelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        HorizontalLayoutGroup layout = hintPanelGo.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.childAlignment = TextAnchor.MiddleCenter;

        ContentSizeFitter fitter = hintPanelGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject hintTextGo = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintTextGo.layer = uiLayer;
        hintTextGo.transform.SetParent(hintPanelGo.transform, false);
        TextMeshProUGUI text = hintTextGo.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24;
        text.color = Color.white;

        return promptGo;
    }

    private static int GetUILayer()
    {
        int layer = LayerMask.NameToLayer("UI");
        return layer >= 0 ? layer : 0;
    }

    private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        GameObject childGo = new GameObject(name);
        childGo.transform.SetParent(parent, false);
        childGo.transform.localPosition = localPosition;
        childGo.transform.localRotation = Quaternion.identity;
        return childGo.transform;
    }

    private static Material GetOrCreateRayMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RayMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        material = new Material(shader)
        {
            name = "M_VR_Ray",
            color = new Color(0.3f, 1f, 0.6f, 0.8f),
        };

        AssetDatabase.CreateAsset(material, RayMaterialPath);
        return material;
    }

    private static Material GetOrCreateControllerDisplayMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ControllerMaterialPath);
        Shader shader = GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit")
            : Shader.Find("Standard") ?? Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogWarning("Unable to find a compatible controller display shader. Keeping the XRI controller's original materials.");
            return null;
        }

        bool createdMaterial = material == null;
        if (createdMaterial)
        {
            material = new Material(shader)
            {
                name = "M_XRI_Controller_Demo",
            };
        }
        else
        {
            material.shader = shader;
        }

        Color baseColor = new Color(0.72f, 0.78f, 0.84f, 1f);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.1f);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.38f);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.38f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", baseColor * 0.35f);
        }

        if (createdMaterial)
            AssetDatabase.CreateAsset(material, ControllerMaterialPath);
        else
            EditorUtility.SetDirty(material);

        return material;
    }

    private static void WriteResult(ValidationResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, result.ToJson());
    }

    private static void AddMissing(ValidationResult result, bool condition, string message)
    {
        if (!condition)
            result.Errors.Add(message);
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static XRIHybridDemoDriver GetOrCreateDisabledHybridDriver(GameObject officialSetup)
    {
        XRIHybridDemoDriver driver = officialSetup.GetComponent<XRIHybridDemoDriver>();
        if (driver != null)
        {
            driver.enabled = false;
            return driver;
        }

        bool wasActive = officialSetup.activeSelf;
        if (wasActive)
            officialSetup.SetActive(false);

        driver = officialSetup.AddComponent<XRIHybridDemoDriver>();
        driver.enabled = false;

        if (wasActive)
            officialSetup.SetActive(true);

        return driver;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetLayerMask(SerializedObject serializedObject, string propertyName, int bits)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        SerializedProperty maskBits = property?.FindPropertyRelative("m_Bits");
        if (maskBits != null)
            maskBits.intValue = bits;
    }

    private sealed class ValidationResult
    {
        public string ScenePath;
        public string CheckedAt;
        public string PlayerName;
        public bool HasCharacterController;
        public bool HasVRRigDriver;
        public bool HasVRInteractor;
        public bool HasVRDemoSimulator;
        public bool HasCamera;
        public bool HasLeftHand;
        public bool HasRightHandRay;
        public bool HasRayLine;
        public bool HasPrompt;
        public bool HasOpenXRLoader;
        public bool HasOpenXRInteractionProfile;
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public string ToJson()
        {
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            Add(json, "scenePath", ScenePath, true);
            Add(json, "checkedAt", CheckedAt, true);
            Add(json, "playerName", PlayerName, true);
            Add(json, "hasCharacterController", HasCharacterController, true);
            Add(json, "hasVRRigDriver", HasVRRigDriver, true);
            Add(json, "hasVRInteractor", HasVRInteractor, true);
            Add(json, "hasVRDemoSimulator", HasVRDemoSimulator, true);
            Add(json, "hasCamera", HasCamera, true);
            Add(json, "hasLeftHand", HasLeftHand, true);
            Add(json, "hasRightHandRay", HasRightHandRay, true);
            Add(json, "hasRayLine", HasRayLine, true);
            Add(json, "hasPrompt", HasPrompt, true);
            Add(json, "hasOpenXRLoader", HasOpenXRLoader, true);
            Add(json, "hasOpenXRInteractionProfile", HasOpenXRInteractionProfile, true);
            Add(json, "warnings", Warnings, true);
            Add(json, "errors", Errors, false);
            json.AppendLine("}");
            return json.ToString();
        }

        private static void Add(StringBuilder json, string key, string value, bool comma)
        {
            json.Append("  \"").Append(key).Append("\": ");
            json.Append(value == null ? "null" : $"\"{Escape(value)}\"");
            json.AppendLine(comma ? "," : string.Empty);
        }

        private static void Add(StringBuilder json, string key, bool value, bool comma)
        {
            json.Append("  \"").Append(key).Append("\": ").Append(value ? "true" : "false");
            json.AppendLine(comma ? "," : string.Empty);
        }

        private static void Add(StringBuilder json, string key, List<string> values, bool comma)
        {
            json.Append("  \"").Append(key).Append("\": [");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    json.Append(", ");
                json.Append("\"").Append(Escape(values[i])).Append("\"");
            }
            json.Append("]");
            json.AppendLine(comma ? "," : string.Empty);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
