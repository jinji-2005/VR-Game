using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerRuntimeValidation
{
    private const string ScenePath = "Assets/_Project/Scenes/Level0.unity";
    private const string OutputPath = "Logs/PlayerRuntimeValidation.json";
    private const string InteractableLayerName = "Interactable";

    public static void RunFromCommandLine()
    {
        ValidationResult result = ValidateLevel0();
        WriteResult(result);

        if (result.Errors.Count > 0)
            EditorApplication.Exit(1);
        else
            EditorApplication.Exit(0);
    }

    [MenuItem("VR Game/Validation/Validate Player Runtime Setup")]
    public static void RunFromMenu()
    {
        ValidationResult result = ValidateLevel0();
        WriteResult(result);

        if (result.Errors.Count > 0)
            Debug.LogError($"Player runtime validation failed. See {OutputPath}");
        else
            Debug.Log($"Player runtime validation passed. See {OutputPath}");
    }

    private static ValidationResult ValidateLevel0()
    {
        var result = new ValidationResult
        {
            ScenePath = ScenePath,
            CheckedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
        };

        if (!File.Exists(ScenePath))
        {
            result.Errors.Add($"Scene not found: {ScenePath}");
            return result;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            player = GameObject.Find("Player");

        if (player == null)
        {
            result.Errors.Add("Player GameObject was not found by tag or name.");
            return result;
        }

        result.PlayerName = player.name;
        result.PlayerPosition = player.transform.position;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            result.Errors.Add("Player has no CharacterController.");
        }
        else
        {
            result.ControllerHeight = controller.height;
            result.ControllerRadius = controller.radius;
            result.ControllerCenter = controller.center;
            result.ControllerSkinWidth = controller.skinWidth;
            result.ControllerStepOffset = controller.stepOffset;

            if (controller.radius < 0.3f)
                result.Warnings.Add($"CharacterController radius is {controller.radius:0.###}; 0.32 or higher is recommended to reduce camera wall clipping without blocking narrow doors.");
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null)
        {
            result.Errors.Add("Player has no PlayerController.");
        }
        else
        {
            var serializedController = new SerializedObject(playerController);
            result.ConfiguredControllerRadius = GetFloat(serializedController, "controllerRadius");
            result.ConfiguredCameraNearClipPlane = GetFloat(serializedController, "cameraNearClipPlane");
            result.LimitCameraPlanarOffset = GetBool(serializedController, "limitCameraPlanarOffset");
            result.CameraPlanarPadding = GetFloat(serializedController, "cameraPlanarPadding");

            if (result.ConfiguredControllerRadius < 0.3f)
                result.Warnings.Add($"PlayerController controllerRadius is {result.ConfiguredControllerRadius:0.###}; 0.32 or higher is recommended.");

            if (result.ConfiguredCameraNearClipPlane > 0.06f)
                result.Warnings.Add($"PlayerController cameraNearClipPlane is {result.ConfiguredCameraNearClipPlane:0.###}; 0.05 or smaller is recommended.");

            if (!result.LimitCameraPlanarOffset)
                result.Warnings.Add("PlayerController limitCameraPlanarOffset is disabled.");
        }

        Camera playerCamera = player.GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            result.Errors.Add("No Camera was found under Player.");
        }
        else
        {
            result.CameraName = playerCamera.name;
            result.CameraLocalPosition = playerCamera.transform.localPosition;
            result.CameraNearClipPlane = playerCamera.nearClipPlane;

            Vector2 cameraPlanarOffset = new Vector2(
                playerCamera.transform.localPosition.x,
                playerCamera.transform.localPosition.z);
            result.CameraPlanarOffset = cameraPlanarOffset.magnitude;

            if (playerCamera.nearClipPlane > 0.06f)
                result.Warnings.Add($"Camera near clip plane is {playerCamera.nearClipPlane:0.###}; 0.05 or smaller is recommended.");

            if (controller != null)
            {
                float maxPlanarOffset = Mathf.Max(0f, controller.radius - Mathf.Max(0f, result.CameraPlanarPadding));
                result.MaxRecommendedCameraPlanarOffset = maxPlanarOffset;

                if (cameraPlanarOffset.magnitude > maxPlanarOffset + 0.001f)
                    result.Warnings.Add($"Camera planar offset is {cameraPlanarOffset.magnitude:0.###}, greater than recommended max {maxPlanarOffset:0.###}.");
            }
        }

        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
        if (interactor == null)
        {
            result.Errors.Add("Player has no PlayerInteractor.");
        }
        else
        {
            var serializedInteractor = new SerializedObject(interactor);
            result.InteractDistance = GetFloat(serializedInteractor, "interactDistance");
            result.InteractRadius = GetFloat(serializedInteractor, "interactRadius");
            result.HasInteractorCameraReference = GetObject(serializedInteractor, "cameraTransform") != null;
            result.HasPromptReference = GetObject(serializedInteractor, "prompt") != null;
            result.InteractionMaskBits = GetLayerMaskBits(serializedInteractor, "interactionMask");
            result.BlockingMaskBits = GetLayerMaskBits(serializedInteractor, "blockingMask");

            if (!result.HasInteractorCameraReference)
                result.Errors.Add("PlayerInteractor cameraTransform is not assigned.");

            if (!result.HasPromptReference)
                result.Warnings.Add("PlayerInteractor prompt is not assigned; interaction UI will not show.");
        }

        result.SceneColliderCount = UnityEngine.Object.FindObjectsOfType<Collider>(true).Length;
        result.NonTriggerColliderCount = CountNonTriggerColliders();
        if (result.NonTriggerColliderCount == 0)
            result.Errors.Add("Scene has no non-trigger colliders for the CharacterController to collide with.");

        result.InteractableLayer = LayerMask.NameToLayer(InteractableLayerName);
        if (result.InteractableLayer < 0)
        {
            result.Errors.Add($"Layer '{InteractableLayerName}' is not defined.");
        }
        else
        {
            int expectedInteractionMask = 1 << result.InteractableLayer;
            if (result.InteractionMaskBits != 0 && result.InteractionMaskBits != expectedInteractionMask && result.InteractionMaskBits != -1)
                result.Warnings.Add($"PlayerInteractor interactionMask is {result.InteractionMaskBits}, expected all layers, runtime fallback 0, or {expectedInteractionMask} for layer '{InteractableLayerName}'.");
        }

        CountInteractables(result);

        return result;
    }

    private static int CountNonTriggerColliders()
    {
        int count = 0;
        foreach (Collider collider in UnityEngine.Object.FindObjectsOfType<Collider>(true))
        {
            if (!collider.isTrigger)
                count++;
        }

        return count;
    }

    private static void CountInteractables(ValidationResult result)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (!(behaviour is IInteractable))
                continue;

            result.InteractableCount++;

            if (result.InteractableLayer >= 0 && behaviour.gameObject.layer != result.InteractableLayer)
            {
                result.InteractableLayerMismatchCount++;
                result.Warnings.Add($"Interactable '{behaviour.name}' is on layer '{LayerMask.LayerToName(behaviour.gameObject.layer)}', expected '{InteractableLayerName}'.");
            }
        }

        if (result.InteractableCount == 0)
            result.Warnings.Add("No IInteractable components were found in the scene.");
    }

    private static void WriteResult(ValidationResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, result.ToJson());
    }

    private static float GetFloat(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.floatValue : 0f;
    }

    private static bool GetBool(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.boolValue;
    }

    private static UnityEngine.Object GetObject(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static int GetLayerMaskBits(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        SerializedProperty bits = property?.FindPropertyRelative("m_Bits");
        return bits != null ? bits.intValue : 0;
    }

    private sealed class ValidationResult
    {
        public string ScenePath;
        public string CheckedAt;
        public string PlayerName;
        public Vector3 PlayerPosition;
        public float ControllerHeight;
        public float ControllerRadius;
        public Vector3 ControllerCenter;
        public float ControllerSkinWidth;
        public float ControllerStepOffset;
        public float ConfiguredControllerRadius;
        public float ConfiguredCameraNearClipPlane;
        public bool LimitCameraPlanarOffset;
        public float CameraPlanarPadding;
        public string CameraName;
        public Vector3 CameraLocalPosition;
        public float CameraNearClipPlane;
        public float CameraPlanarOffset;
        public float MaxRecommendedCameraPlanarOffset;
        public float InteractDistance;
        public float InteractRadius;
        public bool HasInteractorCameraReference;
        public bool HasPromptReference;
        public int InteractionMaskBits;
        public int BlockingMaskBits;
        public int InteractableLayer;
        public int InteractableCount;
        public int InteractableLayerMismatchCount;
        public int SceneColliderCount;
        public int NonTriggerColliderCount;
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public string ToJson()
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            Add(json, "scenePath", ScenePath, true);
            Add(json, "checkedAt", CheckedAt, true);
            Add(json, "playerName", PlayerName, true);
            Add(json, "playerPosition", PlayerPosition, true);
            Add(json, "controllerHeight", ControllerHeight, true);
            Add(json, "controllerRadius", ControllerRadius, true);
            Add(json, "controllerCenter", ControllerCenter, true);
            Add(json, "controllerSkinWidth", ControllerSkinWidth, true);
            Add(json, "controllerStepOffset", ControllerStepOffset, true);
            Add(json, "configuredControllerRadius", ConfiguredControllerRadius, true);
            Add(json, "configuredCameraNearClipPlane", ConfiguredCameraNearClipPlane, true);
            Add(json, "limitCameraPlanarOffset", LimitCameraPlanarOffset, true);
            Add(json, "cameraPlanarPadding", CameraPlanarPadding, true);
            Add(json, "cameraName", CameraName, true);
            Add(json, "cameraLocalPosition", CameraLocalPosition, true);
            Add(json, "cameraNearClipPlane", CameraNearClipPlane, true);
            Add(json, "cameraPlanarOffset", CameraPlanarOffset, true);
            Add(json, "maxRecommendedCameraPlanarOffset", MaxRecommendedCameraPlanarOffset, true);
            Add(json, "interactDistance", InteractDistance, true);
            Add(json, "interactRadius", InteractRadius, true);
            Add(json, "hasInteractorCameraReference", HasInteractorCameraReference, true);
            Add(json, "hasPromptReference", HasPromptReference, true);
            Add(json, "interactionMaskBits", InteractionMaskBits, true);
            Add(json, "blockingMaskBits", BlockingMaskBits, true);
            Add(json, "interactableLayer", InteractableLayer, true);
            Add(json, "interactableCount", InteractableCount, true);
            Add(json, "interactableLayerMismatchCount", InteractableLayerMismatchCount, true);
            Add(json, "sceneColliderCount", SceneColliderCount, true);
            Add(json, "nonTriggerColliderCount", NonTriggerColliderCount, true);
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

        private static void Add(StringBuilder json, string key, int value, bool comma)
        {
            json.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            json.AppendLine(comma ? "," : string.Empty);
        }

        private static void Add(StringBuilder json, string key, float value, bool comma)
        {
            json.Append("  \"").Append(key).Append("\": ").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            json.AppendLine(comma ? "," : string.Empty);
        }

        private static void Add(StringBuilder json, string key, Vector3 value, bool comma)
        {
            json.Append("  \"").Append(key).Append("\": { ");
            json.Append("\"x\": ").Append(value.x.ToString("0.###", CultureInfo.InvariantCulture)).Append(", ");
            json.Append("\"y\": ").Append(value.y.ToString("0.###", CultureInfo.InvariantCulture)).Append(", ");
            json.Append("\"z\": ").Append(value.z.ToString("0.###", CultureInfo.InvariantCulture)).Append(" }");
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
