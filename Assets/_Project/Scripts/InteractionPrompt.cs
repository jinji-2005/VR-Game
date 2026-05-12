using UnityEngine;

public class InteractionPrompt : MonoBehaviour
{
    private string currentMessage;
    private bool hasMessage;

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
}
