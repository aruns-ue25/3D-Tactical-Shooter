using UnityEngine;

namespace TacticalShooter.Systems.Mission
{
    public enum MissionState
    {
        ObjectivePending,
        ObjectiveCompleted,
        ExtractionAvailable,
        MissionCompleted
    }

    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        public MissionState CurrentState { get; private set; } = MissionState.ObjectivePending;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            CurrentState = MissionState.ObjectivePending;
        }

        public void CompleteObjective()
        {
            if (CurrentState != MissionState.ObjectivePending) return;

            // Transitions directly from ObjectivePending -> ExtractionAvailable
            CurrentState = MissionState.ExtractionAvailable;
            Debug.Log("[MissionManager] Objective Completed! Extraction is now AVAILABLE.");
        }

        public void CompleteExtraction()
        {
            if (CurrentState != MissionState.ExtractionAvailable) return;

            CurrentState = MissionState.MissionCompleted;
            Debug.Log("[MissionManager] Extraction Completed! MISSION COMPLETE.");
        }

        private void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 18;
            style.alignment = TextAnchor.MiddleCenter;

            float width = 420f;
            float height = 35f;
            float left = (Screen.width - width) * 0.5f;
            float top = 20f; // Top-center HUD banner

            string hudText = "";

            switch (CurrentState)
            {
                case MissionState.ObjectivePending:
                    style.normal.textColor = new Color(1.0f, 0.85f, 0.2f); // Yellowish
                    hudText = "OBJECTIVE: Secure the laboratory objective";
                    break;

                case MissionState.ObjectiveCompleted:
                case MissionState.ExtractionAvailable:
                    style.normal.textColor = new Color(0.2f, 1.0f, 0.3f); // Greenish
                    hudText = "EXTRACTION AVAILABLE";
                    break;

                case MissionState.MissionCompleted:
                    style.normal.textColor = new Color(0.2f, 0.8f, 1.0f); // Cyan / Blue
                    hudText = "MISSION COMPLETE";
                    break;
            }

            GUI.Box(new Rect(left, top, width, height), hudText, style);
        }
    }
}
