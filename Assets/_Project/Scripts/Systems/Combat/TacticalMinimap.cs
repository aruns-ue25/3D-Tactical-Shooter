using UnityEngine;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Tactical Radar Minimap & 3D Waypoint Tracker.
    /// Shows:
    /// 1. Top-left HUD Radar: player position, player direction, radar grid, and Target Dummy location.
    /// 2. 3D In-World Navigation Waypoint: on-screen bracket pointing directly at the Target Dummy with distance in meters.
    /// 3. Off-screen navigation pointers so the player never gets lost.
    /// </summary>
    public class TacticalMinimap : MonoBehaviour
    {
        [Header("Radar Settings")]
        [SerializeField] private bool showRadar = true;
        [SerializeField] private float radarRadiusMeters = 35f;
        [SerializeField] private float radarPixelSize = 180f;
        [SerializeField] private Vector2 radarScreenOffset = new Vector2(20f, 20f);

        [Header("3D Waypoint Settings")]
        [SerializeField] private bool show3DWaypoint = true;

        private Transform playerTransform;
        private Transform dummyTransform;
        private TargetDummy targetDummy;
        private Camera cachedCamera;
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

            if (dummyTransform == null)
            {
                GameObject dummy = GameObject.Find("TargetDummy_CombatTest");
                if (dummy != null)
                {
                    dummyTransform = dummy.transform;
                    targetDummy = dummy.GetComponent<TargetDummy>();
                }
            }
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

        private Camera GetCamera()
        {
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled) return cachedCamera;
            cachedCamera = Camera.main;
            if (cachedCamera == null) cachedCamera = FindFirstObjectByType<Camera>();
            return cachedCamera;
        }

        private void OnGUI()
        {
            FindReferences();
            if (playerTransform == null) return;

            if (showRadar)
            {
                DrawRadarMinimap();
            }

            if (show3DWaypoint && dummyTransform != null)
            {
                Draw3DWaypoint();
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

            // 5. Target Dummy Marker on Radar
            if (dummyTransform != null)
            {
                Vector3 delta = dummyTransform.position - playerTransform.position;
                float dist = delta.magnitude;

                // Project delta onto radar (scale by radar radius)
                float mapScale = (halfSize - 12f) / radarRadiusMeters;
                float dx = delta.x * mapScale;
                float dy = -delta.z * mapScale; // invert Z for screen Y

                // Clamp within radar box
                float clampedX = Mathf.Clamp(dx, -halfSize + 10f, halfSize - 10f);
                float clampedY = Mathf.Clamp(dy, -halfSize + 22f, halfSize - 10f);

                Vector2 dummyRadarPos = radarCenter + new Vector2(clampedX, clampedY);

                // Blinking Red Dot
                float blink = Mathf.PingPong(Time.time * 3f, 1f);
                GUI.color = new Color(1f, 0.2f * blink, 0.2f * blink, 1f);
                GUI.DrawTexture(new Rect(dummyRadarPos.x - 5, dummyRadarPos.y - 5, 10, 10), dotTex);

                // Dummy Label
                GUI.color = Color.white;
                GUIStyle dummyLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold
                };
                dummyLabelStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                GUI.Label(new Rect(dummyRadarPos.x + 7, dummyRadarPos.y - 8, 80, 16), $"TARGET {dist:0.0}m", dummyLabelStyle);
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

            GUI.color = prevColor;
        }

        private void Draw3DWaypoint()
        {
            Camera cam = GetCamera();
            if (cam == null || dummyTransform == null) return;

            Vector3 targetWorldPos = dummyTransform.position + Vector3.up * 1.4f;
            Vector3 screenPos = cam.WorldToScreenPoint(targetWorldPos);
            float distance = Vector3.Distance(playerTransform.position, dummyTransform.position);

            Color prevColor = GUI.color;

            // In front of camera
            if (screenPos.z > 0f)
            {
                float x = screenPos.x;
                float y = Screen.height - screenPos.y;

                // Chevron and distance box
                float boxW = 160f;
                float boxH = 34f;
                float bx = x - boxW * 0.5f;
                float by = y - boxH - 24f;

                // Background tag
                GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
                GUI.DrawTexture(new Rect(bx, by, boxW, boxH), dotTex);

                // Red Accent Line
                GUI.color = new Color(1f, 0.25f, 0.2f, 0.9f);
                GUI.DrawTexture(new Rect(bx, by, boxW, 2), dotTex);

                // Waypoint Text
                GUIStyle wpStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                wpStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(bx, by + 2, boxW, 16), "🎯 TARGET DUMMY", wpStyle);

                GUIStyle distStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10
                };
                distStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(bx, by + 16, boxW, 16), $"[ DISTANCE: {distance:0.1}m ]", distStyle);

                // Downward Pointer ▼
                GUIStyle arrowStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                arrowStyle.normal.textColor = new Color(1f, 0.3f, 0.2f);
                GUI.Label(new Rect(x - 12, by + boxH - 2, 24, 20), "▼", arrowStyle);
            }
            else
            {
                // Target is BEHIND player: Show off-screen edge turn indicator
                Vector3 toTarget = (dummyTransform.position - playerTransform.position).normalized;
                float dotRight = Vector3.Dot(playerTransform.right, toTarget);

                GUIStyle edgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                edgeStyle.normal.textColor = new Color(1f, 0.3f, 0.2f);

                if (dotRight > 0)
                {
                    // Target is behind to the right
                    GUI.Label(new Rect(Screen.width - 240, Screen.height * 0.5f - 20, 220, 30), $"TARGET BEHIND ({distance:0}m) ►►", edgeStyle);
                }
                else
                {
                    // Target is behind to the left
                    GUI.Label(new Rect(30, Screen.height * 0.5f - 20, 220, 30), $"◄◄ TARGET BEHIND ({distance:0}m)", edgeStyle);
                }
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
