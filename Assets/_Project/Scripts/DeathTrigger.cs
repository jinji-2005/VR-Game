using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathTrigger : MonoBehaviour
{
    [SerializeField] private float killDistance = 1.2f;
    [SerializeField] private float chaseCatchPadding = 0.25f;
    [SerializeField] private float verticalKillDistance = 1.6f;
    [SerializeField] private Vector3 attackPointOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private GameObject player;

    [Header("Death Effects")]
    [SerializeField] private GameObject deathOverlay;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float restartDelay = 3f;

    [Header("Death Camera")]
    [SerializeField] private float cameraLookSpeed = 2f;
    [SerializeField] private string[] headBoneNames = { "CableHead", "Head", "Neck2" };

    private bool hasTriggered;
    private float deathTimer;
    private PlayerController playerController;
    private XRIOfficialPlayerTuning xriPlayerTuning;
    private XRIHybridDemoDriver xriHybridDemoDriver;
    private CharacterController playerCharacterController;
    private BacteriaController bacteriaController;
    private Transform playerCamera;
    private Transform headBone;

    private void Awake()
    {
        bacteriaController = GetComponent<BacteriaController>();
    }

    private void Update()
    {
        if (player == null || !player.activeInHierarchy)
            ResolvePlayer();

        if (hasTriggered)
        {
            UpdateDeathAnimation();
            return;
        }

        if (player == null)
            return;

        Vector3 attackPoint = transform.TransformPoint(attackPointOffset);
        Vector3 playerPoint = GetPlayerTargetPoint();
        Vector3 offset = playerPoint - attackPoint;
        float verticalOffset = Mathf.Abs(offset.y);
        offset.y = 0f;
        float effectiveKillDistance = GetEffectiveKillDistance();

        if (verticalOffset <= verticalKillDistance &&
            offset.sqrMagnitude < effectiveKillDistance * effectiveKillDistance)
        {
            TriggerDeath();
        }
    }

    private void TriggerDeath()
    {
        hasTriggered = true;
        deathTimer = restartDelay;

        if (playerController == null && player != null)
            playerController = player.GetComponent<PlayerController>();

        // Stop footstep noise immediately
        if (playerController != null)
        {
            playerController.StopAllNoise();
            playerController.enabled = false;
        }

        // Stop the bacteria
        if (bacteriaController != null)
        {
            bacteriaController.StopChaseSound();
            bacteriaController.enabled = false;
        }

        // Face the player
        FacePlayer();

        // Find head bone and camera for death animation
        FindHeadBone();
        FindPlayerCamera();

        if (xriPlayerTuning != null)
            xriPlayerTuning.DisableLocomotion();

        if (xriHybridDemoDriver != null)
            xriHybridDemoDriver.FreezeForDeath();

        XRIHybridDemoDriver.FreezeActiveForDeath();

        if (deathOverlay != null)
            deathOverlay.SetActive(true);

        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, player != null ? player.transform.position : transform.position);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void UpdateDeathAnimation()
    {
        deathTimer -= Time.deltaTime;

        // Smoothly rotate camera to look at bacteria head
        if (playerCamera != null && headBone != null)
        {
            Vector3 lookTarget = headBone.position + Vector3.up * 0.2f;
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - playerCamera.position);
            playerCamera.rotation = Quaternion.RotateTowards(playerCamera.rotation, targetRotation, cameraLookSpeed * 45f * Time.deltaTime);
        }

        if (deathTimer <= 0f)
            RestartScene();
    }

    private void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void FindHeadBone()
    {
        if (headBone != null)
            return;

        foreach (string name in headBoneNames)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    headBone = child;
                    return;
                }
            }
        }

        // Fallback: use attack point above bacteria
        headBone = transform;
    }

    private void FindPlayerCamera()
    {
        if (playerController != null)
        {
            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam != null)
                playerCamera = cam.transform;
        }

        if (playerCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                playerCamera = mainCam.transform;
        }
    }

    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 attackPoint = transform.TransformPoint(attackPointOffset);
        Gizmos.DrawWireSphere(attackPoint, killDistance);
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 1f);
        Gizmos.DrawLine(attackPoint + Vector3.up * verticalKillDistance, attackPoint - Vector3.up * verticalKillDistance);
    }

    private void ResolvePlayer()
    {
        XRIOfficialPlayerTuning activeXriPlayer = FindFirstObjectByType<XRIOfficialPlayerTuning>();
        if (activeXriPlayer != null && activeXriPlayer.isActiveAndEnabled &&
            activeXriPlayer.PlayerTarget != null && activeXriPlayer.PlayerTarget.activeInHierarchy)
        {
            player = activeXriPlayer.PlayerTarget;
            xriPlayerTuning = activeXriPlayer;
            xriHybridDemoDriver = activeXriPlayer.GetComponent<XRIHybridDemoDriver>() ??
                activeXriPlayer.GetComponentInParent<XRIHybridDemoDriver>() ??
                activeXriPlayer.GetComponentInChildren<XRIHybridDemoDriver>(true);
            playerCharacterController = activeXriPlayer.BodyController;
            return;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer == null)
            return;

        player = taggedPlayer;
        playerController = taggedPlayer.GetComponent<PlayerController>();
        xriPlayerTuning = taggedPlayer.GetComponentInParent<XRIOfficialPlayerTuning>();
        xriHybridDemoDriver = taggedPlayer.GetComponentInParent<XRIHybridDemoDriver>();
        playerCharacterController = taggedPlayer.GetComponent<CharacterController>();
    }

    private Vector3 GetPlayerTargetPoint()
    {
        if (playerCharacterController != null)
            return playerCharacterController.bounds.center;

        if (player != null)
            return player.transform.position + Vector3.up * 0.9f;

        return transform.position;
    }

    private float GetEffectiveKillDistance()
    {
        if (bacteriaController == null ||
            bacteriaController.currentState != BacteriaController.State.Chase ||
            !bacteriaController.HasVisualContactWithPlayer)
        {
            return killDistance;
        }

        return Mathf.Max(killDistance, bacteriaController.ChaseStopContactDistance + chaseCatchPadding);
    }
}
