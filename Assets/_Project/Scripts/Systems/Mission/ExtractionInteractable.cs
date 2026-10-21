using UnityEngine;
using UnityEngine.InputSystem;
using TacticalShooter.Systems.Interaction;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.Systems.Mission
{
    public class ExtractionInteractable : MonoBehaviour, IInteractable
    {
        [Header("Extraction Settings")]
        [Tooltip("Horizontal radius in meters for standing on the extraction pad.")]
        [SerializeField] private float touchRadius = 2.8f;
        [Tooltip("How many seconds the player must hold 'E' to evacuate and complete the mission.")]
        [SerializeField] private float extractDuration = 2.5f;
        [Tooltip("If true, requires completing Stage 1 (Lab Terminal) before extraction unlocks.")]
        [SerializeField] private bool requireObjective = true;

        [Header("Visual Colors")]
        [SerializeField] private Color lockedColor = new Color(0.9f, 0.2f, 0.1f);       // Red (locked in Stage 1)
        [SerializeField] private Color availableColor = new Color(0.15f, 0.95f, 0.35f); // Vibrant Emerald Green (unlocked in Stage 2)
        [SerializeField] private Color completedColor = new Color(0.2f, 0.8f, 1.0f);     // Cyan/Blue (won)

        [Header("World Visuals")]
        [SerializeField] private Renderer extractionRenderer;

        private bool isCompleted = false;
        private Transform playerTransform;
        private float lockedFeedbackTimer = 0f;
        private float extractProgress = 0f;
        private float lastAudioTickTime = 0f;
        private Texture2D whiteTex;

        // Custom hold UI handles prompts directly
        public string PromptText => "";

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (extractionRenderer == null)
            {
                extractionRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
            }

            GenerateTexture();
            FindPlayer();
            UpdateVisualState();
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

            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        public bool IsGoalUnlocked()
        {
            if (!requireObjective) return true;
            if (MissionManager.Instance == null) return false;
            return MissionManager.Instance.CurrentState == MissionState.ExtractionAvailable;
        }

        private void Update()
        {
            UpdateVisualState();

            if (lockedFeedbackTimer > 0f)
            {
                lockedFeedbackTimer -= Time.deltaTime;
            }

            if (isCompleted) return;

            FindPlayer();
            if (playerTransform == null) return;

            // Proximity check on extraction pad
            Vector3 pPos = playerTransform.position;
            Vector3 gPos = transform.position;
            float horizontalDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(gPos.x, gPos.z));
            float verticalDist = Mathf.Abs(pPos.y - gPos.y);

            bool inRange = horizontalDist <= touchRadius && verticalDist <= 2.5f;

            if (inRange)
            {
                if (!IsGoalUnlocked())
                {
                    // Locked in Stage 1: notify if player tries to interact
                    if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    {
                        if (lockedFeedbackTimer <= 0f)
                        {
                            lockedFeedbackTimer = 2.5f;
                            CombatEffects.PlayHitImpact(transform.position);
                            if (MissionManager.Instance != null)
                            {
                                MissionManager.Instance.ShowSubtitle($"NOTICE: Extraction Locked. Secure all {MissionManager.Instance.RequiredTerminals} Data Terminals first! [{MissionManager.Instance.SecuredTerminalsCount}/{MissionManager.Instance.RequiredTerminals}]", 3.5f, new Color(1.0f, 0.4f, 0.3f));
                            }
                        }
                    }
                    extractProgress = 0f;
                }
                else
                {
                    // Unlocked in Stage 2: require holding 'E' to evacuate!
                    bool isHoldingE = Keyboard.current != null && Keyboard.current.eKey.isPressed;

                    if (isHoldingE)
                    {
                        extractProgress += Time.deltaTime / extractDuration;

                        // Audio feedback tick
                        if (Time.time - lastAudioTickTime >= 0.18f)
                        {
                            lastAudioTickTime = Time.time;
                            CombatEffects.PlayDownloadTick(transform.position, extractProgress);
                        }

                        if (extractProgress >= 1.0f)
                        {
                            extractProgress = 1.0f;
                            TriggerGoalWin();
                        }
                    }
                    else
                    {
                        if (extractProgress > 0f)
                        {
                            extractProgress = Mathf.Max(0f, extractProgress - Time.deltaTime * 2.2f);
                        }
                    }
                }
            }
            else
            {
                // Player left extraction pad: reset progress
                extractProgress = 0f;
            }
        }

        public void Interact(GameObject interactor)
        {
            // Single click bypassed; player must hold 'E' to extract!
        }

        public void TriggerGoalWin()
        {
            if (isCompleted || !IsGoalUnlocked()) return;

            isCompleted = true;

            // Trigger Mission Complete in MissionManager
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.CompleteExtraction();
            }

            // Play triumphant procedural victory sound
            CombatEffects.PlayVictorySound(transform.position);

            UpdateVisualState();
            Debug.Log("[ExtractionInteractable] ★ EXTRACTION COMPLETED (100%) - YOU WIN! ★");
        }

        private void UpdateVisualState()
        {
            if (extractionRenderer == null) return;

            Color targetColor = lockedColor;

            if (isCompleted || (MissionManager.Instance != null && MissionManager.Instance.CurrentState == MissionState.MissionCompleted))
            {
                targetColor = completedColor;
            }
            else if (IsGoalUnlocked())
            {
                // Pulsing gentle emerald green when unlocked
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
                targetColor = new Color(availableColor.r * pulse, availableColor.g * pulse, availableColor.b * pulse, 1f);
            }

            if (extractionRenderer.material.color != targetColor)
            {
                extractionRenderer.material.color = targetColor;
            }
        }

        private void OnGUI()
        {
            if (isCompleted || playerTransform == null) return;
            if (MissionManager.Instance != null && MissionManager.Instance.CurrentState == MissionState.MissionCompleted) return;

            Vector3 pPos = playerTransform.position;
            Vector3 gPos = transform.position;
            float horizontalDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(gPos.x, gPos.z));
            float verticalDist = Mathf.Abs(pPos.y - gPos.y);

            bool inRange = horizontalDist <= touchRadius && verticalDist <= 2.5f;
            if (!inRange) return;

            if (whiteTex == null) GenerateTexture();
            Color prevColor = GUI.color;

            if (!IsGoalUnlocked())
            {
                // Locked Prompt in Stage 1
                float boxW = 340f;
                float boxH = 38f;
                float boxX = (Screen.width - boxW) * 0.5f;
                float boxY = Screen.height * 0.5f + 65f;

                GUI.color = new Color(0.06f, 0.04f, 0.04f, 0.9f);
                GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), whiteTex);

                GUI.color = new Color(1.0f, 0.3f, 0.25f, 0.95f);
                GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2f, boxW, 2f), whiteTex);

                GUI.color = Color.white;
                GUIStyle lockedStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
                lockedStyle.normal.textColor = new Color(1.0f, 0.4f, 0.35f);
                GUI.Label(new Rect(boxX, boxY, boxW, boxH), "🔒 EXTRACTION LOCKED: Secure Lab Terminal First", lockedStyle);
            }
            else
            {
                // Unlocked in Stage 2
                if (extractProgress <= 0.001f)
                {
                    // Prompt: [ HOLD 'E' TO EXTRACT & EVACUATE ]
                    float boxW = 280f;
                    float boxH = 38f;
                    float boxX = (Screen.width - boxW) * 0.5f;
                    float boxY = Screen.height * 0.5f + 65f;

                    GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.88f);
                    GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), whiteTex);

                    GUI.color = new Color(0.2f, 1.0f, 0.4f, 0.95f);
                    GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2f, boxW, 2f), whiteTex);

                    GUI.color = Color.white;
                    GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 13,
                        fontStyle = FontStyle.Bold
                    };
                    promptStyle.normal.textColor = new Color(0.35f, 1.0f, 0.55f);
                    GUI.Label(new Rect(boxX, boxY, boxW, boxH), "[ HOLD 'E' TO EXTRACT & WIN ]", promptStyle);
                }
                else
                {
                    // Spacious Evacuation Progress Bar
                    float boxW = 420f;
                    float boxH = 84f;
                    float boxX = (Screen.width - boxW) * 0.5f;
                    float boxY = Screen.height * 0.5f + 45f;

                    // Background
                    GUI.color = new Color(0.03f, 0.08f, 0.05f, 0.94f);
                    GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), whiteTex);

                    // Green accent borders
                    GUI.color = new Color(0.2f, 1.0f, 0.45f, 0.75f);
                    GUI.DrawTexture(new Rect(boxX, boxY, boxW, 2f), whiteTex);
                    GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2f, boxW, 2f), whiteTex);

                    // Title + Percentage
                    int percent = Mathf.Clamp(Mathf.FloorToInt(extractProgress * 100f), 0, 100);
                    GUI.color = Color.white;
                    GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 13,
                        fontStyle = FontStyle.Bold
                    };
                    titleStyle.normal.textColor = new Color(0.35f, 1.0f, 0.55f);
                    GUI.Label(new Rect(boxX + 20f, boxY + 8f, 280f, 22f), "🚁 EVACUATING SQUAD...", titleStyle);

                    GUIStyle percentStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        fontSize = 13,
                        fontStyle = FontStyle.Bold
                    };
                    percentStyle.normal.textColor = new Color(0.4f, 1.0f, 0.6f);
                    GUI.Label(new Rect(boxX + boxW - 90f, boxY + 8f, 70f, 22f), $"{percent}%", percentStyle);

                    // Bar Track Dimensions
                    float barX = boxX + 20f;
                    float barY = boxY + 34f;
                    float barW = boxW - 40f;
                    float barH = 16f;

                    GUI.color = new Color(0.08f, 0.16f, 0.10f, 0.95f);
                    GUI.DrawTexture(new Rect(barX, barY, barW, barH), whiteTex);

                    // Fill Bar
                    float fillW = barW * Mathf.Clamp01(extractProgress);
                    GUI.color = new Color(0.25f, 1.0f, 0.5f, 1f);
                    GUI.DrawTexture(new Rect(barX, barY, fillW, barH), whiteTex);

                    // Track Outline
                    GUI.color = new Color(0.3f, 0.7f, 0.4f, 0.7f);
                    GUI.DrawTexture(new Rect(barX, barY, barW, 1f), whiteTex);
                    GUI.DrawTexture(new Rect(barX, barY + barH - 1f, barW, 1f), whiteTex);
                    GUI.DrawTexture(new Rect(barX, barY, 1f, barH), whiteTex);
                    GUI.DrawTexture(new Rect(barX + barW - 1f, barY, 1f, barH), whiteTex);

                    // Subtext
                    GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold
                    };
                    subStyle.normal.textColor = new Color(0.8f, 0.95f, 0.85f);
                    GUI.Label(new Rect(boxX + 10f, boxY + 56f, boxW - 20f, 22f), "Hold 'E' to evacuate area and win", subStyle);
                }
            }

            GUI.color = prevColor;
        }
    }
}
