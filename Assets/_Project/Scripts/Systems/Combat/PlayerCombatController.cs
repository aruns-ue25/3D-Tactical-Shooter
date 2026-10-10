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
        [SerializeField] private Vector3 firstPersonCamPos = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private Vector3 thirdPersonCamPos = new Vector3(0.55f, 1.75f, -2.2f);
        [SerializeField] private float viewSwitchSpeed = 10f;
        private bool isThirdPerson = false;
        private Vector3 targetCamLocalPos;

        [Header("Visual Feedback")]
        [SerializeField] private bool showCrosshair = true;

        private ObjectCarrier objectCarrier;
        private float lastFireFeedbackTime = -1f;
        private bool lastShotHitTarget = false;
        private string lastShotFeedback = "Ready (Left Click to Fire)";

        public HitscanWeapon CurrentWeapon => weapon;
        public bool IsThirdPerson => isThirdPerson;

        private void Awake()
        {
            // Auto-locate weapon
            if (weapon == null)
            {
                weapon = GetComponent<HitscanWeapon>();
                if (weapon == null) weapon = GetComponentInChildren<HitscanWeapon>();
            }

            // Auto-locate camera
            if (playerCamera == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) playerCamera = cam.transform;
                else if (Camera.main != null) playerCamera = Camera.main.transform;
            }

            // Auto-locate viewmodel
            if (weaponViewModel == null && playerCamera != null)
            {
                weaponViewModel = playerCamera.GetComponentInChildren<WeaponViewModel>();
            }

            // Link muzzle point if assigned
            if (weapon != null && weaponViewModel != null && weapon.MuzzlePoint == null)
            {
                weapon.MuzzlePoint = weaponViewModel.MuzzlePoint;
            }

            // ObjectCarrier
            objectCarrier = GetComponent<ObjectCarrier>();
            if (objectCarrier == null) objectCarrier = GetComponentInParent<ObjectCarrier>();

            targetCamLocalPos = firstPersonCamPos;
            if (playerCamera != null)
            {
                playerCamera.localPosition = firstPersonCamPos;
            }
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
                    // Hide first-person gun model in third-person view
                    weaponViewModel.gameObject.SetActive(!isThirdPerson);
                }
            }

            // Smooth camera transition
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, targetCamLocalPos, Time.deltaTime * viewSwitchSpeed);
        }

        private void FirePrimary()
        {
            if (weapon == null || playerCamera == null) return;

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

            GUIStyle keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10
            };
            keyStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            string viewName = isThirdPerson ? "3RD PERSON" : "1ST PERSON";

            GUI.Label(new Rect(x + 10, y + 6, boxWidth - 20, 18), "WEAPON: TACTICAL CARBINE (SEMI-AUTO)", headerStyle);
            GUI.Label(new Rect(x + 10, y + 25, boxWidth - 20, 18), $"STATUS: READY  |  VIEW: {viewName} [V key]", bodyStyle);
            GUI.Label(new Rect(x + 10, y + 45, boxWidth - 20, 18), $"LAST: {lastShotFeedback}", bodyStyle);

            GUI.color = prevColor;
        }
    }
}
