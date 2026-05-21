using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathTrigger : MonoBehaviour
{
    [SerializeField] private float killDistance = 1.2f;
    [SerializeField] private float verticalKillDistance = 1.6f;
    [SerializeField] private Vector3 attackPointOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private GameObject player;

    [Header("Death Effects")]
    [SerializeField] private GameObject deathOverlay;
    [SerializeField] private AudioSource deathAudio;
    [SerializeField] private float restartDelay = 3f;

    private bool hasTriggered;
    private PlayerController playerController;
    private CharacterController playerCharacterController;

    private void Update()
    {
        if (player == null)
            ResolvePlayer();

        if (hasTriggered || player == null)
            return;

        Vector3 attackPoint = transform.TransformPoint(attackPointOffset);
        Vector3 playerPoint = GetPlayerTargetPoint();
        Vector3 offset = playerPoint - attackPoint;
        float verticalOffset = Mathf.Abs(offset.y);
        offset.y = 0f;

        if (verticalOffset <= verticalKillDistance &&
            offset.sqrMagnitude < killDistance * killDistance)
        {
            TriggerDeath();
        }
    }

    private void TriggerDeath()
    {
        hasTriggered = true;

        if (playerController == null && player != null)
            playerController = player.GetComponent<PlayerController>();

        if (playerController != null)
            playerController.enabled = false;

        if (deathOverlay != null)
            deathOverlay.SetActive(true);

        if (deathAudio != null)
            deathAudio.Play();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Invoke(nameof(RestartScene), restartDelay);
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
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer == null)
            return;

        player = taggedPlayer;
        playerController = taggedPlayer.GetComponent<PlayerController>();
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
}
