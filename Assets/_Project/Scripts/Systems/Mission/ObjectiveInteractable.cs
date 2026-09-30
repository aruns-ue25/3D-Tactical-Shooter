using UnityEngine;
using TacticalShooter.Systems.Interaction;

namespace TacticalShooter.Systems.Mission
{
    public class ObjectiveInteractable : MonoBehaviour, IInteractable
    {
        [Header("Objective Terminal Config")]
        [SerializeField] private string pendingPrompt = "Press E to secure objective";
        [SerializeField] private Color pendingColor = new Color(0.9f, 0.7f, 0.1f); // Orange/Yellow
        [SerializeField] private Color completedColor = new Color(0.1f, 0.9f, 0.2f); // Green

        [Header("Optional Renderer Overrides")]
        [SerializeField] private Renderer terminalRenderer;

        private bool isCompleted = false;

        public string PromptText
        {
            get
            {
                if (isCompleted || (MissionManager.Instance != null && MissionManager.Instance.CurrentState != MissionState.ObjectivePending))
                {
                    return "";
                }
                return pendingPrompt;
            }
        }

        private void Awake()
        {
            if (terminalRenderer == null)
            {
                terminalRenderer = GetComponent<Renderer>();
                if (terminalRenderer == null)
                {
                    terminalRenderer = GetComponentInChildren<Renderer>();
                }
            }

            ApplyColor(pendingColor);
        }

        public void Interact(GameObject interactor)
        {
            if (isCompleted) return;

            if (MissionManager.Instance != null && MissionManager.Instance.CurrentState != MissionState.ObjectivePending)
            {
                return;
            }

            isCompleted = true;

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.CompleteObjective();
            }

            ApplyColor(completedColor);
        }

        private void ApplyColor(Color color)
        {
            if (terminalRenderer != null)
            {
                // Accessing .material creates a per-renderer instance safely without modifying disk assets
                terminalRenderer.material.color = color;
            }
        }
    }
}
