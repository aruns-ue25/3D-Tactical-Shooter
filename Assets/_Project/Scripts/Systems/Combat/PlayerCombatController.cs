using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TacticalShooter.Systems.Interaction;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Player combat input and view controller.
    /// Supports:
    /// - Primary fire with recoil, hitmarkers, and live combat HUD
    /// - Perspective toggle (Press 'V' to toggle between First-Person and Third-Person over-the-shoulder view)
    /// - Code-level procedural generation of 1st-person 3D tactical carbine if missing
    /// - ObjectCarrier coordination (holding/throwing objects takes priority over weapon fire)
    /// </summary>
    public class PlayerCombatController : MonoBehaviour
    {
        [Header("Weapon & Aiming")]
        [SerializeField] private HitscanWeapon weapon;
        [SerializeField] private Transform playerCamera;
        [SerializeField] private WeaponViewModel weaponViewModel;

        [Header("Perspective Toggle (V Key)")]
        [SerializeField] private bool allowPerspectiveToggle = true;
        [SerializeField] private bool startInThirdPerson = true;
        [SerializeField] private Vector3 firstPersonCamPos = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private Vector3 thirdPersonCamPos = new Vector3(0.45f, 1.7f, -2.5f);
        [SerializeField] private float viewSwitchSpeed = 10f;
        private bool isThirdPerson = true;
        private Vector3 targetCamLocalPos;

        [Header("Visual Feedback")]
        [SerializeField] private bool showCrosshair = true;

        private PlayerController playerController;
        private ObjectCarrier objectCarrier;
        private Health playerHealth;
        private float lastFireFeedbackTime = -1f;
        private bool lastShotHitTarget = false;
        private string lastShotFeedback = "Ready (Left Click to Fire)";
        private float playerDamageFlashTimer = 0f;
        private AudioSource hurtAudioSource;

        public HitscanWeapon CurrentWeapon => weapon;
        public bool IsThirdPerson => isThirdPerson;
        public Health PlayerHealth => playerHealth;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerHealth = GetComponent<Health>();
            if (playerHealth == null)
            {
                playerHealth = gameObject.AddComponent<Health>();
            }
            playerHealth.SetMaxHealth(250f);
            playerHealth.OnDamaged += HandlePlayerDamaged;

            // Ensure Player has a real CapsuleCollider for incoming bullet raycasts and spherecasts
            CapsuleCollider playerCol = GetComponent<CapsuleCollider>();
            if (playerCol == null)
            {
                playerCol = gameObject.AddComponent<CapsuleCollider>();
                playerCol.height = 1.9f;
                playerCol.radius = 0.42f;
                playerCol.center = new Vector3(0f, 0.95f, 0f);
            }

            // Hurt audio source
            hurtAudioSource = gameObject.AddComponent<AudioSource>();
            hurtAudioSource.playOnAwake = false;
            hurtAudioSource.volume = 0.65f;
            hurtAudioSource.clip = CreateHurtAudioClip();

            // Auto-locate weapon
            if (weapon == null)
            {
                weapon = GetComponent<HitscanWeapon>();
                if (weapon == null) weapon = GetComponentInChildren<HitscanWeapon>();
                if (weapon == null) weapon = gameObject.AddComponent<HitscanWeapon>();
            }

            // Auto-locate camera
            if (playerCamera == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) playerCamera = cam.transform;
                else if (Camera.main != null) playerCamera = Camera.main.transform;
            }

            if (playerCamera != null)
            {
                if (playerCamera.gameObject.tag != "MainCamera")
                {
                    playerCamera.gameObject.tag = "MainCamera";
                }

                Camera actualCam = playerCamera.GetComponent<Camera>();
                if (actualCam != null)
                {
                    actualCam.nearClipPlane = 0.08f;
                }
            }

            // Ensure PlayerCharacterVisuals (the 3D person holding the gun) is attached
            if (GetComponent<PlayerCharacterVisuals>() == null)
            {
                gameObject.AddComponent<PlayerCharacterVisuals>();
            }

            // Ensure TacticalMinimap (radar and target tracker) is attached
            if (GetComponent<TacticalMinimap>() == null)
            {
                gameObject.AddComponent<TacticalMinimap>();
            }

            // Auto-locate or procedurally build the 3D Weapon in code
            if (weaponViewModel == null && playerCamera != null)
            {
                weaponViewModel = playerCamera.GetComponentInChildren<WeaponViewModel>();
                if (weaponViewModel == null)
                {
                    weaponViewModel = BuildTacticalWeaponViewModel(playerCamera);
                }
            }

            // Link muzzle point if assigned
            if (weapon != null && weaponViewModel != null && weapon.MuzzlePoint == null)
            {
                weapon.MuzzlePoint = weaponViewModel.MuzzlePoint;
            }

            // ObjectCarrier
            objectCarrier = GetComponent<ObjectCarrier>();
            if (objectCarrier == null) objectCarrier = GetComponentInParent<ObjectCarrier>();

            if (startInThirdPerson)
            {
                isThirdPerson = true;
            }

            targetCamLocalPos = isThirdPerson ? thirdPersonCamPos : firstPersonCamPos;
            if (playerCamera != null)
            {
                playerCamera.localPosition = targetCamLocalPos;
            }

            if (weaponViewModel != null)
            {
                weaponViewModel.gameObject.SetActive(!isThirdPerson);
            }
        }

        private void Start()
        {
            EnsureGuardInScene();
        }

        public static void EnsureGuardInScene()
        {
            var existingGuard = UnityEngine.Object.FindFirstObjectByType<TacticalShooter.AI.Agents.TacticalSoldierAgent>();
            if (existingGuard == null)
            {
                var spawner = UnityEngine.Object.FindFirstObjectByType<TacticalShooter.AI.Agents.TacticalSoldierSpawner>();
                if (spawner != null)
                {
                    spawner.EnsureSoldierInScene();
                }
                else
                {
                    TacticalShooter.AI.Agents.TacticalSoldierSpawner.SpawnTacticalSoldier(
                        new Vector3(30f, 0.1f, 25f),
                        new Vector3[]
                        {
                            new Vector3(30f, 0.1f, 24f),
                            new Vector3(34f, 0.1f, 26f),
                            new Vector3(34f, 0.1f, 34f),
                            new Vector3(28f, 0.1f, 32f)
                        },
                        28f
                    );
                }
            }
        }

        private WeaponViewModel BuildTacticalWeaponViewModel(Transform parentCam)
        {
            GameObject vmRoot = new GameObject("WeaponViewModel");
            vmRoot.transform.SetParent(parentCam, false);
            vmRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.45f);
            vmRoot.transform.localRotation = Quaternion.identity;

            WeaponViewModel vm = vmRoot.AddComponent<WeaponViewModel>();

#if UNITY_EDITOR
            GameObject customPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/PlayerRifle_Custom.prefab");
            if (customPrefab != null)
            {
                GameObject visual = UnityEngine.Object.Instantiate(customPrefab, vmRoot.transform);
                visual.name = "PlayerRifle_Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                foreach (var col in visual.GetComponentsInChildren<Collider>())
                {
                    Destroy(col);
                }

                Transform customMuzzle = visual.transform.Find("MuzzlePoint");
                if (customMuzzle != null && weapon != null)
                {
                    weapon.MuzzlePoint = customMuzzle;
                }
                return vm;
            }
#endif

            Color darkMetal = new Color(0.18f, 0.2f, 0.22f);
            Color darkAccents = new Color(0.1f, 0.1f, 0.1f);

            CreateGunPart(PrimitiveType.Cube, "Receiver", vmRoot.transform, Vector3.zero, new Vector3(0.06f, 0.08f, 0.28f), darkMetal);
            CreateGunPart(PrimitiveType.Cylinder, "Barrel", vmRoot.transform, new Vector3(0f, 0.015f, 0.22f), new Vector3(0.025f, 0.09f, 0.025f), darkAccents, Quaternion.Euler(90f, 0f, 0f));
            CreateGunPart(PrimitiveType.Cube, "Magazine", vmRoot.transform, new Vector3(0f, -0.07f, 0.03f), new Vector3(0.04f, 0.1f, 0.06f), darkAccents, Quaternion.Euler(15f, 0f, 0f));
            CreateGunPart(PrimitiveType.Cube, "Sight", vmRoot.transform, new Vector3(0f, 0.055f, 0.12f), new Vector3(0.02f, 0.03f, 0.03f), darkAccents);

            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(vmRoot.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.015f, 0.32f);

            if (weapon != null)
            {
                weapon.MuzzlePoint = muzzle.transform;
            }

            return vm;
        }

        private GameObject CreateGunPart(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color, Quaternion? rot = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = pos;
            part.transform.localScale = scale;
            if (rot.HasValue) part.transform.localRotation = rot.Value;

            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
                Material mat = new Material(shader);
                mat.color = color;
                rend.material = mat;
            }

            return part;
        }

        private void OnEnable()
        {
            if (weapon != null)
            {
                weapon.OnShotFired += HandleShotFired;
            }
        }

        private void OnDisable()
        {
            if (weapon != null)
            {
                weapon.OnShotFired -= HandleShotFired;
            }
        }

        private void HandleShotFired(Vector3 origin, Vector3 endPoint, bool hitDamageable)
        {
            lastFireFeedbackTime = Time.time;
            lastShotHitTarget = hitDamageable;

            if (hitDamageable)
            {
                lastShotFeedback = "<color=#33FF33>HIT TARGET! (-25 HP)</color>";
            }
            else
            {
                lastShotFeedback = "<color=#FFFF55>SHOT FIRED (Miss)</color>";
            }

            if (weaponViewModel != null && !isThirdPerson)
            {
                weaponViewModel.PlayRecoil();
            }
        }

        private void Update()
        {
            if (playerHealth != null && !playerHealth.IsAlive)
            {
                return;
            }

            HandlePerspectiveToggle();

            // Do not fire if carrying a crate/object
            if (objectCarrier != null && objectCarrier.IsHoldingObject)
            {
                return;
            }

            // Fire on Left Click
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                FirePrimary();
            }
        }

        private void HandlePerspectiveToggle()
        {
            if (!allowPerspectiveToggle || playerCamera == null) return;

            // Press 'V' key to switch between First-Person and Third-Person
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            {
                isThirdPerson = !isThirdPerson;
                targetCamLocalPos = isThirdPerson ? thirdPersonCamPos : firstPersonCamPos;

                if (weaponViewModel != null)
                {
                    weaponViewModel.gameObject.SetActive(!isThirdPerson);
                }
            }

            Vector3 desiredLocalPos = targetCamLocalPos;

            if (isThirdPerson)
            {
                // Smart Spring-Arm: Prevent camera from penetrating or seeing through walls
                Vector3 pivotWorld = transform.position + new Vector3(0f, 1.6f, 0f);
                Vector3 targetWorld = transform.TransformPoint(thirdPersonCamPos);
                Vector3 dir = targetWorld - pivotWorld;
                float targetDist = dir.magnitude;

                if (targetDist > 0.05f)
                {
                    dir /= targetDist;
                    float sphereRadius = 0.22f;

                    // SphereCast from player eye level towards camera position
                    if (Physics.SphereCast(pivotWorld, sphereRadius, dir, out RaycastHit hit, targetDist, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.collider.gameObject != gameObject)
                        {
                            float safeDist = Mathf.Max(0.25f, hit.distance - 0.12f);
                            targetWorld = pivotWorld + dir * safeDist;
                        }
                    }
                }

                desiredLocalPos = transform.InverseTransformPoint(targetWorld);
            }
            else
            {
                if (playerController != null)
                {
                    desiredLocalPos += playerController.HeadBobOffset;
                }
            }

            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, desiredLocalPos, Time.deltaTime * (isThirdPerson ? viewSwitchSpeed * 1.8f : viewSwitchSpeed));
        }

        private void FirePrimary()
        {
            if (weapon == null || playerCamera == null) return;

            // Route weapon muzzle point to the actual 3D rifle barrel in 3rd person!
            if (isThirdPerson)
            {
                var visuals = GetComponent<PlayerCharacterVisuals>();
                if (visuals != null && visuals.ThirdPersonMuzzlePoint != null)
                {
                    weapon.MuzzlePoint = visuals.ThirdPersonMuzzlePoint;
                }
            }
            else
            {
                if (weaponViewModel != null && weaponViewModel.MuzzlePoint != null)
                {
                    weapon.MuzzlePoint = weaponViewModel.MuzzlePoint;
                }
            }

            // Fire raycast forward from camera center
            weapon.TryFire(playerCamera.position, playerCamera.forward, gameObject);
        }

        private void OnGUI()
        {
            if (playerHealth != null && !playerHealth.IsAlive)
            {
                DrawDefeatScreenOverlay();
                DrawPlayerHealthCornerHUD();
                return;
            }

            DrawCrosshairAndHitmarker();
            DrawTacticalHUD();
            DrawPlayerHealthCornerHUD();
            DrawDamageScreenFlash();
        }

        private void DrawDefeatScreenOverlay()
        {
            // Dark vignette overlay with subtle dark crimson edge
            Color prevColor = GUI.color;
            GUI.color = new Color(0.08f, 0.01f, 0.01f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // Center Modal Panel
            float panelW = Mathf.Min(560f, Screen.width * 0.88f);
            float panelH = 190f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            // Background box
            GUI.color = new Color(0.04f, 0.04f, 0.05f, 0.95f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            // Red border
            GUI.color = new Color(0.95f, 0.15f, 0.15f, 0.9f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 3f, panelW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY, 3f, panelH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX + panelW - 3f, panelY, 3f, panelH), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1.0f, 0.2f, 0.2f);
            GUI.Label(new Rect(panelX, panelY + 22f, panelW, 35f), "☠ OPERATOR ELIMINATED ☠", titleStyle);

            // Subtitle
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
            GUI.Label(new Rect(panelX, panelY + 62f, panelW, 25f), "CRITICAL VITAL FAILURE — MISSION FAILED", subStyle);

            // Respawn Instruction with pulsing alpha
            float pulse = 0.65f + Mathf.PingPong(Time.time * 2.5f, 0.35f);
            GUIStyle respawnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            respawnStyle.normal.textColor = new Color(1.0f, 0.85f, 0.2f, pulse);
            GUI.Label(new Rect(panelX, panelY + 115f, panelW, 30f), "[ PRESS 'R' TO RESPAWN & RETRY ]", respawnStyle);
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= HandlePlayerDamaged;
            }
        }

        private void HandlePlayerDamaged(DamageInfo info)
        {
            playerDamageFlashTimer = 0.45f;
            if (hurtAudioSource != null)
            {
                hurtAudioSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
                hurtAudioSource.Play();
            }
        }

        private AudioClip CreateHurtAudioClip()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.22f);
            AudioClip clip = AudioClip.Create("PlayerHurt", lengthSamples, 1, sampleRate, false);
            float[] samples = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 22f);
                float lowImpact = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.6f;
                float punch = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.4f;
                samples[i] = (lowImpact + punch) * envelope;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private void DrawDamageScreenFlash()
        {
            if (playerDamageFlashTimer > 0f)
            {
                playerDamageFlashTimer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(playerDamageFlashTimer / 0.45f) * 0.42f;

                Color prevColor = GUI.color;
                GUI.color = new Color(0.9f, 0.05f, 0.05f, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }
        }

        private void DrawPlayerHealthCornerHUD()
        {
            float boxWidth = 280f;
            float boxHeight = 75f;
            float x = 20f;
            float y = Screen.height - boxHeight - 20f;

            Color prevColor = GUI.color;

            // 1. Dark tactical frame
            GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, boxWidth, boxHeight), Texture2D.whiteTexture);

            // 2. Tech Cyan Top Accent
            GUI.color = new Color(0.15f, 0.75f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, boxWidth, 2), Texture2D.whiteTexture);

            float hp = playerHealth != null ? Mathf.Max(0f, playerHealth.CurrentHealth) : 100f;
            float maxHp = playerHealth != null ? playerHealth.MaxHealth : 100f;
            float hpPct = Mathf.Clamp01(hp / maxHp);

            // 3. Header: Operator Vital Signs
            GUI.color = Color.white;
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);
            GUI.Label(new Rect(x + 10, y + 6, 170, 18), "✚ OPERATOR VITALS", headerStyle);

            // Numeric HP display
            GUIStyle hpNumberStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            Color hpTextColor = hpPct > 0.5f ? new Color(0.2f, 0.95f, 0.4f) : (hpPct > 0.25f ? Color.yellow : Color.red);
            hpNumberStyle.normal.textColor = hpTextColor;
            GUI.Label(new Rect(x + 150, y + 5, boxWidth - 160, 20), $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)} HP", hpNumberStyle);

            // 4. Large Health Bar
            float barX = x + 10f;
            float barY = y + 28f;
            float barW = boxWidth - 20f;
            float barH = 14f;

            // Background track
            GUI.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

            // Dynamic Fill
            Color barColor = hpPct > 0.5f 
                ? new Color(0.15f, 0.88f, 0.38f) 
                : (hpPct > 0.25f ? new Color(1.0f, 0.75f, 0.15f) : new Color(1.0f, 0.15f, 0.15f));

            if (hpPct <= 0.25f)
            {
                float pulse = Mathf.PingPong(Time.time * 5f, 1f);
                barColor = Color.Lerp(new Color(0.8f, 0.1f, 0.1f), Color.red, pulse);
            }

            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barX + 2, barY + 2, (barW - 4) * hpPct, barH - 4), Texture2D.whiteTexture);

            // 5. Status Footer
            GUI.color = Color.white;
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold
            };
            statusStyle.normal.textColor = hpPct > 0.25f ? new Color(0.7f, 0.85f, 0.95f, 0.75f) : new Color(1f, 0.3f, 0.3f, 0.95f);
            string statusText = hpPct > 0.5f ? "STATUS: OPTIMAL  [SHIELDS UP]" : (hpPct > 0.25f ? "STATUS: CAUTION  [DAMAGE DETECTED]" : "STATUS: CRITICAL!  [TAKE COVER]");
            GUI.Label(new Rect(x + 10, y + 46, boxWidth - 20, 16), statusText, statusStyle);

            GUI.color = prevColor;
        }

        private void DrawCrosshairAndHitmarker()
        {
            if (!showCrosshair) return;

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;

            bool isRecentShot = (Time.time - lastFireFeedbackTime) < 0.12f;
            Color crossColor = Color.white;

            if (isRecentShot)
            {
                crossColor = lastShotHitTarget ? Color.red : Color.yellow;
            }

            Color prevColor = GUI.color;
            GUI.color = crossColor;

            float size = isRecentShot ? 16f : 10f;
            float thickness = 2f;

            // Crosshair Bars
            GUI.DrawTexture(new Rect(centerX - size * 0.5f, centerY - thickness * 0.5f, size, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - thickness * 0.5f, centerY - size * 0.5f, thickness, size), Texture2D.whiteTexture);

            // Red Hitmarker X on target hit
            if (isRecentShot && lastShotHitTarget)
            {
                GUI.color = new Color(1f, 0.2f, 0.2f, 0.95f);
                GUIStyle hitStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                hitStyle.normal.textColor = Color.red;
                GUI.Label(new Rect(centerX - 15, centerY - 15, 30, 30), "✕", hitStyle);
            }

            GUI.color = prevColor;
        }

        private void DrawTacticalHUD()
        {
            float boxWidth = 280f;
            float boxHeight = 75f;
            float margin = 20f;
            float x = Screen.width - boxWidth - margin;
            float y = Screen.height - boxHeight - margin;

            Color prevColor = GUI.color;

            // Dark tactical box
            GUI.color = new Color(0.04f, 0.07f, 0.11f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, boxWidth, boxHeight), Texture2D.whiteTexture);

            // Cyan top accent line
            GUI.color = new Color(0.15f, 0.7f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, boxWidth, 2), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.3f, 0.85f, 1f);

            GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true
            };
            bodyStyle.normal.textColor = Color.white;

            string viewName = isThirdPerson ? "3RD PERSON" : "1ST PERSON";

            GUI.Label(new Rect(x + 10, y + 6, boxWidth - 20, 18), "WEAPON: TACTICAL CARBINE (SEMI-AUTO)", headerStyle);
            GUI.Label(new Rect(x + 10, y + 25, boxWidth - 20, 18), $"AMMO: ∞  |  VIEW: {viewName} [V key]", bodyStyle);
            GUI.Label(new Rect(x + 10, y + 45, boxWidth - 20, 18), $"STATUS: {lastShotFeedback}", bodyStyle);

            GUI.color = prevColor;
        }
    }
}
