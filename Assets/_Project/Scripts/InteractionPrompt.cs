using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InteractionPrompt : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Image Crosshair;
    [SerializeField] private bool showCrosshair;

    [Header("Crosshair Colors")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color activeColor = new Color(0.3f, 1f, 0.6f, 0.9f);

    private void Awake()
    {
        CacheCrosshairIfNeeded();
    }

    private void Start()
    {
        Hide();
    }

    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Hide();
            return;
        }

        if (hintPanel != null)
            hintPanel.SetActive(true);

        if (hintText != null)
        {
            hintText.text = message;
        }

        if (Crosshair != null)
        {
            Crosshair.gameObject.SetActive(showCrosshair);
            Crosshair.color = activeColor;
        }
    }

    public void Hide()
    {
        if (hintPanel != null)
            hintPanel.SetActive(false);

        if (Crosshair != null)
        {
            Crosshair.gameObject.SetActive(showCrosshair);
            Crosshair.color = normalColor;
        }
    }

    private void CacheCrosshairIfNeeded()
    {
        if (Crosshair != null)
            return;

        Transform crosshairTransform = transform.Find("Crosshair");
        if (crosshairTransform != null)
            Crosshair = crosshairTransform.GetComponent<Image>();
    }
}
