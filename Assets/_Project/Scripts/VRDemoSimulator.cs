using UnityEngine;

public class VRDemoSimulator : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool startInDemoMode;
    [SerializeField] private KeyCode enableDemoModeKey = KeyCode.F2;
    [SerializeField] private KeyCode enableDesktopModeKey = KeyCode.F1;

    [Header("References")]
    [SerializeField] private PlayerController desktopController;
    [SerializeField] private PlayerInteractor desktopInteractor;
    [SerializeField] private VRRigDriver vrRigDriver;
    [SerializeField] private VRInteractor vrInteractor;

    [Header("Demo Visuals")]
    [SerializeField] private bool showControllerVisuals = true;
    [SerializeField] private GameObject leftControllerVisualPrefab;
    [SerializeField] private GameObject rightControllerVisualPrefab;
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;
    [SerializeField] private Vector3 leftControllerRotation = Vector3.zero;
    [SerializeField] private Vector3 rightControllerRotation = Vector3.zero;
    [SerializeField] private float controllerVisualScale = 1f;
    [SerializeField] private Material controllerDisplayMaterial;

    public static bool IsDemoModeActive { get; private set; }

    private GameObject leftControllerVisual;
    private GameObject rightControllerVisual;

    private void Reset()
    {
        desktopController = GetComponent<PlayerController>();
        desktopInteractor = GetComponent<PlayerInteractor>();
        vrRigDriver = GetComponent<VRRigDriver>();
        vrInteractor = GetComponent<VRInteractor>();
    }

    private void Awake()
    {
        CacheReferences();
        CreateControllerVisuals();
        SetDemoMode(startInDemoMode);
    }

    private void OnDisable()
    {
        if (IsDemoModeActive)
            SetDemoMode(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(enableDemoModeKey))
            SetDemoMode(true);

        if (Input.GetKeyDown(enableDesktopModeKey))
            SetDemoMode(false);
    }

    private void CacheReferences()
    {
        if (desktopController == null)
            desktopController = GetComponent<PlayerController>();

        if (desktopInteractor == null)
            desktopInteractor = GetComponent<PlayerInteractor>();

        if (vrRigDriver == null)
            vrRigDriver = GetComponent<VRRigDriver>();

        if (vrInteractor == null)
            vrInteractor = GetComponent<VRInteractor>();

        if (leftHandTransform == null)
            leftHandTransform = transform.Find("XR Left Hand");

        if (rightHandTransform == null)
            rightHandTransform = transform.Find("XR Right Hand Ray");
    }

    private void SetDemoMode(bool enabled)
    {
        IsDemoModeActive = enabled;

        if (desktopController != null)
            desktopController.enabled = true;

        if (desktopInteractor != null)
            desktopInteractor.enabled = true;

        if (vrRigDriver != null)
            vrRigDriver.enabled = true;

        if (vrInteractor != null)
            vrInteractor.enabled = true;

        if (leftControllerVisual != null)
            leftControllerVisual.SetActive(enabled && showControllerVisuals);

        if (rightControllerVisual != null)
            rightControllerVisual.SetActive(enabled && showControllerVisuals);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void CreateControllerVisuals()
    {
        if (!showControllerVisuals)
            return;

        if (leftControllerVisualPrefab == null || rightControllerVisualPrefab == null)
        {
            Debug.LogWarning("VR Demo XRI controller models are not assigned. Exit Play mode and run VR Game/XR/Configure Level0 VR Rig to import XRI Starter Assets.");
            return;
        }

        leftControllerVisual = CreateControllerVisual(leftControllerVisualPrefab, leftHandTransform, "Demo Left Controller", leftControllerRotation);
        rightControllerVisual = CreateControllerVisual(rightControllerVisualPrefab, rightHandTransform, "Demo Right Controller", rightControllerRotation);
    }

    private GameObject CreateControllerVisual(GameObject prefab, Transform parent, string objectName, Vector3 localEulerAngles)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.gameObject;

        GameObject controller = Instantiate(prefab);
        controller.name = objectName;
        controller.transform.SetParent(parent, false);
        controller.transform.localRotation *= Quaternion.Euler(localEulerAngles);
        controller.transform.localScale *= controllerVisualScale;
        ApplyControllerDisplayMaterial(controller);
        return controller;
    }

    private void ApplyControllerDisplayMaterial(GameObject controller)
    {
        if (controllerDisplayMaterial == null)
            return;

        foreach (Renderer controllerRenderer in controller.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = controllerRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = controllerDisplayMaterial;

            controllerRenderer.sharedMaterials = materials;
        }
    }
}
