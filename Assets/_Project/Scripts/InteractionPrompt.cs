using UnityEngine;

public class InteractionPrompt : MonoBehaviour
{
    private string currentMessage;
    private bool hasMessage;
    private const float CrosshairSize = 8f;
    private const float CrosshairThickness = 2f;
    private Texture2D crosshairTex;
    private readonly Color normalColor = new(1f, 1f, 1f, 0.35f);
    private readonly Color activeColor = new(0.3f, 1f, 0.6f, 0.8f);

    private void Awake()
    {
        crosshairTex = new Texture2D(1, 1);
        crosshairTex.SetPixel(0, 0, Color.white);
        crosshairTex.Apply();
    }

    public void Show(string message)
    {
        currentMessage = message;
        hasMessage = true;
    }

    public void Hide()
    {
        hasMessage = false;
    }

    private void OnGUI()
    {
        DrawCrosshair();

        if (!hasMessage || string.IsNullOrEmpty(currentMessage))
            return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            alignment = TextAnchor.MiddleCenter
        };

        var rect = new Rect(Screen.width / 2f - 200, Screen.height * 0.6f, 400, 50);
        GUI.Label(rect, currentMessage, style);
    }

    private void DrawCrosshair()
    {
        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;

        GUI.color = hasMessage ? activeColor : normalColor;

        GUI.DrawTexture(new Rect(cx - CrosshairSize, cy - CrosshairThickness / 2f, CrosshairSize * 2f, CrosshairThickness), crosshairTex);
        GUI.DrawTexture(new Rect(cx - CrosshairThickness / 2f, cy - CrosshairSize, CrosshairThickness, CrosshairSize * 2f), crosshairTex);

        GUI.color = Color.white;
    }
}
