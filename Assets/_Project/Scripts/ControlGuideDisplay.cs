using UnityEngine;
using TMPro;

public class ControlGuideDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private TextMeshProUGUI guideText;

    [Header("Settings")]
    [SerializeField] private float displayDuration = 15f;
    [SerializeField] private float fadeDuration = 1.5f;

    private CanvasGroup canvasGroup;
    private float elapsed;
    private bool done;

    private void Awake()
    {
        if (guidePanel != null)
            canvasGroup = guidePanel.GetComponent<CanvasGroup>();
        Show();
    }

    private void Show()
    {
        if (guidePanel != null)
            guidePanel.SetActive(true);
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void Update()
    {
        if (done) return;

        elapsed += Time.deltaTime;

        if (elapsed > displayDuration)
        {
            Hide();
            return;
        }

        if (elapsed > displayDuration - fadeDuration && canvasGroup != null)
        {
            float t = (elapsed - (displayDuration - fadeDuration)) / fadeDuration;
            canvasGroup.alpha = 1f - t;
        }
    }

    private void Hide()
    {
        done = true;
        if (guidePanel != null)
            Destroy(guidePanel.transform.parent.gameObject);
        Destroy(this);
    }
}
