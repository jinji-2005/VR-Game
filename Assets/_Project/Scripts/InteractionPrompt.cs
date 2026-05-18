using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InteractionPrompt : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Image Crosshair;

    [Header("Crosshair Colors")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color activeColor = new Color(0.3f, 1f, 0.6f, 0.9f);

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
            hintText.text =
                $"<b><color=#FFD700>[E]</color></b> {message}";
        }

        if (Crosshair != null)
        {
            Crosshair.color = activeColor;
        }
    }

    public void Hide()
    {
        if (hintPanel != null)
            hintPanel.SetActive(false);

        if (Crosshair != null)
        {
            Crosshair.color = normalColor;
        }
    }
}