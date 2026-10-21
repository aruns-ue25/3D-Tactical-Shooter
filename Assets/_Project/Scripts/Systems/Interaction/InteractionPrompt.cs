using UnityEngine;

namespace TacticalShooter.Systems.Interaction
{
    public class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] private string currentPrompt = "";
        [SerializeField] private bool isVisible = false;

        public void Show(string promptText)
        {
            currentPrompt = promptText;
            isVisible = true;
        }

        public void Hide()
        {
            currentPrompt = "";
            isVisible = false;
        }

        private void OnGUI()
        {
            if (!isVisible || string.IsNullOrEmpty(currentPrompt)) return;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 18;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;

            float width = 280f;
            float height = 40f;
            float left = (Screen.width - width) * 0.5f;
            float top = (Screen.height - height) * 0.5f + 100f; // Positioned slightly below screen center crosshair

            GUI.Box(new Rect(left, top, width, height), currentPrompt, style);
        }
    }
}
