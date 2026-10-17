using System;
using UnityEngine;
using TacticalShooter.Systems.Mission;
using TacticalShooter.AI.Core;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Tactical Combat Radar & Waypoint System:
    /// 1. Top-Left Tactical Radar: Displays player coordinates, heading line, target blip, and mission objective blips.
    /// 2. 3D In-World Navigation Waypoint: On-screen bracket pointing directly at the active stage objective:
    ///    - Stage 1: Points to Laboratory Data Terminal [OBJ] (Amber)
    ///    - Stage 2: Points to Extraction Pad [LZ] (Green)
    ///    - Combat: Points to Target Dummy when alive (Red)
    /// </summary>
    public class TacticalMinimap : MonoBehaviour
    {
        [Header("Radar Settings")]
        [SerializeField] private bool showRadar = true;
        [SerializeField] private float radarPixelSize = 170f;
        [SerializeField] private float radarRadiusMeters = 35f;
        [SerializeField] private Vector2 radarScreenOffset = new Vector2(20f, 20f);

        private Transform playerTransform;
        private Transform dummyTransform;
        private TargetDummy targetDummy;
        private Health dummyHealth;
        private Transform goalTransform;
        private Transform objectiveTransform;
        private Texture2D radarBgTex;
        private Texture2D dotTex;

        private void Awake()
        {
            FindReferences();
            GenerateTextures();
        }

        private void FindReferences()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.Find("Player");
                if (player != null) playerTransform = player.transform;
            }

            if (dummyTransform == null || dummyHealth == null || !dummyHealth.IsAlive)
            {
                // Find an active enemy agent in the scene to track on radar
                var enemies = FindObjectsByType<TacticalShooter.AI.Core.AIAgentBase>(FindObjectsSortMode.None);
                foreach (var enemy in enemies)
                {
                    if (enemy != null && enemy.IsAlive)
                    {
                        dummyTransform = enemy.transform;
                        dummyHealth = enemy.GetComponent<Health>();
                        break;
                    }
                }
            }

            if (goalTransform == null)
            {
                GameObject goal = GameObject.Find("ExtractionZone") ?? GameObject.Find("ExtractionPoint");
                if (goal != null) goalTransform = goal.transform;
            }

            if (objectiveTransform == null)
            {
                GameObject obj = GameObject.Find("ObjectiveTerminal") ?? GameObject.Find("Objective Point") ?? GameObject.Find("ObjectivePoint");
                if (obj != null) objectiveTransform = obj.transform;
            }
        }

        private bool IsTargetAlive()
        {
            if (dummyHealth != null) return dummyHealth.IsAlive;
            if (targetDummy != null && targetDummy.HealthComponent != null) return targetDummy.HealthComponent.IsAlive;
            if (dummyTransform != null)
            {
                dummyHealth = dummyTransform.GetComponent<Health>();
                if (dummyHealth != null) return dummyHealth.IsAlive;
            }
            return false;
        }

        private void GenerateTextures()
        {
            if (radarBgTex == null)
            {
                radarBgTex = new Texture2D(1, 1);
                radarBgTex.SetPixel(0, 0, new Color(0.04f, 0.08f, 0.12f, 0.88f));
                radarBgTex.Apply();
            }

            if (dotTex == null)
            {
                dotTex = new Texture2D(1, 1);
                dotTex.SetPixel(0, 0, Color.white);
                dotTex.Apply();
            }
        }


        private void OnGUI()
        {
            FindReferences();
            if (playerTransform == null) return;

            if (showRadar)
            {
                DrawRadarMinimap();
            }
        }

        private void DrawRadarMinimap()
        {
            float rx = radarScreenOffset.x;
            float ry = radarScreenOffset.y;
            float size = radarPixelSize;
            float halfSize = size * 0.5f;
            Vector2 radarCenter = new Vector2(rx + halfSize, ry + halfSize);

            Color prevColor = GUI.color;

            // 1. Radar Background Frame
            GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.9f);
            GUI.DrawTexture(new Rect(rx, ry, size, size), radarBgTex);

            // 2. Cyan Tech Border
            GUI.color = new Color(0.15f, 0.75f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(rx, ry, size, 2), dotTex);
            GUI.DrawTexture(new Rect(rx, ry + size - 2, size, 2), dotTex);
            GUI.DrawTexture(new Rect(rx, ry, 2, size), dotTex);
            GUI.DrawTexture(new Rect(rx + size - 2, ry, 2, size), dotTex);

            // 3. Range Rings (10m, 25m)
            GUI.color = new Color(0.2f, 0.5f, 0.8f, 0.35f);
            DrawCrosshairRings(radarCenter, halfSize * 0.4f);
            DrawCrosshairRings(radarCenter, halfSize * 0.8f);

            // 4. Radar Header
            GUI.color = Color.white;
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);
            GUI.Label(new Rect(rx + 8, ry + 4, size, 16), "TACTICAL RADAR (35m)", titleStyle);

            // 5. Target Dummy Marker on Radar (Only when alive)
            if (dummyTransform != null && IsTargetAlive())
            {
                Vector3 delta = dummyTransform.position - playerTransform.position;
                float dist = delta.magnitude;

                float mapScale = (halfSize - 12f) / radarRadiusMeters;
                float dx = delta.x * mapScale;
                float dy = -delta.z * mapScale;

                float clampedX = Mathf.Clamp(dx, -halfSize + 10f, halfSize - 10f);
                float clampedY = Mathf.Clamp(dy, -halfSize + 22f, halfSize - 10f);
                Vector2 dummyRadarPos = radarCenter + new Vector2(clampedX, clampedY);

                float blink = Mathf.PingPong(Time.time * 3f, 1f);
                GUI.color = new Color(1f, 0.2f * blink, 0.2f * blink, 1f);
                GUI.DrawTexture(new Rect(dummyRadarPos.x - 5, dummyRadarPos.y - 5, 10, 10), dotTex);

                GUI.color = Color.white;
                GUIStyle dummyLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold
                };
                dummyLabelStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                GUI.Label(new Rect(dummyRadarPos.x + 7, dummyRadarPos.y - 8, 80, 16), $"TARGET {dist:0.0}m", dummyLabelStyle);
            }

            // 5b. Hostile AI Agents on Radar (Tactical Soldier / Guard)
            AIAgentBase[] activeAgents = FindObjectsByType<AIAgentBase>(FindObjectsSortMode.None);
            if (activeAgents != null)
            {
                for (int i = 0; i < activeAgents.Length; i++)
                {
                    AIAgentBase agent = activeAgents[i];
                    if (agent != null && agent.IsAlive)
                    {
                        Vector3 delta = agent.transform.position - playerTransform.position;
                        float dist = delta.magnitude;
                        float mapScale = (halfSize - 12f) / radarRadiusMeters;
                        float dx = delta.x * mapScale;
                        float dy = -delta.z * mapScale;

                        float clampedX = Mathf.Clamp(dx, -halfSize + 10f, halfSize - 10f);
                        float clampedY = Mathf.Clamp(dy, -halfSize + 22f, halfSize - 10f);
                        Vector2 agentRadarPos = radarCenter + new Vector2(clampedX, clampedY);

                        float blink = Mathf.PingPong(Time.time * 4f, 1f);
                        GUI.color = new Color(1f, 0.15f * blink, 0.15f * blink, 1f);
                        GUI.DrawTexture(new Rect(agentRadarPos.x - 5, agentRadarPos.y - 5, 10, 10), dotTex);

                        GUI.color = Color.white;
                        GUIStyle agentLabelStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 9,
                            fontStyle = FontStyle.Bold
                        };
                        agentLabelStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);
                        GUI.Label(new Rect(agentRadarPos.x + 7, agentRadarPos.y - 8, 85, 16), $"[GUARD] {dist:0.0}m", agentLabelStyle);
                    }
                }
            }

            // 5c. Stage Mission Objectives on Radar
            bool isStage1 = MissionManager.Instance == null || MissionManager.Instance.CurrentState == MissionState.ObjectivePending;

            if (isStage1 && objectiveTransform != null)
            {
                // Stage 1: Amber Dot for Laboratory Terminal
                Vector3 delta = objectiveTransform.position - playerTransform.position;
                float dist = delta.magnitude;
                float mapScale = (halfSize - 12f) / radarRadiusMeters;
                float dx = delta.x * mapScale;
                float dy = -delta.z * mapScale;
                float clampedX = Mathf.Clamp(dx, -halfSize + 10f, halfSize - 10f);
                float clampedY = Mathf.Clamp(dy, -halfSize + 22f, halfSize - 10f);
                Vector2 objRadarPos = radarCenter + new Vector2(clampedX, clampedY);

                GUI.color = new Color(1.0f, 0.78f, 0.15f, 1f);
                GUI.DrawTexture(new Rect(objRadarPos.x - 5, objRadarPos.y - 5, 10, 10), dotTex);

                GUIStyle objStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold
                };
                objStyle.normal.textColor = new Color(1.0f, 0.85f, 0.2f);
                GUI.Label(new Rect(objRadarPos.x + 7, objRadarPos.y - 8, 85, 16), $"LAB {dist:0.0}m", objStyle);
            }
            else if (!isStage1 && goalTransform != null)
            {
                // Stage 2: Pulsing Emerald Green Dot for Extraction Zone
                Vector3 delta = goalTransform.position - playerTransform.position;
                float dist = delta.magnitude;
                float mapScale = (halfSize - 12f) / radarRadiusMeters;
                float dx = delta.x * mapScale;
                float dy = -delta.z * mapScale;
                float clampedX = Mathf.Clamp(dx, -halfSize + 10f, halfSize - 10f);
                float clampedY = Mathf.Clamp(dy, -halfSize + 22f, halfSize - 10f);
                Vector2 goalRadarPos = radarCenter + new Vector2(clampedX, clampedY);

                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
                GUI.color = new Color(0.2f * pulse, 1f * pulse, 0.45f * pulse, 1f);
                GUI.DrawTexture(new Rect(goalRadarPos.x - 5, goalRadarPos.y - 5, 10, 10), dotTex);

                GUIStyle goalStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold
                };
                goalStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
                GUI.Label(new Rect(goalRadarPos.x + 7, goalRadarPos.y - 8, 85, 16), $"EXTRACT {dist:0.0}m", goalStyle);
            }

            // 6. Player Marker (Center Green Arrow)
            GUI.color = new Color(0.2f, 1f, 0.4f, 1f);
            GUI.DrawTexture(new Rect(radarCenter.x - 4, radarCenter.y - 4, 8, 8), dotTex);

            // Player Heading Line
            Vector3 forward = playerTransform.forward;
            Vector2 forwardDir = new Vector2(forward.x, -forward.z).normalized;
            Vector2 forwardEnd = radarCenter + forwardDir * 16f;
            DrawLine(radarCenter, forwardEnd, new Color(0.2f, 1f, 0.4f, 0.9f), 2f);

            // Coordinates Display
            GUI.color = Color.white;
            GUIStyle coordStyle = new GUIStyle(GUI.skin.label) { fontSize = 9 };
            coordStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);
            GUI.Label(new Rect(rx + 8, ry + size - 18, size - 16, 16), $"POS: ({playerTransform.position.x:0.0}, {playerTransform.position.z:0.0})", coordStyle);

            // Subtle Objective Label Under Radar
            GUIStyle objBriefStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold
            };
            if (isStage1)
            {
                objBriefStyle.normal.textColor = new Color(1.0f, 0.82f, 0.2f);
                GUI.Label(new Rect(rx, ry + size + 3, size + 20, 16), "▶ STAGE 1: LAB TERMINAL", objBriefStyle);
            }
            else
            {
                objBriefStyle.normal.textColor = new Color(0.25f, 1.0f, 0.45f);
                GUI.Label(new Rect(rx, ry + size + 3, size + 20, 16), "▶ STAGE 2: EXTRACTION", objBriefStyle);
            }

            GUI.color = prevColor;
        }



        private void DrawCrosshairRings(Vector2 center, float radius)
        {
            int segments = 16;
            float step = (2f * Mathf.PI) / segments;
            Vector2 prev = center + new Vector2(Mathf.Cos(0), Mathf.Sin(0)) * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * step;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                DrawLine(prev, next, new Color(0.2f, 0.6f, 0.9f, 0.25f), 1f);
                prev = next;
            }
        }

        private void DrawLine(Vector2 p1, Vector2 p2, Color color, float width)
        {
            Color prev = GUI.color;
            GUI.color = color;
            Vector2 d = p2 - p1;
            float length = d.magnitude;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.DrawTexture(new Rect(p1.x, p1.y - width * 0.5f, length, width), dotTex);
            GUIUtility.RotateAroundPivot(-angle, p1);

            GUI.color = prev;
        }
    }
}
