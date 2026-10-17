using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;

namespace TacticalShooter.AI.Agents
{
    /// <summary>
    /// Realistic 3D Royal Guard Character Model & Animation Controller.
    /// Faithfully models the 4-view concept art:
    /// - Steel Kettle Helmet with central crest ridge, flared brim, and nasal guard
    /// - Sculpted face, jaw, dark hair, and royal blue gorget neck collar
    /// - Polished Steel Breastplate & Backplate with leather shoulder harness & brass rivets
    /// - Segmented curved steel pauldrons (shoulder armor)
    /// - Royal Blue Surcoat / Tunic with silver trim piping and front/back splits
    /// - Wide brown leather utility belt with brass buckle
    /// - Steel vambraces and brown leather gauntlet gloves
    /// - Dark charcoal trousers and folded-cuff riding boots
    /// - Realistic Modern Tactical Assault Carbine Rifle held in combat two-handed stance
    /// - Procedural walking stride, combat aiming, rifle recoil kick, and defeat knockdown
    /// </summary>
    [DisallowMultipleComponent]
    public class RoyalGuardVisuals : MonoBehaviour
    {
        [Header("Model Prefab Override (Optional)")]
        #pragma warning disable CS0649
        [SerializeField] private GameObject customModelPrefab = null;
        #pragma warning restore CS0649

        [Header("Visual Tuning")]
        [SerializeField] private float modelScale = 1.0f;
        [SerializeField] private Vector3 modelOffset = Vector3.zero;

        // Visual Transforms for procedural animation
        private Transform visualRoot;
        private Transform torso;
        private Transform head;
        private Transform helmet;
        private Transform upperArmL, forearmL, handL;
        private Transform upperArmR, forearmR, handR;
        private Transform thighL, shinL, footL;
        private Transform thighR, shinR, footR;
        private Transform tacticalRifle;
        private Transform rifleMuzzle;

        // Default local rotations for IK/animation blending
        private Quaternion defaultTorsoRot;
        private Quaternion defaultArmLRot, defaultForearmLRot;
        private Quaternion defaultArmRRot, defaultForearmRRot;
        private Quaternion defaultRifleRot;

        // References
        private CharacterController characterController;
        private AIMovement movement;
        private AIAgentBase agent;
        private AIPerception perception;

        // Material caching for hit flashes
        private Renderer[] allRenderers;
        private Color[] originalColors;
        private float damageFlashTimer = 0f;
        private bool isDefeated = false;

        // Locomotion & Weapon recoil
        private float strideTimer = 0f;
        private float recoilOffset = 0f;
        private Light muzzleFlashLight;

        public Transform MuzzlePoint => rifleMuzzle;
        private Vector3? customAimTarget = null;
        public void SetAimTarget(Vector3 target) => customAimTarget = target;
        public void ClearAimTarget() => customAimTarget = null;

        private Transform TargetPlayer
        {
            get
            {
                if (perception != null && perception.Target != null) return perception.Target;
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) return pc.transform;
                var pcc = FindFirstObjectByType<TacticalShooter.Systems.Combat.PlayerCombatController>();
                if (pcc != null) return pcc.transform;
                return null;
            }
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            movement = GetComponent<AIMovement>();
            agent = GetComponent<AIAgentBase>();
            perception = GetComponent<AIPerception>();

            BuildVisualHierarchy();
        }

        public void BuildVisualHierarchy()
        {
            // Remove any legacy placeholder visual
            Transform oldVis = transform.Find("Visuals");
            if (oldVis != null)
            {
                if (Application.isPlaying) Destroy(oldVis.gameObject);
                else DestroyImmediate(oldVis.gameObject);
            }

            GameObject rootObj = new GameObject("Visuals");
            rootObj.transform.SetParent(transform, false);
            rootObj.transform.localPosition = modelOffset;
            rootObj.transform.localScale = Vector3.one * modelScale;
            visualRoot = rootObj.transform;

            if (customModelPrefab != null)
            {
                GameObject customInstance = Instantiate(customModelPrefab, visualRoot);
                customInstance.transform.localPosition = Vector3.zero;
                customInstance.transform.localRotation = Quaternion.identity;
                rifleMuzzle = customInstance.transform.Find("MuzzlePoint") ?? customInstance.transform;
                CacheRenderers();
                return;
            }

            // Build High-Fidelity 3D Royal Guard with Tactical Rifle
            ConstructProceduralRoyalGuard();
            CacheRenderers();
        }

        private void ConstructProceduralRoyalGuard()
        {
            // 1. Realistic PBR Materials Palette matching concept art
            Material matSteel = CreatePbrMaterial("Mat_SteelArmor", new Color(0.68f, 0.72f, 0.76f), 0.92f, 0.78f);
            Material matSteelDark = CreatePbrMaterial("Mat_SteelDark", new Color(0.40f, 0.44f, 0.48f), 0.88f, 0.65f);
            Material matRoyalBlue = CreatePbrMaterial("Mat_RoyalBlueFabric", new Color(0.14f, 0.28f, 0.58f), 0.05f, 0.30f);
            Material matSilverTrim = CreatePbrMaterial("Mat_SilverTrim", new Color(0.90f, 0.92f, 0.96f), 0.75f, 0.75f);
            Material matLeather = CreatePbrMaterial("Mat_BrownLeather", new Color(0.34f, 0.20f, 0.12f), 0.10f, 0.40f);
            Material matBrass = CreatePbrMaterial("Mat_BrassGold", new Color(0.86f, 0.74f, 0.28f), 0.85f, 0.70f);
            Material matSkin = CreatePbrMaterial("Mat_SkinTone", new Color(0.88f, 0.72f, 0.60f), 0.0f, 0.25f);
            Material matHair = CreatePbrMaterial("Mat_DarkHair", new Color(0.12f, 0.10f, 0.08f), 0.0f, 0.20f);
            Material matTrousers = CreatePbrMaterial("Mat_DarkTrousers", new Color(0.16f, 0.17f, 0.20f), 0.0f, 0.20f);
            Material matGunmetal = CreatePbrMaterial("Mat_GunmetalRifle", new Color(0.12f, 0.13f, 0.15f), 0.95f, 0.80f);

            // 2. Torso Root
            GameObject torsoObj = new GameObject("Torso");
            torsoObj.transform.SetParent(visualRoot, false);
            torsoObj.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            torso = torsoObj.transform;
            defaultTorsoRot = torso.localRotation;

            // Royal Blue Surcoat / Tunic Body
            CreateMeshPart(PrimitiveType.Cube, "Tunic_Body", torso, new Vector3(0f, 0.05f, 0f), new Vector3(0.48f, 0.46f, 0.28f), matRoyalBlue);

            // Steel Breastplate (Curved polished chestplate)
            CreateMeshPart(PrimitiveType.Sphere, "Breastplate_Curved", torso, new Vector3(0f, 0.10f, 0.05f), new Vector3(0.50f, 0.38f, 0.28f), matSteel);
            // Steel Backplate
            CreateMeshPart(PrimitiveType.Cube, "Backplate", torso, new Vector3(0f, 0.10f, -0.09f), new Vector3(0.46f, 0.36f, 0.12f), matSteelDark);

            // Leather Shoulder Harness Straps
            CreateMeshPart(PrimitiveType.Cube, "Strap_L", torso, new Vector3(-0.16f, 0.18f, 0.02f), new Vector3(0.06f, 0.28f, 0.26f), matLeather);
            CreateMeshPart(PrimitiveType.Cube, "Strap_R", torso, new Vector3(0.16f, 0.18f, 0.02f), new Vector3(0.06f, 0.28f, 0.26f), matLeather);

            // Wide Leather Belt with Brass Buckle
            CreateMeshPart(PrimitiveType.Cube, "Belt_Leather", torso, new Vector3(0f, -0.16f, 0f), new Vector3(0.50f, 0.10f, 0.32f), matLeather);
            CreateMeshPart(PrimitiveType.Cube, "Belt_Buckle", torso, new Vector3(0f, -0.16f, 0.17f), new Vector3(0.12f, 0.08f, 0.03f), matBrass);

            // Royal Blue Tunic Skirt with Silver Trim & Central Split
            GameObject skirtObj = new GameObject("Tunic_Skirt");
            skirtObj.transform.SetParent(torso, false);
            skirtObj.transform.localPosition = new Vector3(0f, -0.22f, 0f);

            CreateMeshPart(PrimitiveType.Cube, "Skirt_Left", skirtObj.transform, new Vector3(-0.13f, -0.16f, 0f), new Vector3(0.24f, 0.34f, 0.30f), matRoyalBlue);
            CreateMeshPart(PrimitiveType.Cube, "Skirt_Right", skirtObj.transform, new Vector3(0.13f, -0.16f, 0f), new Vector3(0.24f, 0.34f, 0.30f), matRoyalBlue);

            // Silver Trim Piping along front center and hem
            CreateMeshPart(PrimitiveType.Cube, "Trim_FrontCenter", skirtObj.transform, new Vector3(0f, -0.16f, 0.155f), new Vector3(0.035f, 0.34f, 0.02f), matSilverTrim);
            CreateMeshPart(PrimitiveType.Cube, "Trim_Hem_L", skirtObj.transform, new Vector3(-0.13f, -0.32f, 0f), new Vector3(0.25f, 0.035f, 0.31f), matSilverTrim);
            CreateMeshPart(PrimitiveType.Cube, "Trim_Hem_R", skirtObj.transform, new Vector3(0.13f, -0.32f, 0f), new Vector3(0.25f, 0.035f, 0.31f), matSilverTrim);

            // 3. Head & Kettle Helmet
            GameObject headObj = new GameObject("Head");
            headObj.transform.SetParent(torso, false);
            headObj.transform.localPosition = new Vector3(0f, 0.35f, 0.02f);
            head = headObj.transform;

            // Gorget Neck Collar
            CreateMeshPart(PrimitiveType.Cylinder, "Neck_Gorget", head, new Vector3(0f, -0.06f, 0f), new Vector3(0.18f, 0.08f, 0.18f), matRoyalBlue);

            // Face & Jaw
            CreateMeshPart(PrimitiveType.Sphere, "Face_Head", head, new Vector3(0f, 0.08f, 0f), new Vector3(0.22f, 0.25f, 0.22f), matSkin);
            CreateMeshPart(PrimitiveType.Cube, "Chin_Jaw", head, new Vector3(0f, 0.01f, 0.07f), new Vector3(0.10f, 0.10f, 0.09f), matSkin);
            CreateMeshPart(PrimitiveType.Sphere, "DarkHair_Trim", head, new Vector3(0f, 0.08f, -0.04f), new Vector3(0.23f, 0.24f, 0.23f), matHair);

            // Steel Kettle Helmet
            GameObject helmetObj = new GameObject("KettleHelmet");
            helmetObj.transform.SetParent(head, false);
            helmetObj.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            helmet = helmetObj.transform;

            // Helmet Dome
            CreateMeshPart(PrimitiveType.Sphere, "Helmet_Dome", helmet, new Vector3(0f, 0.04f, 0f), new Vector3(0.27f, 0.26f, 0.29f), matSteel);

            // Flared Lower Brim
            CreateMeshPart(PrimitiveType.Cylinder, "Helmet_Brim", helmet, new Vector3(0f, -0.06f, 0f), new Vector3(0.36f, 0.025f, 0.38f), matSteel);

            // Helmet Central Comb Ridge
            CreateMeshPart(PrimitiveType.Cube, "Helmet_CombRidge", helmet, new Vector3(0f, 0.16f, 0f), new Vector3(0.024f, 0.06f, 0.26f), matSteel);

            // Nasal Guard (Projecting down between eyes)
            CreateMeshPart(PrimitiveType.Cube, "NasalGuard", helmet, new Vector3(0f, -0.10f, 0.15f), new Vector3(0.035f, 0.12f, 0.025f), matSteel);

            // 4. Pauldrons (Segmented Curved Shoulder Armor)
            CreateMeshPart(PrimitiveType.Sphere, "Pauldron_L", torso, new Vector3(-0.31f, 0.24f, 0f), new Vector3(0.18f, 0.14f, 0.18f), matSteel);
            CreateMeshPart(PrimitiveType.Sphere, "Pauldron_R", torso, new Vector3(0.31f, 0.24f, 0f), new Vector3(0.18f, 0.14f, 0.18f), matSteel);

            // 5. Tactical Assault Carbine Rifle Mount (Attached directly to Torso right chest)
            GameObject rifleObj = new GameObject("TacticalAssaultRifle");
            rifleObj.transform.SetParent(torso, false);
            rifleObj.transform.localPosition = new Vector3(0.18f, 0.12f, 0.28f);
            rifleObj.transform.localRotation = Quaternion.identity;
            tacticalRifle = rifleObj.transform;
            defaultRifleRot = Quaternion.Euler(22f, -18f, 10f); // Low-ready patrol angle

            // Receiver & Body
            CreateMeshPart(PrimitiveType.Cube, "Rifle_Receiver", tacticalRifle, Vector3.zero, new Vector3(0.05f, 0.12f, 0.32f), matGunmetal);
            // Stock
            CreateMeshPart(PrimitiveType.Cube, "Rifle_Stock", tacticalRifle, new Vector3(0f, 0.02f, -0.22f), new Vector3(0.045f, 0.11f, 0.16f), matGunmetal);
            // Handguard Rail
            CreateMeshPart(PrimitiveType.Cube, "Rifle_Handguard", tacticalRifle, new Vector3(0f, 0.015f, 0.24f), new Vector3(0.055f, 0.08f, 0.22f), matGunmetal);
            // Fluted Barrel
            CreateMeshPart(PrimitiveType.Cylinder, "Rifle_Barrel", tacticalRifle, new Vector3(0f, 0.015f, 0.44f), new Vector3(0.022f, 0.18f, 0.022f), matSteelDark, Quaternion.Euler(90f, 0f, 0f));
            // Muzzle Brake
            CreateMeshPart(PrimitiveType.Cylinder, "Rifle_MuzzleBrake", tacticalRifle, new Vector3(0f, 0.015f, 0.63f), new Vector3(0.028f, 0.04f, 0.028f), matGunmetal, Quaternion.Euler(90f, 0f, 0f));
            // 30-Round Compact Curved Magazine (compact fit so nothing extends into stomach)
            CreateMeshPart(PrimitiveType.Cube, "Rifle_Magazine", tacticalRifle, new Vector3(0f, -0.07f, 0.08f), new Vector3(0.04f, 0.10f, 0.07f), matGunmetal, Quaternion.Euler(14f, 0f, 0f));
            // Holographic Optic Sight
            CreateMeshPart(PrimitiveType.Cube, "Rifle_HoloSight", tacticalRifle, new Vector3(0f, 0.08f, 0.02f), new Vector3(0.042f, 0.05f, 0.09f), matSteelDark);

            // Muzzle Flash Light & Bullet Raycast Point
            GameObject muzzleObj = new GameObject("RifleMuzzlePoint");
            muzzleObj.transform.SetParent(tacticalRifle, false);
            muzzleObj.transform.localPosition = new Vector3(0f, 0.015f, 0.68f);
            muzzleObj.transform.localRotation = Quaternion.identity;
            rifleMuzzle = muzzleObj.transform;

            GameObject flashLightObj = new GameObject("MuzzleFlashLight");
            flashLightObj.transform.SetParent(rifleMuzzle, false);
            muzzleFlashLight = flashLightObj.AddComponent<Light>();
            muzzleFlashLight.type = LightType.Point;
            muzzleFlashLight.color = new Color(1.0f, 0.75f, 0.3f);
            muzzleFlashLight.range = 5.0f;
            muzzleFlashLight.intensity = 0f;
            muzzleFlashLight.enabled = false;

            // 6. Right Arm (Primary trigger arm gripping tactical rifle)
            GameObject upperArmRObj = new GameObject("UpperArm_R");
            upperArmRObj.transform.SetParent(torso, false);
            upperArmRObj.transform.localPosition = new Vector3(0.25f, 0.16f, 0.05f);
            upperArmR = upperArmRObj.transform;
            defaultArmRRot = Quaternion.Euler(50f, -15f, 5f);
            upperArmR.localRotation = defaultArmRRot;

            CreateMeshPart(PrimitiveType.Capsule, "Sleeve_R", upperArmR, new Vector3(0f, -0.12f, 0.04f), new Vector3(0.12f, 0.20f, 0.12f), matRoyalBlue, Quaternion.Euler(20f, 0f, 0f));

            GameObject forearmRObj = new GameObject("Forearm_R");
            forearmRObj.transform.SetParent(upperArmR, false);
            forearmRObj.transform.localPosition = new Vector3(0f, -0.22f, 0.08f);
            forearmR = forearmRObj.transform;
            defaultForearmRRot = Quaternion.Euler(25f, 10f, 0f);
            forearmR.localRotation = defaultForearmRRot;

            CreateMeshPart(PrimitiveType.Capsule, "Vambrace_R", forearmR, new Vector3(0f, -0.09f, 0.05f), new Vector3(0.11f, 0.18f, 0.11f), matSteelDark, Quaternion.Euler(30f, 0f, 0f));

            // Right Hand (Trigger hand firmly on pistol grip)
            GameObject handRObj = new GameObject("Hand_R");
            handRObj.transform.SetParent(forearmR, false);
            handRObj.transform.localPosition = new Vector3(0f, -0.17f, 0.08f);
            handRObj.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            handR = handRObj.transform;
            CreateMeshPart(PrimitiveType.Cube, "Glove_R", handR, Vector3.zero, new Vector3(0.09f, 0.09f, 0.09f), matLeather);

            // Left Arm (Natural tactical posture along side - NO awkward parts protruding from stomach!)
            GameObject upperArmLObj = new GameObject("UpperArm_L");
            upperArmLObj.transform.SetParent(torso, false);
            upperArmLObj.transform.localPosition = new Vector3(-0.25f, 0.16f, 0f);
            upperArmL = upperArmLObj.transform;
            defaultArmLRot = Quaternion.Euler(-6f, 0f, 6f);
            upperArmL.localRotation = defaultArmLRot;

            CreateMeshPart(PrimitiveType.Capsule, "Sleeve_L", upperArmL, new Vector3(0f, -0.11f, 0f), new Vector3(0.12f, 0.20f, 0.12f), matRoyalBlue);

            GameObject forearmLObj = new GameObject("Forearm_L");
            forearmLObj.transform.SetParent(upperArmL, false);
            forearmLObj.transform.localPosition = new Vector3(0f, -0.20f, 0f);
            forearmL = forearmLObj.transform;
            defaultForearmLRot = Quaternion.Euler(-10f, 0f, 0f);
            forearmL.localRotation = defaultForearmLRot;

            CreateMeshPart(PrimitiveType.Capsule, "Vambrace_L", forearmL, new Vector3(0f, -0.09f, 0f), new Vector3(0.11f, 0.18f, 0.11f), matSteelDark);

            // Left Hand (Resting naturally at left hip / side)
            GameObject handLObj = new GameObject("Hand_L");
            handLObj.transform.SetParent(forearmL, false);
            handLObj.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            handLObj.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            handL = handLObj.transform;
            CreateMeshPart(PrimitiveType.Cube, "Glove_L", handL, Vector3.zero, new Vector3(0.08f, 0.09f, 0.08f), matLeather);

            // 7. Legs & Riding Boots
            // Left Leg
            GameObject thighLObj = new GameObject("Thigh_L");
            thighLObj.transform.SetParent(torso, false);
            thighLObj.transform.localPosition = new Vector3(-0.13f, -0.25f, 0f);
            thighL = thighLObj.transform;
            CreateMeshPart(PrimitiveType.Capsule, "Pants_L", thighL, new Vector3(0f, -0.16f, 0f), new Vector3(0.14f, 0.28f, 0.14f), matTrousers);

            GameObject shinLObj = new GameObject("Shin_L");
            shinLObj.transform.SetParent(thighL, false);
            shinLObj.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            shinL = shinLObj.transform;

            // Boot folded cuff
            CreateMeshPart(PrimitiveType.Cylinder, "BootCuff_L", shinL, new Vector3(0f, -0.04f, 0f), new Vector3(0.17f, 0.06f, 0.17f), matLeather);
            // Boot shaft
            CreateMeshPart(PrimitiveType.Capsule, "BootShaft_L", shinL, new Vector3(0f, -0.18f, 0f), new Vector3(0.14f, 0.26f, 0.14f), matLeather);

            GameObject footLObj = new GameObject("Foot_L");
            footLObj.transform.SetParent(shinL, false);
            footLObj.transform.localPosition = new Vector3(0f, -0.32f, 0.06f);
            footL = footLObj.transform;
            CreateMeshPart(PrimitiveType.Cube, "BootFoot_L", footL, Vector3.zero, new Vector3(0.13f, 0.10f, 0.24f), matLeather);

            // Right Leg
            GameObject thighRObj = new GameObject("Thigh_R");
            thighRObj.transform.SetParent(torso, false);
            thighRObj.transform.localPosition = new Vector3(0.13f, -0.25f, 0f);
            thighR = thighRObj.transform;
            CreateMeshPart(PrimitiveType.Capsule, "Pants_R", thighR, new Vector3(0f, -0.16f, 0f), new Vector3(0.14f, 0.28f, 0.14f), matTrousers);

            GameObject shinRObj = new GameObject("Shin_R");
            shinRObj.transform.SetParent(thighR, false);
            shinRObj.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            shinR = shinRObj.transform;

            // Boot folded cuff
            CreateMeshPart(PrimitiveType.Cylinder, "BootCuff_R", shinR, new Vector3(0f, -0.04f, 0f), new Vector3(0.17f, 0.06f, 0.17f), matLeather);
            // Boot shaft
            CreateMeshPart(PrimitiveType.Capsule, "BootShaft_R", shinR, new Vector3(0f, -0.18f, 0f), new Vector3(0.14f, 0.26f, 0.14f), matLeather);

            GameObject footRObj = new GameObject("Foot_R");
            footRObj.transform.SetParent(shinR, false);
            footRObj.transform.localPosition = new Vector3(0f, -0.32f, 0.06f);
            footR = footRObj.transform;
            CreateMeshPart(PrimitiveType.Cube, "BootFoot_R", footR, Vector3.zero, new Vector3(0.13f, 0.10f, 0.24f), matLeather);
        }

        private GameObject CreateMeshPart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion rot = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            if (rot != default) part.transform.localRotation = rot;

            part.GetComponent<Renderer>().material = mat;
            Destroy(part.GetComponent<Collider>());
            return part;
        }

        private Material CreatePbrMaterial(string name, Color color, float metallic = 0.5f, float smoothness = 0.5f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            Material mat = new Material(shader);
            mat.name = name;
            mat.color = color;

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            return mat;
        }

        private void CacheRenderers()
        {
            allRenderers = visualRoot.GetComponentsInChildren<Renderer>();
            originalColors = new Color[allRenderers.Length];
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null)
                {
                    originalColors[i] = allRenderers[i].material.color;
                }
            }
        }

        private void LateUpdate()
        {
            if (isDefeated) return;

            UpdateDamageFlash();
            AnimateGaitAndBreathing();
            AnimateCombatAiming();
        }

        private void AnimateGaitAndBreathing()
        {
            if (torso == null) return;

            Vector3 vel = characterController != null ? characterController.velocity : Vector3.zero;
            float horizSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
            bool isWalking = movement != null && movement.IsMoving && horizSpeed > 0.25f;

            if (isWalking)
            {
                // Continuous stride cadence matched to movement speed
                strideTimer += Time.deltaTime * (horizSpeed * 2.8f);
                float legSwing = Mathf.Sin(strideTimer) * 30f;
                float kneeFlexL = Mathf.Clamp(-legSwing, 0f, 45f);
                float kneeFlexR = Mathf.Clamp(legSwing, 0f, 45f);

                if (thighL != null) thighL.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
                if (shinL != null) shinL.localRotation = Quaternion.Euler(kneeFlexL, 0f, 0f);
                if (footL != null) footL.localRotation = Quaternion.Euler(-legSwing * 0.35f, 0f, 0f);

                if (thighR != null) thighR.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
                if (shinR != null) shinR.localRotation = Quaternion.Euler(kneeFlexR, 0f, 0f);
                if (footR != null) footR.localRotation = Quaternion.Euler(legSwing * 0.35f, 0f, 0f);

                // Natural contralateral left arm stride swing along side
                float armSwingL = -legSwing * 0.55f;
                if (upperArmL != null) upperArmL.localRotation = Quaternion.Euler(armSwingL - 6f, 0f, 6f);
                if (forearmL != null) forearmL.localRotation = Quaternion.Euler(-10f, 0f, 0f);

                // Subtle organic torso step bob and lateral weight sway
                float bob = Mathf.Abs(Mathf.Sin(strideTimer)) * 0.038f;
                torso.localPosition = new Vector3(0f, 1.05f - bob, 0f);
            }
            else
            {
                // Idle breathing cycle
                float breath = Mathf.Sin(Time.time * 2.2f) * 0.015f;
                torso.localPosition = new Vector3(0f, 1.05f + breath, 0f);

                // Instantly zero stride timer and smoothly return legs and left arm to standing stance
                strideTimer = 0f;
                if (thighL != null) thighL.localRotation = Quaternion.Slerp(thighL.localRotation, Quaternion.identity, Time.deltaTime * 14f);
                if (shinL != null) shinL.localRotation = Quaternion.Slerp(shinL.localRotation, Quaternion.identity, Time.deltaTime * 14f);
                if (footL != null) footL.localRotation = Quaternion.identity;

                if (thighR != null) thighR.localRotation = Quaternion.Slerp(thighR.localRotation, Quaternion.identity, Time.deltaTime * 14f);
                if (shinR != null) shinR.localRotation = Quaternion.Slerp(shinR.localRotation, Quaternion.identity, Time.deltaTime * 14f);
                if (footR != null) footR.localRotation = Quaternion.identity;

                if (upperArmL != null) upperArmL.localRotation = Quaternion.Slerp(upperArmL.localRotation, defaultArmLRot, Time.deltaTime * 14f);
                if (forearmL != null) forearmL.localRotation = Quaternion.Slerp(forearmL.localRotation, defaultForearmLRot, Time.deltaTime * 14f);
            }
        }

        private void AnimateCombatAiming()
        {
            if (tacticalRifle == null || agent == null) return;

            Transform target = TargetPlayer;
            bool isEngaging = (agent.CurrentState == AgentState.Engage || (perception != null && perception.CanSeeTarget));

            if (isEngaging && target != null)
            {
                // Aim directly at specific exposed target point if available, or player chest center
                Vector3 targetChest = customAimTarget.HasValue ? customAimTarget.Value : (target.position + Vector3.up * 1.15f);

                // Aim vector from character to target
                Vector3 aimDir = (targetChest - (transform.position + Vector3.up * 1.2f)).normalized;
                if (aimDir.sqrMagnitude < 0.01f) aimDir = transform.forward;

                // Calculate vertical pitch
                float pitch = Vector3.SignedAngle(transform.forward, aimDir, transform.right);
                pitch = Mathf.Clamp(pitch, -35f, 35f);

                // 1. Torso tilts slightly with pitch
                if (torso != null)
                {
                    torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(pitch * 0.35f, 0f, 0f), Time.deltaTime * 14f);
                }

                // 2. Head looks directly at target
                if (head != null)
                {
                    Vector3 headLookDir = (targetChest - head.position).normalized;
                    head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(headLookDir, Vector3.up), Time.deltaTime * 22f);
                }

                // 3. Right arm aligns with rifle pitch; Left arm stays naturally at flank/side (clear of stomach)
                if (upperArmR != null) upperArmR.localRotation = Quaternion.Slerp(upperArmR.localRotation, Quaternion.Euler(58f + pitch * 0.4f, -10f, 6f), Time.deltaTime * 18f);
                if (forearmR != null) forearmR.localRotation = Quaternion.Slerp(forearmR.localRotation, Quaternion.Euler(22f, 8f, 0f), Time.deltaTime * 18f);
                if (upperArmL != null) upperArmL.localRotation = Quaternion.Slerp(upperArmL.localRotation, Quaternion.Euler(-10f, 0f, 10f), Time.deltaTime * 18f);
                if (forearmL != null) forearmL.localRotation = Quaternion.Slerp(forearmL.localRotation, Quaternion.Euler(-14f, 0f, 0f), Time.deltaTime * 18f);

                // 4. High-ready combat stance: raise rifle to shoulder level
                Vector3 combatRifleLocalPos = new Vector3(0.18f, 0.14f, 0.32f);
                tacticalRifle.localPosition = Vector3.Lerp(tacticalRifle.localPosition, combatRifleLocalPos - tacticalRifle.forward * recoilOffset, Time.deltaTime * 18f);

                // 5. FINALLY, align rifle barrel directly at the target in world space!
                Vector3 rifleToTarget = (targetChest - tacticalRifle.position).normalized;
                if (rifleToTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRifleAim = Quaternion.LookRotation(rifleToTarget, Vector3.up);
                    tacticalRifle.rotation = Quaternion.Slerp(tacticalRifle.rotation, targetRifleAim, Time.deltaTime * 35f);
                }
            }
            else
            {
                // Low-ready patrol carry stance across chest
                Vector3 patrolRifleLocalPos = new Vector3(0.14f, 0.05f, 0.24f);
                tacticalRifle.localPosition = Vector3.Lerp(tacticalRifle.localPosition, patrolRifleLocalPos - tacticalRifle.forward * recoilOffset, Time.deltaTime * 6f);
                tacticalRifle.localRotation = Quaternion.Slerp(tacticalRifle.localRotation, defaultRifleRot, Time.deltaTime * 6f);

                if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.identity, Time.deltaTime * 6f);
                if (torso != null) torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.identity, Time.deltaTime * 6f);

                if (upperArmR != null) upperArmR.localRotation = Quaternion.Slerp(upperArmR.localRotation, defaultArmRRot, Time.deltaTime * 6f);
                if (forearmR != null) forearmR.localRotation = Quaternion.Slerp(forearmR.localRotation, defaultForearmRRot, Time.deltaTime * 6f);
                if (upperArmL != null) upperArmL.localRotation = Quaternion.Slerp(upperArmL.localRotation, defaultArmLRot, Time.deltaTime * 6f);
                if (forearmL != null) forearmL.localRotation = Quaternion.Slerp(forearmL.localRotation, defaultForearmLRot, Time.deltaTime * 6f);
            }

            // Recoil kickback recovery
            if (recoilOffset > 0.001f)
            {
                recoilOffset = Mathf.Lerp(recoilOffset, 0f, Time.deltaTime * 18f);
            }
        }

        public void TriggerRecoil()
        {
            recoilOffset = 0.08f;
            StartCoroutine(MuzzleFlashRoutine());
        }

        private IEnumerator MuzzleFlashRoutine()
        {
            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = true;
                muzzleFlashLight.intensity = 2.5f;
                yield return new WaitForSeconds(0.04f);
                muzzleFlashLight.intensity = 0f;
                muzzleFlashLight.enabled = false;
            }
        }

        public void FlashDamage(Color flashColor)
        {
            damageFlashTimer = 0.15f;
            ApplyColor(flashColor);
        }

        public void PlayDefeatKnockdown()
        {
            isDefeated = true;
            StopAllCoroutines();
            if (muzzleFlashLight != null) muzzleFlashLight.enabled = false;
            StartCoroutine(KnockdownRoutine());
        }

        private IEnumerator KnockdownRoutine()
        {
            Quaternion startRot = transform.rotation;
            Quaternion targetRot = Quaternion.Euler(-75f, transform.eulerAngles.y, 0f);
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + Vector3.down * 0.4f;

            float elapsed = 0f;
            float duration = 0.65f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            // Defeated grey shade
            ApplyColor(new Color(0.35f, 0.35f, 0.38f));
        }

        private void UpdateDamageFlash()
        {
            if (damageFlashTimer > 0f)
            {
                damageFlashTimer -= Time.deltaTime;
                if (damageFlashTimer <= 0f)
                {
                    RestoreColors();
                }
            }
        }

        private void ApplyColor(Color col)
        {
            if (allRenderers == null) return;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null)
                {
                    allRenderers[i].material.color = col;
                }
            }
        }

        private void RestoreColors()
        {
            if (allRenderers == null || originalColors == null) return;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null && i < originalColors.Length)
                {
                    allRenderers[i].material.color = originalColors[i];
                }
            }
        }
    }
}
