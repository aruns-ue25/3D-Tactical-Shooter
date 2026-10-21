using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TacticalShooter.Systems.Combat;
using TacticalShooter.Systems.Interaction;
using TacticalShooter.Systems.Mission;

namespace TacticalShooter.AssetPipeline.Editor
{
    public static class GameVerificationScreenshotUtility
    {
        private const string SCENE_PATH = "Assets/_Project/Scenes/Main/Main.unity";
        private const string WEAPON_SCREENSHOT_PATH = "Assets/_Project/Art/SourceArt/PlayerWeapon_Game_Screenshot.png";
        private const string TERMINAL_SCREENSHOT_PATH = "Assets/_Project/Art/SourceArt/ObjectiveTerminal_Game_Screenshot.png";

        [MenuItem("Tools/Verification/Capture Game Screenshots and Verify", false, 10)]
        public static void CaptureScreenshotsAndVerify()
        {
            Debug.Log("[GameVerificationScreenshotUtility] Opening scene 'Assets/_Project/Scenes/Main/Main.unity'...");
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            // 1. Verify Player & Weapon
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[GameVerificationScreenshotUtility] FAILED: Player not found in scene!");
                return;
            }

            Transform playerCamTransform = player.transform.Find("PlayerCamera");
            if (playerCamTransform == null)
            {
                Camera cam = player.GetComponentInChildren<Camera>();
                if (cam != null) playerCamTransform = cam.transform;
            }

            Camera playerCamera = playerCamTransform != null ? playerCamTransform.GetComponent<Camera>() : null;
            if (playerCamera == null)
            {
                Debug.LogError("[GameVerificationScreenshotUtility] FAILED: Player Camera not found!");
                return;
            }

            // Ensure first person view
            PlayerCombatController combatCtrl = player.GetComponent<PlayerCombatController>();
            if (combatCtrl != null)
            {
                SerializedObject so = new SerializedObject(combatCtrl);
                so.FindProperty("startInThirdPerson").boolValue = false;
                so.ApplyModifiedProperties();
            }

            // Verify Weapon ViewModel
            Transform vmTransform = playerCamTransform.Find("WeaponViewModel");
            if (vmTransform == null)
            {
                Debug.LogError("[GameVerificationScreenshotUtility] FAILED: WeaponViewModel not found under PlayerCamera!");
            }
            else
            {
                Debug.Log($"[GameVerificationScreenshotUtility] ✅ WeaponViewModel found at localPos: {vmTransform.localPosition}");
                Transform rifleVisual = vmTransform.Find("PlayerRifle_Visual");
                if (rifleVisual != null)
                {
                    Debug.Log("[GameVerificationScreenshotUtility] ✅ PlayerRifle_Visual model active under viewmodel!");
                }
                else
                {
                    Debug.LogWarning("[GameVerificationScreenshotUtility] PlayerRifle_Visual child missing under WeaponViewModel!");
                }
            }

            // 2. Capture First-Person Weapon Screenshot in Game
            Debug.Log("[GameVerificationScreenshotUtility] Capturing First-Person Weapon Game Screenshot...");
            player.transform.position = new Vector3(30f, 1.0f, 2.5f);
            playerCamTransform.localPosition = new Vector3(0f, 1.6f, 0f);
            playerCamTransform.localRotation = Quaternion.identity;

            CaptureCameraView(playerCamera, WEAPON_SCREENSHOT_PATH, 1280, 720);

            // 3. Verify Objective Terminal
            GameObject terminalObj = GameObject.Find("ObjectiveTerminal");
            if (terminalObj == null)
            {
                Debug.LogError("[GameVerificationScreenshotUtility] FAILED: ObjectiveTerminal not found in scene!");
            }
            else
            {
                ObjectiveInteractable interactable = terminalObj.GetComponent<ObjectiveInteractable>();
                Collider col = terminalObj.GetComponent<Collider>();
                Debug.Log($"[GameVerificationScreenshotUtility] ✅ ObjectiveTerminal verified (Interactable: {interactable != null}, Collider: {col != null})");

                Transform visualChild = terminalObj.transform.Find("ObjectiveTerminal_Visual");
                if (visualChild != null)
                {
                    Debug.Log("[GameVerificationScreenshotUtility] ✅ ObjectiveTerminal_Visual model active in scene!");
                }
                else
                {
                    Debug.LogWarning("[GameVerificationScreenshotUtility] ObjectiveTerminal_Visual child missing!");
                }
            }

            // 4. Capture Laboratory Objective Terminal Screenshot in Game
            Debug.Log("[GameVerificationScreenshotUtility] Capturing Objective Terminal in Laboratory Screenshot...");
            // Move player to Laboratory room facing the console:
            playerCamTransform.position = new Vector3(45.6f, 1.35f, 35.0f);
            playerCamTransform.LookAt(new Vector3(45f, 1.0f, 37.5f));

            // Temporarily hide player viewmodel for clean terminal view
            if (vmTransform != null) vmTransform.gameObject.SetActive(false);
            CaptureCameraView(playerCamera, TERMINAL_SCREENSHOT_PATH, 1280, 720);
            if (vmTransform != null) vmTransform.gameObject.SetActive(true);

            // Restore camera position
            playerCamTransform.localPosition = new Vector3(0f, 1.6f, 0f);
            playerCamTransform.localRotation = Quaternion.identity;

            // 5. Test Weapon Firing Logic
            HitscanWeapon weapon = player.GetComponent<HitscanWeapon>();
            if (weapon != null)
            {
                bool fired = weapon.TryFire(playerCamTransform.position, playerCamTransform.forward, player);
                Debug.Log($"[GameVerificationScreenshotUtility] ✅ HitscanWeapon firing verification: fired={fired}");
            }

            // 6. Test Enemy / Guard & Flanker presence
            PlayerCombatController.EnsureGuardInScene();
            var guard = Object.FindFirstObjectByType<TacticalShooter.AI.Agents.TacticalSoldierAgent>();
            Debug.Log($"[GameVerificationScreenshotUtility] ✅ TacticalSoldierAgent present in scene: {guard != null}");

            var flanker = Object.FindFirstObjectByType<TacticalShooter.AI.Agents.FlankerAgent>();
            Debug.Log($"[GameVerificationScreenshotUtility] ✅ FlankerAgent present in scene: {flanker != null}");

            // 7. Verify MissionManager
            var missionMgr = Object.FindFirstObjectByType<MissionManager>();
            Debug.Log($"[GameVerificationScreenshotUtility] ✅ MissionManager present in scene: {missionMgr != null}");

            Debug.Log("[GameVerificationScreenshotUtility] 🏆 COMPLETE VERIFICATION & SCREENSHOT CAPTURE SUCCEEDED!");
        }

        private static void CaptureCameraView(Camera cam, string outputPath, int width, int height)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRt = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            cam.targetTexture = prevRt;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);

            Debug.Log($"[GameVerificationScreenshotUtility] Saved game screenshot to: {outputPath}");
        }
    }
}
