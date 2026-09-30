using UnityEngine;
using TacticalShooter.Systems.Interaction;

namespace TacticalShooter.Systems.Mission
{
    public class ExtractionInteractable : MonoBehaviour, IInteractable
    {
        [Header("Extraction Settings")]
        [SerializeField] private string extractPrompt = "Press E to extract";
        [SerializeField] private Color lockedColor = new Color(0.9f, 0.2f, 0.1f); // Red/Orange
        [SerializeField] private Color availableColor = new Color(0.1f, 0.9f, 0.2f); // Green
        [SerializeField] private Color completedColor = new Color(0.2f, 0.6f, 1.0f); // Cyan/Blue

        [Header("Optional Renderer Override")]
        [SerializeField] private Renderer extractionRenderer;

        private bool isCompleted = false;

        public string PromptText
        {
            get
            {
                if (isCompleted || MissionManager.Instance == null)
                    return "";

                if (MissionManager.Instance.CurrentState == MissionState.ExtractionAvailable)
                {
                    return extractPrompt;
                }

                return ""; // Hidden/Locked when ObjectivePending
            }
        }

        private void Awake()
        {
            if (extractionRenderer == null)
            {
                extractionRenderer = GetComponent<Renderer>();
                if (extractionRenderer == null)
                {
                    extractionRenderer = GetComponentInChildren<Renderer>();
                }
            }

            UpdateVisualState();
        }

        private void Update()
        {
            UpdateVisualState();
        }

        public void Interact(GameObject interactor)
        {
            if (isCompleted || MissionManager.Instance == null) return;

            if (MissionManager.Instance.CurrentState != MissionState.ExtractionAvailable)
            {
                return;
            }

            isCompleted = true;
            MissionManager.Instance.CompleteExtraction();
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (extractionRenderer == null) return;

            Color targetColor = lockedColor;

            if (isCompleted || (MissionManager.Instance != null && MissionManager.Instance.CurrentState == MissionState.MissionCompleted))
            {
                targetColor = completedColor;
            }
            else if (MissionManager.Instance != null && MissionManager.Instance.CurrentState == MissionState.ExtractionAvailable)
            {
                targetColor = availableColor;
            }

            // Instance-safe color update without modifying disk material assets
            if (extractionRenderer.material.color != targetColor)
            {
                extractionRenderer.material.color = targetColor;
            }
        }
    }
}
