using UnityEngine;
using UnityEngine.InputSystem;
using TacticalShooter.Systems.Interaction;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.Systems.Mission
{
    public class ObjectiveInteractable : MonoBehaviour, IInteractable
    {
        [Header("Objective Terminal Config")]
        [Tooltip("Identifier for this terminal.")]
        [SerializeField] private string terminalId = "TERM_1";
        [Tooltip("Display name shown on radar, HUD, and download progress bar.")]
        [SerializeField] private string terminalDisplayName = "Data Terminal";
        [Tooltip("Distance in meters within which the terminal prompt and interaction activates.")]
        [SerializeField] private float interactionRadius = 2.8f;
        [Tooltip("How many seconds the player must hold 'E' to complete the data download.")]
        [SerializeField] private float downloadDuration = 2.5f;
        [SerializeField] private Color pendingColor = new Color(1.0f, 0.75f, 0.15f); // Tech Amber / Gold
        [SerializeField] private Color completedColor = new Color(0.15f, 0.95f, 0.35f); // Vibrant Green

        [Header("Optional Renderer Overrides")]
        [SerializeField] private Renderer terminalRenderer;

        private bool isCompleted = false;
        private Transform playerTransform;
        private float downloadProgress = 0f;
        private float lastAudioTickTime = 0f;
        private Texture2D whiteTex;

        public string TerminalId { get => terminalId; set => terminalId = value; }
        public string DisplayName { get => terminalDisplayName; set => terminalDisplayName = value; }
        public bool IsCompleted => isCompleted;

        // Prompt text is empty so PlayerInteractor doesn't draw duplicate single-click UI
        public string PromptText => "";

        private void Awake()
        {
            if (terminalRenderer == null)
            {
                terminalRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
            }

            ApplyColor(pendingColor);
            GenerateTexture();
            FindPlayer();
        }

        private void GenerateTexture()
        {
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(1, 1);
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
            }
        }

        private void FindPlayer()
        {
            if (playerTransform != null) return;
            GameObject player = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
            if (player == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) player = pc.gameObject;
            }
            if (player != null) playerTransform = player.transform;
        }

        private void Update()
        {
            if (isCompleted) return;

            // Only active in Stage 1 (ObjectivePending)
            if (MissionManager.Instance != null && MissionManager.Instance.CurrentState != MissionState.ObjectivePending)
            {
                return;
            }

            FindPlayer();
            if (playerTransform == null) return;

            // Proximity calculation (horizontal & vertical)
            Vector3 pPos = playerTransform.position;
            Vector3 tPos = transform.position;
            float horizontalDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(tPos.x, tPos.z));
            float verticalDist = Mathf.Abs(pPos.y - tPos.y);

            bool inRange = horizontalDist <= interactionRadius && verticalDist <= 2.5f;

            if (inRange)
            {
                bool isHoldingE = Keyboard.current != null && Keyboard.current.eKey.isPressed;

                if (isHoldingE)
                {
                    downloadProgress += Time.deltaTime / downloadDuration;

                    // Play high-tech data processing audio tick every 0.18s
                    if (Time.time - lastAudioTickTime >= 0.18f)
                    {
                        lastAudioTickTime = Time.time;
                        CombatEffects.PlayDownloadTick(transform.position, downloadProgress);
                    }

                    if (downloadProgress >= 1.0f)
                    {
                        downloadProgress = 1.0f;
                        CompleteObjectiveDownload();
                    }
                }
                else
                {
                    // If player lets go of E before completion, smoothly drain progress back down
                    if (downloadProgress > 0f)
                    {
                        downloadProgress = Mathf.Max(0f, downloadProgress - Time.deltaTime * 2.2f);
                    }
                }
            }
            else
            {
                // Player stepped away: cancel and reset download progress
                downloadProgress = 0f;
            }
        }

        private void CompleteObjectiveDownload()
        {
            if (isCompleted) return;
            isCompleted = true;

            // Play celebratory audio chime
            CombatEffects.PlayObjectiveSecuredSound(transform.position);

            // Advance Mission to Stage 2 / Notify Multi-terminal progress
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnTerminalSecured(this);
            }

            ApplyColor(completedColor);
            Debug.Log($"[ObjectiveInteractable] ★ DATA DOWNLOAD COMPLETED: {terminalDisplayName} (100%)! ★");
        }

        public void Interact(GameObject interactor)
        {
            // Single click bypassed; player must hold 'E' to fill the download progress bar
        }

        private void ApplyColor(Color color)
        {
            if (terminalRenderer != null)
            {
                terminalRenderer.material.color = color;
            }
        }

        private void OnGUI()
        {
            if (isCompleted || playerTransform == null) return;
            if (MissionManager.Instance != null && MissionManager.Instance.CurrentState != MissionState.ObjectivePending) return;

            Vector3 pPos = playerTransform.position;
            Vector3 tPos = transform.position;
            float horizontalDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(tPos.x, tPos.z));
            float verticalDist = Mathf.Abs(pPos.y - tPos.y);

            bool inRange = horizontalDist <= interactionRadius && verticalDist <= 2.5f;
            if (!inRange) return;

            if (whiteTex == null) GenerateTexture();
            Color prevColor = GUI.color;

            if (downloadProgress <= 0.001f)
            {
                // 1. Sleek Prompt: [ HOLD 'E' TO DOWNLOAD DATA ]
                float boxW = 270f;
                float boxH = 38f;
                float boxX = (Screen.width - boxW) * 0.5f;
                float boxY = Screen.height * 0.5f + 65f;

                // Dark translucent backing
                GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.88f);
                GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), whiteTex);

                // Golden amber accent line
                GUI.color = new Color(1.0f, 0.78f, 0.15f, 0.95f);
                GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2f, boxW, 2f), whiteTex);

                // Prompt Label
                GUI.color = Color.white;
                GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                promptStyle.normal.textColor = new Color(1.0f, 0.88f, 0.25f);
                GUI.Label(new Rect(boxX, boxY, boxW, boxH), $"[ HOLD 'E' TO HACK {terminalDisplayName.ToUpper()} ]", promptStyle);
            }
            else
            {
                // 2. High-Tech Data Install / Download Loading Bar
                float boxW = 420f;
                float boxH = 84f;
                float boxX = (Screen.width - boxW) * 0.5f;
                float boxY = Screen.height * 0.5f + 45f;

                // Translucent tech background
                GUI.color = new Color(0.03f, 0.06f, 0.1f, 0.94f);
                GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), whiteTex);

                // Top & Bottom Accent Border outlines
                GUI.color = new Color(0.25f, 0.75f, 1.0f, 0.75f);
                GUI.DrawTexture(new Rect(boxX, boxY, boxW, 2f), whiteTex);
                GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2f, boxW, 2f), whiteTex);

                // Title + Percentage
                int percent = Mathf.Clamp(Mathf.FloorToInt(downloadProgress * 100f), 0, 100);
                GUI.color = Color.white;
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);
                GUI.Label(new Rect(boxX + 20f, boxY + 8f, 280f, 22f), $"💾 DOWNLOADING {terminalDisplayName.ToUpper()}...", titleStyle);

                GUIStyle percentStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                percentStyle.normal.textColor = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(0.25f, 1f, 0.5f), downloadProgress);
                GUI.Label(new Rect(boxX + boxW - 90f, boxY + 8f, 70f, 22f), $"{percent}%", percentStyle);

                // Progress Bar Dimensions
                float barX = boxX + 20f;
                float barY = boxY + 34f;
                float barW = boxW - 40f;
                float barH = 16f;

                // Track Background
                GUI.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
                GUI.DrawTexture(new Rect(barX, barY, barW, barH), whiteTex);

                // Fill Bar
                float fillW = barW * Mathf.Clamp01(downloadProgress);
                GUI.color = Color.Lerp(new Color(1.0f, 0.75f, 0.15f), new Color(0.2f, 1.0f, 0.45f), downloadProgress);
                GUI.DrawTexture(new Rect(barX, barY, fillW, barH), whiteTex);

                // Track Outline
                GUI.color = new Color(0.35f, 0.6f, 0.8f, 0.7f);
                GUI.DrawTexture(new Rect(barX, barY, barW, 1f), whiteTex);
                GUI.DrawTexture(new Rect(barX, barY + barH - 1f, barW, 1f), whiteTex);
                GUI.DrawTexture(new Rect(barX, barY, 1f, barH), whiteTex);
                GUI.DrawTexture(new Rect(barX + barW - 1f, barY, 1f, barH), whiteTex);

                // Status Subtext
                GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                subStyle.normal.textColor = new Color(0.75f, 0.82f, 0.9f);
                GUI.Label(new Rect(boxX + 10f, boxY + 56f, boxW - 20f, 22f), "Hold 'E' to complete data installation", subStyle);
            }

            GUI.color = prevColor;
        }
    }
}
