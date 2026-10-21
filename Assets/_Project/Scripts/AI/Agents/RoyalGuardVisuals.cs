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
        private AgentRole? currentConstructedRole = null;

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

        public AgentRole ResolveAgentRole()
        {
            string n = gameObject.name.ToLowerInvariant();
            if (n.Contains("boss") || GetComponent<SupportAgent>() != null) return AgentRole.Support;
            if (n.Contains("hunter") || GetComponent<HunterAgent>() != null) return AgentRole.Hunter;
            if (n.Contains("flanker") || GetComponent<FlankerAgent>() != null) return AgentRole.Flanker;
            if (n.Contains("guard") || n.Contains("soldier") || GetComponent<TacticalSoldierAgent>() != null) return AgentRole.Guard;
            if (agent != null && agent.Role != AgentRole.Guard) return agent.Role;
            return AgentRole.Guard;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            movement = GetComponent<AIMovement>();
            agent = GetComponent<AIAgentBase>();
            perception = GetComponent<AIPerception>();

            CleanOldVisuals();
            BuildVisualHierarchy();
        }

        private void Start()
        {
            if (visualRoot == null || currentConstructedRole != ResolveAgentRole())
            {
                CleanOldVisuals();
                BuildVisualHierarchy();
            }
        }

        public void CleanOldVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "Visuals" || child.name.StartsWith("Mat_") || child.name.Contains("Visual") || child.name.Contains("Mesh"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        public void BuildVisualHierarchy()
        {
            if (agent == null) agent = GetComponent<AIAgentBase>();
            AgentRole role = ResolveAgentRole();
            currentConstructedRole = role;

            CleanOldVisuals();

            // Distinct scale profile per archetype
            float effectiveScale = modelScale;
            if (role == AgentRole.Support) effectiveScale *= 1.30f; // Heavy Juggernaut Boss (Towering 2.30m)
            else if (role == AgentRole.Hunter) effectiveScale *= 1.05f; // Tall agile stalker (1.90m)
            else if (role == AgentRole.Flanker) effectiveScale *= 0.96f; // Sleek low-profile infiltrator (1.75m)
            else effectiveScale *= 1.00f; // Standard Sentinel Guard (1.85m)

            GameObject rootObj = new GameObject("Visuals");
            rootObj.transform.SetParent(transform, false);
            rootObj.transform.localPosition = modelOffset;
            rootObj.transform.localScale = Vector3.one * effectiveScale;
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

            // Build High-Fidelity 3D Tactical Operator Model matching Archetype
            ConstructProceduralSoldier(role);
            CacheRenderers();
        }

        private void ConstructProceduralRoyalGuard()
        {
            ConstructProceduralSoldier(ResolveAgentRole());
        }

        private void ConstructProceduralRoyalGuard(AgentRole role)
        {
            ConstructProceduralSoldier(role);
        }

        private void ConstructProceduralSoldier(AgentRole role)
        {
            // 1. High-Fidelity Materials Palette matching realistic concept art
            Color fabricColor;
            Color armorColor;
            Color armorDarkColor;
            Color trimColor;
            Color leatherColor;
            Color visorColor;
            float metallic = 0.85f;
            float smoothness = 0.75f;
            Vector3 pauldronScale;

            switch (role)
            {
                case AgentRole.Support: // Boss: Ironclad Juggernaut in Obsidian Iron & Blood Red with Imperial Gold
                    fabricColor = new Color(0.42f, 0.05f, 0.07f);       // Blood-Red Ballistic Carbon Weave
                    armorColor = new Color(0.12f, 0.12f, 0.15f);        // Scorched Obsidian Iron Plate
                    armorDarkColor = new Color(0.06f, 0.06f, 0.08f);    // Deep Carbon Steel
                    trimColor = new Color(0.95f, 0.78f, 0.20f);         // Imperial Gilded Gold Accents
                    leatherColor = new Color(0.12f, 0.12f, 0.14f);      // Heavy Ballistic Webbing
                    visorColor = new Color(1.0f, 0.05f, 0.05f) * 6.5f;  // Bright Glowing Crimson Visor
                    metallic = 0.95f;
                    smoothness = 0.80f;
                    pauldronScale = new Vector3(0.38f, 0.28f, 0.38f);   // Massive Tiered Pauldrons
                    break;

                case AgentRole.Hunter: // Hunter: Apex Stalker in Charcoal Carbon & Hazard Tiger Orange
                    fabricColor = new Color(0.96f, 0.42f, 0.04f);       // High-Visibility Hazard Tiger Orange
                    armorColor = new Color(0.20f, 0.22f, 0.25f);        // Titanium Dark Charcoal
                    armorDarkColor = new Color(0.12f, 0.13f, 0.15f);    // Matte Carbon Fiber
                    trimColor = new Color(1.0f, 0.68f, 0.08f) * 4.0f;   // Neon Amber Hazard Trim
                    leatherColor = new Color(0.25f, 0.16f, 0.10f);      // Rugged Brown Leather Straps
                    visorColor = new Color(1.0f, 0.60f, 0.05f) * 6.5f;  // Bright Glowing Amber Visor
                    metallic = 0.75f;
                    smoothness = 0.65f;
                    pauldronScale = new Vector3(0.18f, 0.14f, 0.18f);   // Agile Athletic Pauldrons
                    break;

                case AgentRole.Flanker: // Flanker: Ghost Marksman in Matte Carbon Black & Electric Cyan
                    fabricColor = new Color(0.04f, 0.07f, 0.12f);       // Stealth Shadow Midnight Navy
                    armorColor = new Color(0.08f, 0.09f, 0.10f);        // Non-reflective Matte Carbon Black
                    armorDarkColor = new Color(0.05f, 0.05f, 0.06f);    // Ultra-black Radar Absorbent
                    trimColor = new Color(0.12f, 0.88f, 1.0f) * 4.5f;   // Electric Cyan Sensor Trim
                    leatherColor = new Color(0.08f, 0.08f, 0.08f);      // Matte Black Harness
                    visorColor = new Color(0.10f, 0.90f, 1.0f) * 6.5f;  // Electric Cyan Optics
                    metallic = 0.25f;
                    smoothness = 0.25f;                                  // Matte Non-reflective Stealth
                    pauldronScale = new Vector3(0.14f, 0.11f, 0.14f);   // Sleek Low-profile Pauldrons
                    break;

                case AgentRole.Guard: // Guard: Frontline Sentinel in Slate Steel & Royal Navy
                default:
                    fabricColor = new Color(0.10f, 0.18f, 0.32f);       // Tactical Royal Navy
                    armorColor = new Color(0.48f, 0.52f, 0.58f);        // Ceramic Slate Plate
                    armorDarkColor = new Color(0.28f, 0.32f, 0.38f);    // Steel Grey
                    trimColor = new Color(0.85f, 0.88f, 0.92f);         // Silver Squad Piping
                    leatherColor = new Color(0.20f, 0.14f, 0.10f);      // Tactical Utility Leather
                    visorColor = new Color(0.15f, 0.95f, 0.35f) * 5.0f; // Glowing Panoramic NVG Green
                    metallic = 0.80f;
                    smoothness = 0.60f;
                    pauldronScale = new Vector3(0.22f, 0.16f, 0.22f);   // Standard Infantry Armor
                    break;
            }

            Material matArmor = CreatePbrMaterial($"Mat_Armor_{role}", armorColor, metallic, smoothness);
            Material matArmorDark = CreatePbrMaterial($"Mat_ArmorDark_{role}", armorDarkColor, metallic * 0.9f, smoothness * 0.8f);
            Material matFabric = CreatePbrMaterial($"Mat_Fabric_{role}", fabricColor, 0.05f, 0.35f);
            Material matTrim = CreatePbrMaterial($"Mat_Trim_{role}", trimColor, 0.90f, 0.80f);
            Material matLeather = CreatePbrMaterial($"Mat_Leather_{role}", leatherColor, 0.10f, 0.40f);
            Material matVisor = CreatePbrMaterial($"Mat_Visor_{role}", visorColor * 0.2f, 0.1f, 0.95f, visorColor);
            Material matGunmetal = CreatePbrMaterial("Mat_GunmetalRifle", new Color(0.10f, 0.11f, 0.13f), 0.95f, 0.80f);
            Material matTrousers = (role == AgentRole.Hunter) ? matFabric : CreatePbrMaterial("Mat_DarkTrousers", new Color(0.14f, 0.15f, 0.18f), 0.05f, 0.25f);

            // 2. Torso Root (Modern Tactical Plate Carrier)
            GameObject torsoObj = new GameObject("Torso");
            torsoObj.transform.SetParent(visualRoot, false);
            torsoObj.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            torso = torsoObj.transform;
            defaultTorsoRot = torso.localRotation;

            // Combat Under-suit Core
            CreateMeshPart(PrimitiveType.Cube, "Tactical_Undersuit", torso, new Vector3(0f, 0.05f, 0f), new Vector3(0.48f, 0.46f, 0.28f), matFabric);

            // Front Modular Ballistic Plate Carrier
            CreateMeshPart(PrimitiveType.Cube, "Chest_BallisticPlate", torso, new Vector3(0f, 0.08f, 0.10f), new Vector3(0.46f, 0.38f, 0.14f), matArmor);
            // Rear Backplate Carrier
            CreateMeshPart(PrimitiveType.Cube, "Back_BallisticPlate", torso, new Vector3(0f, 0.08f, -0.10f), new Vector3(0.44f, 0.38f, 0.12f), matArmorDark);

            // Heavy Ballistic Shoulder Straps
            CreateMeshPart(PrimitiveType.Cube, "VestStrap_L", torso, new Vector3(-0.16f, 0.20f, 0f), new Vector3(0.08f, 0.20f, 0.28f), matLeather);
            CreateMeshPart(PrimitiveType.Cube, "VestStrap_R", torso, new Vector3(0.16f, 0.20f, 0f), new Vector3(0.08f, 0.20f, 0.28f), matLeather);

            // Tactical Modular Duty Belt & Groin Armor Plate
            CreateMeshPart(PrimitiveType.Cube, "DutyBelt", torso, new Vector3(0f, -0.17f, 0f), new Vector3(0.50f, 0.09f, 0.32f), matLeather);
            CreateMeshPart(PrimitiveType.Cube, "BeltBuckle", torso, new Vector3(0f, -0.17f, 0.165f), new Vector3(0.10f, 0.07f, 0.025f), matTrim);
            CreateMeshPart(PrimitiveType.Cube, "Pelvic_BlastPlate", torso, new Vector3(0f, -0.27f, 0.11f), new Vector3(0.24f, 0.16f, 0.06f), matArmorDark);
            CreateMeshPart(PrimitiveType.Cube, "HipArmor_L", torso, new Vector3(-0.24f, -0.22f, 0f), new Vector3(0.06f, 0.14f, 0.22f), matArmorDark);
            CreateMeshPart(PrimitiveType.Cube, "HipArmor_R", torso, new Vector3(0.24f, -0.22f, 0f), new Vector3(0.06f, 0.14f, 0.22f), matArmorDark);

            // Archetype-Specific Torso Attachments
            if (role == AgentRole.Support)
            {
                // Boss Heavy Exoskeleton Armature on Spine & Blast Collar
                CreateMeshPart(PrimitiveType.Cylinder, "Boss_BlastCollar", torso, new Vector3(0f, 0.28f, 0.02f), new Vector3(0.38f, 0.10f, 0.38f), matArmorDark);
                CreateMeshPart(PrimitiveType.Cube, "Boss_ChestSlab_L", torso, new Vector3(-0.11f, 0.08f, 0.18f), new Vector3(0.20f, 0.32f, 0.06f), matArmor);
                CreateMeshPart(PrimitiveType.Cube, "Boss_ChestSlab_R", torso, new Vector3(0.11f, 0.08f, 0.18f), new Vector3(0.20f, 0.32f, 0.06f), matArmor);
                CreateMeshPart(PrimitiveType.Cube, "Boss_ChestBevel", torso, new Vector3(0f, 0.08f, 0.195f), new Vector3(0.04f, 0.30f, 0.03f), matTrim);
                // Exoskeleton Spine Hydraulic Pistons on Back
                CreateMeshPart(PrimitiveType.Cylinder, "Boss_SpineExo", torso, new Vector3(0f, 0.08f, -0.17f), new Vector3(0.08f, 0.36f, 0.08f), matTrim);
                CreateMeshPart(PrimitiveType.Cylinder, "Boss_Piston_L", torso, new Vector3(-0.10f, 0.08f, -0.16f), new Vector3(0.035f, 0.32f, 0.035f), matTrim);
                CreateMeshPart(PrimitiveType.Cylinder, "Boss_Piston_R", torso, new Vector3(0.10f, 0.08f, -0.16f), new Vector3(0.035f, 0.32f, 0.035f), matTrim);
            }
            else if (role == AgentRole.Hunter)
            {
                // Dual Crossed Combat Karambit Blades on Chest Harness
                CreateMeshPart(PrimitiveType.Cube, "Blade_L", torso, new Vector3(-0.12f, 0.14f, 0.18f), new Vector3(0.025f, 0.16f, 0.035f), matTrim, Quaternion.Euler(0f, 0f, -38f));
                CreateMeshPart(PrimitiveType.Cube, "Blade_R", torso, new Vector3(0.12f, 0.14f, 0.18f), new Vector3(0.025f, 0.16f, 0.035f), matTrim, Quaternion.Euler(0f, 0f, 38f));
                CreateMeshPart(PrimitiveType.Cube, "Hunter_Strap_Cross", torso, new Vector3(0f, 0.10f, 0.175f), new Vector3(0.32f, 0.04f, 0.02f), matLeather);
            }
            else if (role == AgentRole.Flanker)
            {
                // Sleek Angular Low-Profile Stealth Harness + Cyan Sensor Node
                CreateMeshPart(PrimitiveType.Cube, "StealthSensorNode", torso, new Vector3(0f, 0.12f, 0.175f), new Vector3(0.08f, 0.08f, 0.03f), matTrim);
                CreateMeshPart(PrimitiveType.Sphere, "SensorLens", torso, new Vector3(0f, 0.12f, 0.195f), new Vector3(0.04f, 0.04f, 0.04f), matVisor);
            }
            else // Guard
            {
                // Triple STANAG Magazine Pouches & Shoulder Radio Antenna
                CreateMeshPart(PrimitiveType.Cube, "MagPouch_1", torso, new Vector3(-0.11f, -0.04f, 0.18f), new Vector3(0.07f, 0.12f, 0.05f), matLeather);
                CreateMeshPart(PrimitiveType.Cube, "MagPouch_2", torso, new Vector3(0f, -0.04f, 0.185f), new Vector3(0.07f, 0.12f, 0.05f), matLeather);
                CreateMeshPart(PrimitiveType.Cube, "MagPouch_3", torso, new Vector3(0.11f, -0.04f, 0.18f), new Vector3(0.07f, 0.12f, 0.05f), matLeather);
                CreateMeshPart(PrimitiveType.Cube, "Radio_Body", torso, new Vector3(-0.18f, 0.16f, -0.15f), new Vector3(0.06f, 0.12f, 0.06f), matArmorDark);
                CreateMeshPart(PrimitiveType.Cylinder, "Radio_Antenna", torso, new Vector3(-0.18f, 0.32f, -0.15f), new Vector3(0.008f, 0.22f, 0.008f), matArmorDark);
            }

            // 3. Head & Ballistic Combat Helmet
            GameObject headObj = new GameObject("Head");
            headObj.transform.SetParent(torso, false);
            headObj.transform.localPosition = new Vector3(0f, 0.35f, 0.02f);
            head = headObj.transform;

            // Ballistic Neck Gorget
            CreateMeshPart(PrimitiveType.Cylinder, "Neck_Gorget", head, new Vector3(0f, -0.06f, 0f), new Vector3(0.20f, 0.10f, 0.20f), matFabric);

            // Ballistic Helmet & Mask Structure
            GameObject helmetObj = new GameObject("BallisticHelmet");
            helmetObj.transform.SetParent(head, false);
            helmetObj.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            helmet = helmetObj.transform;

            // Helmet Main Shell
            CreateMeshPart(PrimitiveType.Sphere, "Helmet_Shell", helmet, new Vector3(0f, 0.02f, 0f), new Vector3(0.28f, 0.27f, 0.30f), matArmor);
            CreateMeshPart(PrimitiveType.Cube, "Helmet_BrowPlate", helmet, new Vector3(0f, 0.06f, 0.11f), new Vector3(0.26f, 0.08f, 0.10f), matArmorDark);

            // Archetype Helmet Optics & Visors with Point Light
            GameObject glowLightObj = new GameObject("VisorGlowLight");
            glowLightObj.transform.SetParent(helmet, false);
            Light glowLight = glowLightObj.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.range = 3.5f;

            if (role == AgentRole.Support)
            {
                // Juggernaut Enclosed Face Shield & Glowing Crimson Visor Slit
                CreateMeshPart(PrimitiveType.Cube, "FaceShield", helmet, new Vector3(0f, -0.06f, 0.10f), new Vector3(0.26f, 0.18f, 0.12f), matArmorDark);
                CreateMeshPart(PrimitiveType.Cube, "Boss_CrimsonVisor", helmet, new Vector3(0f, 0.01f, 0.165f), new Vector3(0.22f, 0.035f, 0.03f), matVisor);
                CreateMeshPart(PrimitiveType.Cube, "HeavyRebreather", helmet, new Vector3(0f, -0.11f, 0.14f), new Vector3(0.12f, 0.08f, 0.08f), matTrim);
                glowLight.color = Color.red;
                glowLight.intensity = 1.8f;
                glowLightObj.transform.localPosition = new Vector3(0f, 0.01f, 0.20f);
            }
            else if (role == AgentRole.Hunter)
            {
                // Predator Combat Mask with Glowing Amber Visor & Dual Gas Filters
                CreateMeshPart(PrimitiveType.Cube, "FaceMask", helmet, new Vector3(0f, -0.06f, 0.09f), new Vector3(0.24f, 0.16f, 0.11f), matArmorDark);
                CreateMeshPart(PrimitiveType.Cube, "Hunter_AmberVisor", helmet, new Vector3(0f, 0.01f, 0.155f), new Vector3(0.20f, 0.045f, 0.03f), matVisor);
                CreateMeshPart(PrimitiveType.Cylinder, "Respirator_L", helmet, new Vector3(-0.08f, -0.10f, 0.13f), new Vector3(0.045f, 0.04f, 0.045f), matTrim, Quaternion.Euler(45f, 0f, 0f));
                CreateMeshPart(PrimitiveType.Cylinder, "Respirator_R", helmet, new Vector3(0.08f, -0.10f, 0.13f), new Vector3(0.045f, 0.04f, 0.045f), matTrim, Quaternion.Euler(45f, 0f, 0f));
                glowLight.color = new Color(1.0f, 0.6f, 0.1f);
                glowLight.intensity = 1.8f;
                glowLightObj.transform.localPosition = new Vector3(0f, 0.01f, 0.19f);
            }
            else if (role == AgentRole.Flanker)
            {
                // Recon Helmet with Tri-Lens Electric Cyan Sensor Optics
                CreateMeshPart(PrimitiveType.Cube, "ReconFacePlate", helmet, new Vector3(0f, -0.06f, 0.09f), new Vector3(0.22f, 0.15f, 0.10f), matArmorDark);
                CreateMeshPart(PrimitiveType.Cylinder, "CyanOptic_Center", helmet, new Vector3(0f, 0.02f, 0.16f), new Vector3(0.04f, 0.04f, 0.04f), matVisor, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPart(PrimitiveType.Cylinder, "CyanOptic_L", helmet, new Vector3(-0.065f, 0.03f, 0.15f), new Vector3(0.03f, 0.035f, 0.03f), matVisor, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPart(PrimitiveType.Cylinder, "CyanOptic_R", helmet, new Vector3(0.065f, 0.03f, 0.15f), new Vector3(0.03f, 0.035f, 0.03f), matVisor, Quaternion.Euler(90f, 0f, 0f));
                glowLight.color = new Color(0.1f, 0.85f, 1.0f);
                glowLight.intensity = 1.8f;
                glowLightObj.transform.localPosition = new Vector3(0f, 0.02f, 0.20f);
            }
            else // Guard
            {
                // Modern High-Cut Helmet with Quad Panoramic Night-Vision Optics (NVG Green)
                CreateMeshPart(PrimitiveType.Sphere, "Face_Head", head, new Vector3(0f, 0.04f, 0.02f), new Vector3(0.22f, 0.24f, 0.22f), matArmorDark);
                CreateMeshPart(PrimitiveType.Cube, "NVG_Mount", helmet, new Vector3(0f, 0.08f, 0.15f), new Vector3(0.06f, 0.06f, 0.03f), matArmorDark);
                CreateMeshPart(PrimitiveType.Cylinder, "NVG_1", helmet, new Vector3(-0.075f, 0.06f, 0.18f), new Vector3(0.025f, 0.05f, 0.025f), matVisor, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPart(PrimitiveType.Cylinder, "NVG_2", helmet, new Vector3(-0.025f, 0.06f, 0.19f), new Vector3(0.025f, 0.05f, 0.025f), matVisor, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPart(PrimitiveType.Cylinder, "NVG_3", helmet, new Vector3(0.025f, 0.06f, 0.19f), new Vector3(0.025f, 0.05f, 0.025f), matVisor, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPart(PrimitiveType.Cylinder, "NVG_4", helmet, new Vector3(0.075f, 0.06f, 0.18f), new Vector3(0.025f, 0.05f, 0.025f), matVisor, Quaternion.Euler(90f, 0f, 0f));
                glowLight.color = new Color(0.2f, 0.95f, 0.35f);
                glowLight.intensity = 1.5f;
                glowLightObj.transform.localPosition = new Vector3(0f, 0.06f, 0.22f);
            }

            // 4. Armored Pauldrons (Segmented Shoulder Armor)
            CreateMeshPart(PrimitiveType.Cube, "Pauldron_L", torso, new Vector3(-0.31f, 0.22f, 0f), pauldronScale, matArmor);
            CreateMeshPart(PrimitiveType.Cube, "Pauldron_R", torso, new Vector3(0.31f, 0.22f, 0f), pauldronScale, matArmor);

            // 5. Tactical Weapon Mount (Attached to Torso right chest)
            GameObject rifleObj = new GameObject("TacticalWeapon");
            rifleObj.transform.SetParent(torso, false);
            rifleObj.transform.localPosition = new Vector3(0.18f, 0.12f, 0.28f);
            rifleObj.transform.localRotation = Quaternion.identity;
            tacticalRifle = rifleObj.transform;
            defaultRifleRot = Quaternion.Euler(22f, -18f, 10f); // Low-ready patrol angle

            // Receiver & Body
            CreateMeshPart(PrimitiveType.Cube, "Rifle_Receiver", tacticalRifle, Vector3.zero, new Vector3(0.05f, 0.12f, 0.34f), matGunmetal);
            CreateMeshPart(PrimitiveType.Cube, "Rifle_Stock", tacticalRifle, new Vector3(0f, 0.02f, -0.22f), new Vector3(0.045f, 0.11f, 0.16f), matGunmetal);
            CreateMeshPart(PrimitiveType.Cube, "Rifle_Handguard", tacticalRifle, new Vector3(0f, 0.015f, 0.24f), new Vector3(0.055f, 0.08f, 0.22f), matGunmetal);
            CreateMeshPart(PrimitiveType.Cylinder, "Rifle_Barrel", tacticalRifle, new Vector3(0f, 0.015f, 0.44f), new Vector3(0.022f, 0.18f, 0.022f), matArmorDark, Quaternion.Euler(90f, 0f, 0f));
            CreateMeshPart(PrimitiveType.Cylinder, "Rifle_MuzzleBrake", tacticalRifle, new Vector3(0f, 0.015f, 0.63f), new Vector3(0.028f, 0.04f, 0.028f), matGunmetal, Quaternion.Euler(90f, 0f, 0f));
            CreateMeshPart(PrimitiveType.Cube, "Rifle_Magazine", tacticalRifle, new Vector3(0f, -0.07f, 0.08f), new Vector3(0.04f, 0.10f, 0.07f), matGunmetal, Quaternion.Euler(14f, 0f, 0f));
            CreateMeshPart(PrimitiveType.Cube, "Rifle_HoloSight", tacticalRifle, new Vector3(0f, 0.08f, 0.02f), new Vector3(0.042f, 0.05f, 0.09f), matArmorDark);

            // Archetype Weapon Attachments
            if (role == AgentRole.Support)
            {
                // Boss Heavy SAW LMG: Dual 100-Round Ammo Drums & Heavy Shroud
                CreateMeshPart(PrimitiveType.Cylinder, "SAW_Drum_L", tacticalRifle, new Vector3(-0.07f, -0.06f, 0.08f), new Vector3(0.10f, 0.05f, 0.10f), matGunmetal, Quaternion.Euler(0f, 0f, 90f));
                CreateMeshPart(PrimitiveType.Cylinder, "SAW_Drum_R", tacticalRifle, new Vector3(0.07f, -0.06f, 0.08f), new Vector3(0.10f, 0.05f, 0.10f), matGunmetal, Quaternion.Euler(0f, 0f, 90f));
                CreateMeshPart(PrimitiveType.Cylinder, "SAW_HeavyShroud", tacticalRifle, new Vector3(0f, 0.015f, 0.38f), new Vector3(0.07f, 0.14f, 0.07f), matArmorDark, Quaternion.Euler(90f, 0f, 0f));
            }
            else if (role == AgentRole.Flanker)
            {
                // Flanker Ghost Marksman: Long Silencer & High-Power Sniper Scope
                CreateMeshPart(PrimitiveType.Cylinder, "Sniper_Suppressor", tacticalRifle, new Vector3(0f, 0.015f, 0.68f), new Vector3(0.045f, 0.16f, 0.045f), matGunmetal, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPart(PrimitiveType.Cylinder, "Sniper_Scope", tacticalRifle, new Vector3(0f, 0.11f, 0.04f), new Vector3(0.035f, 0.16f, 0.035f), matArmorDark, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPart(PrimitiveType.Sphere, "Sniper_FrontLens", tacticalRifle, new Vector3(0f, 0.11f, 0.13f), new Vector3(0.03f, 0.03f, 0.03f), matVisor);
            }
            else if (role == AgentRole.Hunter)
            {
                // Hunter Stalker: PEQ Laser Unit with glowing amber diode
                CreateMeshPart(PrimitiveType.Cube, "Hunter_PEQ_Box", tacticalRifle, new Vector3(0.045f, 0.06f, 0.22f), new Vector3(0.035f, 0.03f, 0.08f), matGunmetal);
                CreateMeshPart(PrimitiveType.Sphere, "Hunter_LaserEmitter", tacticalRifle, new Vector3(0.045f, 0.06f, 0.27f), new Vector3(0.018f, 0.018f, 0.018f), matVisor);
            }

            // Muzzle Flash Light & Bullet Raycast Point
            GameObject muzzleObj = new GameObject("RifleMuzzlePoint");
            muzzleObj.transform.SetParent(tacticalRifle, false);
            muzzleObj.transform.localPosition = new Vector3(0f, 0.015f, 0.72f);
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

            // 6. Right Arm (Trigger Arm Gripping Weapon)
            GameObject upperArmRObj = new GameObject("UpperArm_R");
            upperArmRObj.transform.SetParent(torso, false);
            upperArmRObj.transform.localPosition = new Vector3(0.25f, 0.16f, 0.05f);
            upperArmR = upperArmRObj.transform;
            defaultArmRRot = Quaternion.Euler(50f, -15f, 5f);
            upperArmR.localRotation = defaultArmRRot;
            CreateMeshPart(PrimitiveType.Capsule, "Sleeve_R", upperArmR, new Vector3(0f, -0.12f, 0.04f), new Vector3(0.12f, 0.20f, 0.12f), matFabric, Quaternion.Euler(20f, 0f, 0f));

            GameObject forearmRObj = new GameObject("Forearm_R");
            forearmRObj.transform.SetParent(upperArmR, false);
            forearmRObj.transform.localPosition = new Vector3(0f, -0.22f, 0.08f);
            forearmR = forearmRObj.transform;
            defaultForearmRRot = Quaternion.Euler(25f, 10f, 0f);
            forearmR.localRotation = defaultForearmRRot;
            CreateMeshPart(PrimitiveType.Capsule, "ForearmArmor_R", forearmR, new Vector3(0f, -0.09f, 0.05f), new Vector3(0.11f, 0.18f, 0.11f), matArmorDark, Quaternion.Euler(30f, 0f, 0f));

            GameObject handRObj = new GameObject("Hand_R");
            handRObj.transform.SetParent(forearmR, false);
            handRObj.transform.localPosition = new Vector3(0f, -0.17f, 0.08f);
            handRObj.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            handR = handRObj.transform;
            CreateMeshPart(PrimitiveType.Cube, "Glove_R", handR, Vector3.zero, new Vector3(0.09f, 0.09f, 0.09f), matLeather);

            // Left Arm
            GameObject upperArmLObj = new GameObject("UpperArm_L");
            upperArmLObj.transform.SetParent(torso, false);
            upperArmLObj.transform.localPosition = new Vector3(-0.25f, 0.16f, 0f);
            upperArmL = upperArmLObj.transform;
            defaultArmLRot = Quaternion.Euler(-6f, 0f, 6f);
            upperArmL.localRotation = defaultArmLRot;
            CreateMeshPart(PrimitiveType.Capsule, "Sleeve_L", upperArmL, new Vector3(0f, -0.11f, 0f), new Vector3(0.12f, 0.20f, 0.12f), matFabric);

            GameObject forearmLObj = new GameObject("Forearm_L");
            forearmLObj.transform.SetParent(upperArmL, false);
            forearmLObj.transform.localPosition = new Vector3(0f, -0.20f, 0f);
            forearmL = forearmLObj.transform;
            defaultForearmLRot = Quaternion.Euler(-10f, 0f, 0f);
            forearmL.localRotation = defaultForearmLRot;
            CreateMeshPart(PrimitiveType.Capsule, "ForearmArmor_L", forearmL, new Vector3(0f, -0.09f, 0f), new Vector3(0.11f, 0.18f, 0.11f), matArmorDark);

            // Flanker Wrist Tactical Computer
            if (role == AgentRole.Flanker)
            {
                CreateMeshPart(PrimitiveType.Cube, "WristComputer", forearmL, new Vector3(0f, -0.06f, 0.06f), new Vector3(0.08f, 0.07f, 0.03f), matTrim);
            }

            GameObject handLObj = new GameObject("Hand_L");
            handLObj.transform.SetParent(forearmL, false);
            handLObj.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            handLObj.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            handL = handLObj.transform;
            CreateMeshPart(PrimitiveType.Cube, "Glove_L", handL, Vector3.zero, new Vector3(0.08f, 0.09f, 0.08f), matLeather);

            // 7. Tactical Legs & Combat Boots
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
            // Armored Knee Pad
            CreateMeshPart(PrimitiveType.Cube, "KneePad_L", shinL, new Vector3(0f, 0.02f, 0.08f), new Vector3(0.11f, 0.11f, 0.04f), matArmor);
            // Tactical Combat Boot
            CreateMeshPart(PrimitiveType.Capsule, "BootShaft_L", shinL, new Vector3(0f, -0.18f, 0f), new Vector3(0.13f, 0.26f, 0.13f), matLeather);

            GameObject footLObj = new GameObject("Foot_L");
            footLObj.transform.SetParent(shinL, false);
            footLObj.transform.localPosition = new Vector3(0f, -0.32f, 0.06f);
            footL = footLObj.transform;
            CreateMeshPart(PrimitiveType.Cube, "CombatBoot_L", footL, Vector3.zero, new Vector3(0.13f, 0.10f, 0.25f), matLeather);

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
            // Armored Knee Pad
            CreateMeshPart(PrimitiveType.Cube, "KneePad_R", shinR, new Vector3(0f, 0.02f, 0.08f), new Vector3(0.11f, 0.11f, 0.04f), matArmor);
            // Tactical Combat Boot
            CreateMeshPart(PrimitiveType.Capsule, "BootShaft_R", shinR, new Vector3(0f, -0.18f, 0f), new Vector3(0.13f, 0.26f, 0.13f), matLeather);

            GameObject footRObj = new GameObject("Foot_R");
            footRObj.transform.SetParent(shinR, false);
            footRObj.transform.localPosition = new Vector3(0f, -0.32f, 0.06f);
            footR = footRObj.transform;
            CreateMeshPart(PrimitiveType.Cube, "CombatBoot_R", footR, Vector3.zero, new Vector3(0.13f, 0.10f, 0.25f), matLeather);
        }

        private GameObject CreateMeshPart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion rot = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            if (rot != default) part.transform.localRotation = rot;

            var r = part.GetComponent<Renderer>();
            if (r != null) r.material = mat;

            var col = part.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            return part;
        }

        private Material CreatePbrMaterial(string name, Color color, float metallic = 0.5f, float smoothness = 0.5f, Color emission = default)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            Material mat = new Material(shader);
            mat.name = name;

            // Set both URP Lit (_BaseColor) and Standard (_Color)
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color = color;

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (emission != default && emission != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
            }

            return mat;
        }

        private void CacheRenderers()
        {
            if (visualRoot == null) return;
            allRenderers = visualRoot.GetComponentsInChildren<Renderer>();
            originalColors = new Color[allRenderers.Length];
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].sharedMaterial != null)
                {
                    Material m = allRenderers[i].sharedMaterial;
                    if (m.HasProperty("_BaseColor")) originalColors[i] = m.GetColor("_BaseColor");
                    else originalColors[i] = m.color;
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

            // Ensure locomotion & AI systems stop controlling defeated agent
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            var mov = GetComponent<AIMovement>();
            if (mov != null)
            {
                mov.Stop();
                mov.enabled = false;
            }

            var perc = GetComponent<AIPerception>();
            if (perc != null) perc.enabled = false;

            var nma = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nma != null) nma.enabled = false;

            StartCoroutine(KnockdownRoutine());
        }

        private IEnumerator KnockdownRoutine()
        {
            if (visualRoot == null) yield break;

            // Ensure enemy base is firmly anchored to the top of the floor
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            }

            Quaternion startRot = visualRoot.localRotation;
            Quaternion targetRot = Quaternion.Euler(-90f, 0f, 0f);
            Vector3 startPos = visualRoot.localPosition;

            // Align defeated body so it rests cleanly ON the floor surface without sinking into the ground
            Vector3 targetPos = new Vector3(startPos.x, 0.12f, startPos.z - 0.15f);

            float elapsed = 0f;
            float duration = 0.55f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (visualRoot != null)
                {
                    visualRoot.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                    visualRoot.localPosition = Vector3.Lerp(startPos, targetPos, t);
                }
                yield return null;
            }

            if (visualRoot != null)
            {
                visualRoot.localRotation = targetRot;
                visualRoot.localPosition = targetPos;
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
                    Material m = allRenderers[i].material;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", col);
                    m.color = col;
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
                    Material m = allRenderers[i].material;
                    Color oc = originalColors[i];
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", oc);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", oc);
                    m.color = oc;
                }
            }
        }
    }
}
