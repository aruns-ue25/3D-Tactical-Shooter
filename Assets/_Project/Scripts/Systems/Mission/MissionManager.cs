using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        [Header("Multi-Terminal Objective Config")]
        [SerializeField] private int requiredTerminals = 3;
        private int securedTerminalsCount = 0;

        public int SecuredTerminalsCount => securedTerminalsCount;
        public int RequiredTerminals => requiredTerminals;

        private float startTime;
        private float completionTime;

        // Subtitle system: sleek bottom-of-screen message that auto-fades without disturbing the screen
        private string currentSubtitle = "";
        private float subtitleTimer = 0f;
        private float subtitleDuration = 6.0f;
        private Color subtitleColor = Color.white;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            CurrentState = MissionState.ObjectivePending;
            securedTerminalsCount = 0;
            startTime = Time.time;
        }

        private void Start()
        {
            // Initial mission objective subtitle
            ShowSubtitle($"MISSION: Infiltrate facility and secure all {requiredTerminals} Data Terminals [ 0 / {requiredTerminals} ] (Check Radar)", 7.0f, new Color(1.0f, 0.88f, 0.35f));
        }

        public void ShowSubtitle(string text, float duration = 5.5f, Color? color = null)
        {
            currentSubtitle = text;
            subtitleDuration = Mathf.Max(duration, 0.5f);
            subtitleTimer = subtitleDuration;
            subtitleColor = color ?? Color.white;
        }

        private void Update()
        {
            if (subtitleTimer > 0f)
            {
                subtitleTimer -= Time.deltaTime;
            }

            if (CurrentState == MissionState.MissionCompleted)
            {
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    RestartMission();
                }
            }
        }

        public void OnTerminalSecured(ObjectiveInteractable terminal)
        {
            if (CurrentState != MissionState.ObjectivePending) return;

            securedTerminalsCount++;
            string tName = terminal != null ? terminal.DisplayName : "Data Terminal";
            Debug.Log($"[MissionManager] ★ TERMINAL SECURED ({tName})! Progress: {securedTerminalsCount} / {requiredTerminals} ★");

            if (securedTerminalsCount < requiredTerminals)
            {
                ShowSubtitle($"MISSION UPDATE: {tName} Secured! [ {securedTerminalsCount} / {requiredTerminals} ] — Locate remaining terminals", 5.5f, new Color(1.0f, 0.88f, 0.35f));
            }
            else
            {
                CurrentState = MissionState.ExtractionAvailable;
                ShowSubtitle($"▶ ALL DATA TERMINALS SECURED [ {securedTerminalsCount} / {requiredTerminals} ] — EXTRACTION ZONE UNLOCKED ON HELIPAD (LEVEL 3)!", 7.5f, new Color(0.35f, 1.0f, 0.55f));
                Debug.Log("[MissionManager] ★ ALL TERMINALS COMPLETE! STAGE 2 UNLOCKED: Helipad Extraction is now AVAILABLE. ★");
            }
        }

        public void CompleteObjective()
        {
            if (CurrentState != MissionState.ObjectivePending) return;

            // Fallback transition
            securedTerminalsCount = requiredTerminals;
            CurrentState = MissionState.ExtractionAvailable;
            ShowSubtitle("MISSION UPDATE: All Terminals secured! Proceed to Extraction Zone (Level 3 Helipad)", 6.5f, new Color(0.35f, 1.0f, 0.55f));
            Debug.Log("[MissionManager] ★ STAGE 1 COMPLETE! STAGE 2 UNLOCKED: Extraction is now AVAILABLE. ★");
        }

        public void CompleteExtraction()
        {
            if (CurrentState != MissionState.ExtractionAvailable) return;

            CurrentState = MissionState.MissionCompleted;
            completionTime = Time.time - startTime;

            // Unlock mouse cursor so player can click victory buttons
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[MissionManager] ★ STAGE 2 COMPLETE! MISSION ACCOMPLISHED - YOU WIN! ★");
        }

        public void RestartMission()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnGUI()
        {
            if (CurrentState == MissionState.MissionCompleted)
            {
                DrawVictoryScreen();
                return;
            }

            // Draw sleek cinematic subtitle at bottom center
            DrawSubtitle();
        }

        private void DrawSubtitle()
        {
            if (subtitleTimer <= 0f || string.IsNullOrEmpty(currentSubtitle)) return;

            // Smooth fade-in and fade-out
            float alpha = 1f;
            if (subtitleTimer < 1.0f)
            {
                alpha = Mathf.Clamp01(subtitleTimer);
            }
            else if (subtitleTimer > subtitleDuration - 0.4f)
            {
                alpha = Mathf.Clamp01((subtitleDuration - subtitleTimer) / 0.4f);
            }

            if (alpha <= 0.01f) return;

            Color prevColor = GUI.color;

            // Center subtitle neatly at bottom of screen
            float boxWidth = Mathf.Clamp(currentSubtitle.Length * 9.2f + 36f, 320f, Screen.width * 0.85f);
            float boxHeight = 34f;
            float boxX = (Screen.width - boxWidth) * 0.5f;
            float boxY = Screen.height - 75f;

            // Clean dark translucent bar
            GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.72f * alpha);
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), Texture2D.whiteTexture);

            // Subtle accent bottom underline
            GUI.color = new Color(subtitleColor.r, subtitleColor.g, subtitleColor.b, 0.8f * alpha);
            GUI.DrawTexture(new Rect(boxX + 10f, boxY + boxHeight - 2f, boxWidth - 20f, 2f), Texture2D.whiteTexture);

            // Subtitle text
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            subStyle.normal.textColor = new Color(subtitleColor.r, subtitleColor.g, subtitleColor.b, alpha);
            GUI.Label(new Rect(boxX, boxY, boxWidth, boxHeight - 2f), currentSubtitle, subStyle);

            GUI.color = prevColor;
        }

        private void DrawVictoryScreen()
        {
            // Dim background overlay
            GUI.color = new Color(0f, 0f, 0f, 0.68f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float boxW = 520f;
            float boxH = 310f;
            float boxX = (Screen.width - boxW) * 0.5f;
            float boxY = (Screen.height - boxH) * 0.5f;

            // Tactical Box Background
            GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.96f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);

            // Gold accent borders
            GUI.color = new Color(1.0f, 0.82f, 0.2f, 0.95f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 4, boxW, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY, 4, boxH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX + boxW - 4, boxY, 4, boxH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Header: VICTORY
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(1.0f, 0.85f, 0.2f);
            GUI.Label(new Rect(boxX, boxY + 20, boxW, 34), "★ ★ ★ VICTORY! ★ ★ ★", headerStyle);

            // Large Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.25f, 0.95f, 1.0f);
            GUI.Label(new Rect(boxX, boxY + 60, boxW, 28), "ALL OBJECTIVES COMPLETED", titleStyle);

            // Subtext
            GUIStyle descStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
            descStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(boxX, boxY + 98, boxW, 22), "All 3 Data Terminals & Helipad Extraction fully completed!", descStyle);

            // Time Stat
            int minutes = (int)(completionTime / 60f);
            int seconds = (int)(completionTime % 60f);
            GUIStyle statStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            statStyle.normal.textColor = new Color(0.3f, 1.0f, 0.5f);
            GUI.Label(new Rect(boxX, boxY + 130, boxW, 24), $"TOTAL MISSION TIME: {minutes:00}:{seconds:00}", statStyle);

            // Restart Button
            float btnW = 240f;
            float btnH = 44f;
            float btnX = boxX + (boxW - btnW) * 0.5f;
            float btnY = boxY + 185f;

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnStyle.normal.textColor = Color.white;

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "🔄 PLAY AGAIN [R]", btnStyle))
            {
                RestartMission();
            }

            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(boxX, btnY + 48, boxW, 20), "Press 'R' on keyboard to restart anytime", hintStyle);
        }
    }
}
