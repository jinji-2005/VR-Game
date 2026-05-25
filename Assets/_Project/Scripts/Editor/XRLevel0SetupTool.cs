using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

public static class XRLevel0SetupTool
{
    private const string ScenePath = "Assets/_Project/Scenes/Level0.unity";
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

    [MenuItem("VR Game/XR/Preview Official XRI Rig in Level0")]
    public static void PreviewOfficialXRIRigInLevel0()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Stop Play mode first, then enable the official XRI preview rig.");
            return;
        }

        ConfigureOpenXRForStandalone();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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
            officialSetup.transform.SetPositionAndRotation(desktopPlayer.transform.position, desktopPlayer.transform.rotation);
            ConfigureOfficialXRIIntegration(officialSetup, desktopPlayer);
            EnsureOfficialInteractablesForExistingGameplay();
            desktopPlayer.SetActive(false);
        }

        officialSetup.SetActive(true);
        simulator.SetActive(true);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Official XRI preview enabled in Level0. This uses the unmodified XR Interaction Setup and XR Device Simulator prefabs. Run VR Game/XR/Restore Existing Player Mode to return to the keyboard player.");
    }

    [MenuItem("VR Game/XR/Restore Existing Player Mode")]
    public static void RestoreExistingPlayerMode()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Stop Play mode first, then restore the existing player mode.");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject desktopPlayer = FindDesktopPlayerInScene();
        GameObject officialSetup = FindSceneRootStartingWith("XR Interaction Setup");
        GameObject simulator = FindSceneRootStartingWith("XR Device Simulator");

        if (desktopPlayer != null)
            desktopPlayer.SetActive(true);

        if (officialSetup != null)
            officialSetup.SetActive(false);

        if (simulator != null)
            simulator.SetActive(false);

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Existing keyboard and VR demo player restored in Level0. The official XRI preview objects remain available but disabled.");
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
        PlayerController desktopController = desktopPlayer.GetComponent<PlayerController>();
        CharacterController desktopCharacter = desktopPlayer.GetComponent<CharacterController>();
        InputActionAsset actions = FindImportedXRIInputActions();
        InteractionPrompt prompt = FindPrompt();

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
        }

        if (desktopController != null && driver != null)
            driver.minHeight = desktopController.CrouchHeight;

        if (xriCamera != null)
            xriCamera.nearClipPlane = 0.05f;

        XRIOfficialPlayerTuning tuning = EnsureComponent<XRIOfficialPlayerTuning>(officialSetup);
        SerializedObject tuningSo = new SerializedObject(tuning);
        SetObject(tuningSo, "moveProvider", moveProvider);
        SetObject(tuningSo, "bodyController", xriCharacter);
        SetObject(tuningSo, "xriInputActions", actions);
        if (desktopController != null)
        {
            SetFloat(tuningSo, "walkSpeed", desktopController.WalkSpeed);
            SetFloat(tuningSo, "sprintMultiplier", desktopController.SprintMultiplier);
            SetFloat(tuningSo, "crouchHeight", desktopController.CrouchHeight);
            SetFloat(tuningSo, "crouchSpeed", desktopController.CrouchSpeed);
        }
        tuningSo.ApplyModifiedProperties();

        if (rightHandRay == null || rightHandController == null || leftHandRay == null || leftHandController == null)
            Debug.LogWarning("One or more official XRI hand rays/controllers were not found. Key and door Trigger bridging may be incomplete.");
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

    [MenuItem("VR Game/XR/Validate Level0 VR Rig")]
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
