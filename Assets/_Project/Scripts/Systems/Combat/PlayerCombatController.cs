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
        private float lastFireFeedbackTime = -1f;
        private bool lastShotHitTarget = false;
        private string lastShotFeedback = "Ready (Left Click to Fire)";

        public HitscanWeapon CurrentWeapon => weapon;
        public bool IsThirdPerson => isThirdPerson;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
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

        private WeaponViewModel BuildTacticalWeaponViewModel(Transform parentCam)
        {
            GameObject vmRoot = new GameObject("WeaponViewModel");
            vmRoot.transform.SetParent(parentCam, false);
            vmRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.45f);
            vmRoot.transform.localRotation = Quaternion.identity;

            WeaponViewModel vm = vmRoot.AddComponent<WeaponViewModel>();

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
            DrawCrosshairAndHitmarker();
            DrawTacticalHUD();
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
            GUI.Label(new Rect(x + 10, y + 25, boxWidth - 20, 18), $"STATUS: READY  |  VIEW: {viewName} [V key]", bodyStyle);
            GUI.Label(new Rect(x + 10, y + 45, boxWidth - 20, 18), $"LAST: {lastShotFeedback}", bodyStyle);

            GUI.color = prevColor;
        }
    }
}
