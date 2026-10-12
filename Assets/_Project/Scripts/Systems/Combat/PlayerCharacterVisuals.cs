using UnityEngine;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Monkey D. Luffy (One Piece) character visuals with realistic full-body locomotion:
    /// - Full biomechanical gait cycle for Walking and Running (Sprint)
    /// - Realistic contralateral arm swings (left arm opposes left leg, dynamic elbow flexion)
    /// - Realistic articulated leg strides (thigh forward reach, backward knee flexion, ankle roll)
    /// - Pelvis vertical step-bob, lateral hip sway, and torso counter-torsion
    /// - Seamless switch between 1st Person (hidden) and 3rd Person (visible)
    /// - Plug-and-play support for imported 3D Models (.FBX / Prefabs with Animator or bone hierarchy)
    /// </summary>
    public class PlayerCharacterVisuals : MonoBehaviour
    {
        [Header("Character Body Root")]
        [SerializeField] private GameObject bodyRoot;
        [SerializeField] private Transform upperBody;
        [SerializeField] private Transform thirdPersonRifle;

        [Header("Custom 3D Model (FBX / Prefab)")]
        [Tooltip("Assign your Luffy FBX or 3D Character Prefab here. If assigned, it overrides procedural visuals.")]
        [SerializeField] private GameObject customModelPrefab;
        [SerializeField] private float modelScaleMultiplier = 1.0f;
        [SerializeField] private Vector3 modelOffset = Vector3.zero;
        [Tooltip("If true, procedural articulated character is built whenever no external 3D FBX is provided.")]
        [SerializeField] private bool fallbackToProceduralShapes = true;

        private Animator customAnimator;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

        // Articulated Lower Body Transforms
        private Transform pelvis;
        private Transform thighL;
        private Transform shinL;
        private Transform footL;
        private Transform thighR;
        private Transform shinR;
        private Transform footR;

        // Articulated Upper Body & Arm Transforms
        private Transform upperArmL;
        private Transform forearmL;
        private Transform handL;
        private Transform upperArmR;
        private Transform forearmR;
        private Transform handR;

        // Accessories & Secondary Transforms
        private Transform sashTail;
        private Transform strawHatRoot;
        private Transform thirdPersonMuzzle;

        public Transform ThirdPersonMuzzlePoint => thirdPersonMuzzle != null ? thirdPersonMuzzle : thirdPersonRifle;

        private PlayerCombatController combatController;
        private CharacterController characterController;
        private Transform playerCamera;

        // Locomotion animation state
        private float strideCycle = 0f;
        private Vector3 defaultPelvisPos;
        private Vector3 defaultUpperBodyLocalPos;

        private Quaternion defaultUpperArmLRot = Quaternion.Euler(-6f, 0f, -8f);
        private Quaternion defaultForearmLRot = Quaternion.Euler(-18f, 0f, 0f);
        private Quaternion defaultUpperArmRRot = Quaternion.Euler(-28f, -10f, 4f);
        private Quaternion defaultForearmRRot = Quaternion.Euler(-55f, 0f, 0f);
        private Quaternion defaultHandRRot = Quaternion.Euler(83f, 0f, 0f);

        private void Awake()
        {
            combatController = GetComponent<PlayerCombatController>();
            characterController = GetComponent<CharacterController>();

            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;

            if (bodyRoot != null)
            {
                Destroy(bodyRoot);
                bodyRoot = null;
            }

            Transform found = transform.Find("CharacterBody");
            if (found != null)
            {
                Destroy(found.gameObject);
            }

            // 1. Try loading model from Resources if not assigned
            if (customModelPrefab == null)
            {
                customModelPrefab = Resources.Load<GameObject>("LuffyModel");
                if (customModelPrefab == null) customModelPrefab = Resources.Load<GameObject>("PlayerModel");
            }

            // 2. Instantiate custom FBX or generate articulated humanoid body
            if (customModelPrefab != null)
            {
                bodyRoot = Instantiate(customModelPrefab, transform);
                bodyRoot.name = "CharacterBody";
                bodyRoot.transform.localPosition = modelOffset;
                bodyRoot.transform.localRotation = Quaternion.identity;
                bodyRoot.transform.localScale = Vector3.one * modelScaleMultiplier;

                customAnimator = bodyRoot.GetComponentInChildren<Animator>();

                foreach (var col in bodyRoot.GetComponentsInChildren<Collider>())
                {
                    Destroy(col);
                }

                Transform handBone = FindRightHandBone(bodyRoot.transform);
                if (handBone != null)
                {
                    thirdPersonRifle = CreateThirdPersonWeapon(handBone);
                }

                Debug.Log($"[PlayerCharacterVisuals] ✅ Successfully loaded 3D Character Model: '{customModelPrefab.name}'!");
            }
            else if (fallbackToProceduralShapes)
            {
                bodyRoot = BuildLuffyCharacterBody();
            }

            if (bodyRoot != null)
            {
                bool shouldBeActive = combatController != null ? combatController.IsThirdPerson : true;
                bodyRoot.SetActive(shouldBeActive);
            }

            if (pelvis != null) defaultPelvisPos = pelvis.localPosition;
            if (upperBody != null) defaultUpperBodyLocalPos = upperBody.localPosition;
        }

        private void Start()
        {
            UpdateVisibility();
        }

        private GameObject BuildLuffyCharacterBody()
        {
            GameObject root = new GameObject("CharacterBody");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            root.transform.localRotation = Quaternion.identity;

            // Anime Luffy Color Palette
            Color luffySkin = new Color(1f, 0.88f, 0.78f);        // Anime skin tone
            Color luffyScar = new Color(0.82f, 0.58f, 0.50f);      // Chest X-scar and eye scar
            Color strawYellow = new Color(0.92f, 0.80f, 0.44f);    // Straw Hat yellow
            Color ribbonRed = new Color(0.85f, 0.15f, 0.15f);      // Hat ribbon red
            Color vestRed = new Color(0.92f, 0.18f, 0.18f);        // Open red cardigan
            Color sashYellow = new Color(0.98f, 0.85f, 0.22f);     // Waist sash yellow
            Color denimBlue = new Color(0.15f, 0.52f, 0.90f);      // Denim shorts blue
            Color fleeceWhite = new Color(0.95f, 0.95f, 0.98f);    // Fluffy shorts cuffs
            Color sandalStraw = new Color(0.82f, 0.72f, 0.58f);    // Straw sandal base
            Color sandalStrap = new Color(0.15f, 0.15f, 0.15f);    // Sandal straps
            Color hairBlack = new Color(0.12f, 0.12f, 0.14f);      // Messy black hair

            // 1. Pelvis / Hips (Root of lower body)
            GameObject pelvisObj = new GameObject("Pelvis");
            pelvisObj.transform.SetParent(root.transform, false);
            pelvisObj.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            pelvis = pelvisObj.transform;

            // Denim Shorts (Waist) - Smooth curved pelvis
            CreatePart(PrimitiveType.Capsule, "Shorts_Pelvis", pelvisObj.transform, Vector3.zero, new Vector3(0.28f, 0.16f, 0.22f), denimBlue, Quaternion.Euler(0f, 0f, 90f));

            // Yellow Sash wrapped around waist
            CreatePart(PrimitiveType.Capsule, "YellowSash_Belt", pelvisObj.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.31f, 0.10f, 0.24f), sashYellow, Quaternion.Euler(0f, 0f, 90f));
            CreatePart(PrimitiveType.Sphere, "SashKnot", pelvisObj.transform, new Vector3(0.16f, 0.04f, 0.05f), new Vector3(0.09f, 0.09f, 0.09f), sashYellow);

            // Trailing sash cloth (sways during locomotion)
            GameObject sashTailObj = new GameObject("SashTail");
            sashTailObj.transform.SetParent(pelvisObj.transform, false);
            sashTailObj.transform.localPosition = new Vector3(0.20f, -0.02f, 0.02f);
            sashTail = sashTailObj.transform;
            CreatePart(PrimitiveType.Capsule, "SashCloth", sashTailObj.transform, new Vector3(0f, -0.14f, 0f), new Vector3(0.06f, 0.26f, 0.08f), sashYellow, Quaternion.Euler(15f, 0f, 12f));

            // 2. Left Leg (Hip -> Thigh -> Knee -> Shin -> Ankle -> Sandal)
            GameObject thighLObj = new GameObject("Thigh_L");
            thighLObj.transform.SetParent(pelvisObj.transform, false);
            thighLObj.transform.localPosition = new Vector3(-0.11f, -0.06f, 0f);
            thighL = thighLObj.transform;

            CreatePart(PrimitiveType.Capsule, "ShortsLeg_L", thighLObj.transform, new Vector3(0f, -0.09f, 0f), new Vector3(0.14f, 0.17f, 0.14f), denimBlue);
            CreatePart(PrimitiveType.Sphere, "FluffyCuff_L", thighLObj.transform, new Vector3(0f, -0.21f, 0f), new Vector3(0.16f, 0.06f, 0.16f), fleeceWhite);
            CreatePart(PrimitiveType.Capsule, "BareThigh_L", thighLObj.transform, new Vector3(0f, -0.26f, 0f), new Vector3(0.10f, 0.10f, 0.10f), luffySkin);

            GameObject shinLObj = new GameObject("Shin_L");
            shinLObj.transform.SetParent(thighLObj.transform, false);
            shinLObj.transform.localPosition = new Vector3(0f, -0.32f, 0f);
            shinL = shinLObj.transform;

            CreatePart(PrimitiveType.Capsule, "BareCalf_L", shinLObj.transform, new Vector3(0f, -0.16f, 0f), new Vector3(0.09f, 0.22f, 0.09f), luffySkin);

            GameObject footLObj = new GameObject("Foot_L");
            footLObj.transform.SetParent(shinLObj.transform, false);
            footLObj.transform.localPosition = new Vector3(0f, -0.32f, 0.04f);
            footL = footLObj.transform;

            CreatePart(PrimitiveType.Capsule, "SandalSole_L", footLObj.transform, Vector3.zero, new Vector3(0.11f, 0.035f, 0.21f), sandalStraw, Quaternion.Euler(0f, 0f, 90f));
            CreatePart(PrimitiveType.Cube, "Strap_L1", footLObj.transform, new Vector3(0.02f, 0.02f, 0.01f), new Vector3(0.02f, 0.025f, 0.08f), sandalStrap, Quaternion.Euler(25f, -30f, 0f));
            CreatePart(PrimitiveType.Cube, "Strap_L2", footLObj.transform, new Vector3(-0.02f, 0.02f, 0.01f), new Vector3(0.02f, 0.025f, 0.08f), sandalStrap, Quaternion.Euler(25f, 30f, 0f));

            // 3. Right Leg (Hip -> Thigh -> Knee -> Shin -> Ankle -> Sandal)
            GameObject thighRObj = new GameObject("Thigh_R");
            thighRObj.transform.SetParent(pelvisObj.transform, false);
            thighRObj.transform.localPosition = new Vector3(0.11f, -0.06f, 0f);
            thighR = thighRObj.transform;

            CreatePart(PrimitiveType.Capsule, "ShortsLeg_R", thighRObj.transform, new Vector3(0f, -0.09f, 0f), new Vector3(0.14f, 0.17f, 0.14f), denimBlue);
            CreatePart(PrimitiveType.Sphere, "FluffyCuff_R", thighRObj.transform, new Vector3(0f, -0.21f, 0f), new Vector3(0.16f, 0.06f, 0.16f), fleeceWhite);
            CreatePart(PrimitiveType.Capsule, "BareThigh_R", thighRObj.transform, new Vector3(0f, -0.26f, 0f), new Vector3(0.10f, 0.10f, 0.10f), luffySkin);

            GameObject shinRObj = new GameObject("Shin_R");
            shinRObj.transform.SetParent(thighRObj.transform, false);
            shinRObj.transform.localPosition = new Vector3(0f, -0.32f, 0f);
            shinR = shinRObj.transform;

            CreatePart(PrimitiveType.Capsule, "BareCalf_R", shinRObj.transform, new Vector3(0f, -0.16f, 0f), new Vector3(0.09f, 0.22f, 0.09f), luffySkin);

            GameObject footRObj = new GameObject("Foot_R");
            footRObj.transform.SetParent(shinRObj.transform, false);
            footRObj.transform.localPosition = new Vector3(0f, -0.32f, 0.04f);
            footR = footRObj.transform;

            CreatePart(PrimitiveType.Capsule, "SandalSole_R", footRObj.transform, Vector3.zero, new Vector3(0.11f, 0.035f, 0.21f), sandalStraw, Quaternion.Euler(0f, 0f, 90f));
            CreatePart(PrimitiveType.Cube, "Strap_R1", footRObj.transform, new Vector3(0.02f, 0.02f, 0.01f), new Vector3(0.02f, 0.025f, 0.08f), sandalStrap, Quaternion.Euler(25f, -30f, 0f));
            CreatePart(PrimitiveType.Cube, "Strap_R2", footRObj.transform, new Vector3(-0.02f, 0.02f, 0.01f), new Vector3(0.02f, 0.025f, 0.08f), sandalStrap, Quaternion.Euler(25f, 30f, 0f));

            // 4. Upper Body (Smooth muscular torso & Open Red Cardigan)
            GameObject torsoObj = new GameObject("Torso");
            torsoObj.transform.SetParent(root.transform, false);
            torsoObj.transform.localPosition = new Vector3(0f, 1.14f, 0f);
            upperBody = torsoObj.transform;

            // Bare Muscular Chest / Core
            CreatePart(PrimitiveType.Capsule, "Torso_Core", torsoObj.transform, Vector3.zero, new Vector3(0.28f, 0.22f, 0.22f), luffySkin);
            CreatePart(PrimitiveType.Cube, "XScar_1", torsoObj.transform, new Vector3(0f, 0.06f, 0.105f), new Vector3(0.025f, 0.15f, 0.01f), luffyScar, Quaternion.Euler(0f, 0f, 35f));
            CreatePart(PrimitiveType.Cube, "XScar_2", torsoObj.transform, new Vector3(0f, 0.06f, 0.105f), new Vector3(0.025f, 0.15f, 0.01f), luffyScar, Quaternion.Euler(0f, 0f, -35f));

            // Open Red Cardigan (Curved vest hugging shoulders and torso)
            CreatePart(PrimitiveType.Capsule, "Vest_Back", torsoObj.transform, new Vector3(0f, 0.02f, -0.04f), new Vector3(0.31f, 0.24f, 0.22f), vestRed);
            CreatePart(PrimitiveType.Capsule, "Vest_FlapL", torsoObj.transform, new Vector3(-0.14f, 0f, 0.03f), new Vector3(0.07f, 0.22f, 0.16f), vestRed);
            CreatePart(PrimitiveType.Capsule, "Vest_FlapR", torsoObj.transform, new Vector3(0.14f, 0f, 0.03f), new Vector3(0.07f, 0.22f, 0.16f), vestRed);
            CreatePart(PrimitiveType.Capsule, "Vest_HemL", torsoObj.transform, new Vector3(-0.13f, -0.16f, 0.02f), new Vector3(0.08f, 0.12f, 0.14f), vestRed, Quaternion.Euler(10f, 0f, -12f));
            CreatePart(PrimitiveType.Capsule, "Vest_HemR", torsoObj.transform, new Vector3(0.13f, -0.16f, 0.02f), new Vector3(0.08f, 0.12f, 0.14f), vestRed, Quaternion.Euler(10f, 0f, 12f));

            // 5. Left Arm (Shoulder -> Upper Arm -> Elbow -> Forearm -> Wrist -> Hand Fist)
            GameObject upperArmLObj = new GameObject("UpperArm_L");
            upperArmLObj.transform.SetParent(torsoObj.transform, false);
            upperArmLObj.transform.localPosition = new Vector3(-0.20f, 0.10f, 0.02f);
            upperArmLObj.transform.localRotation = defaultUpperArmLRot;
            upperArmL = upperArmLObj.transform;

            CreatePart(PrimitiveType.Capsule, "Sleeve_L", upperArmLObj.transform, new Vector3(0f, -0.08f, 0f), new Vector3(0.11f, 0.14f, 0.11f), vestRed);

            GameObject forearmLObj = new GameObject("Forearm_L");
            forearmLObj.transform.SetParent(upperArmLObj.transform, false);
            forearmLObj.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            forearmLObj.transform.localRotation = defaultForearmLRot;
            forearmL = forearmLObj.transform;

            CreatePart(PrimitiveType.Capsule, "BareForearm_L", forearmLObj.transform, new Vector3(0f, -0.08f, 0f), new Vector3(0.08f, 0.16f, 0.08f), luffySkin);

            GameObject handLObj = new GameObject("Hand_L");
            handLObj.transform.SetParent(forearmLObj.transform, false);
            handLObj.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            handL = handLObj.transform;

            CreatePart(PrimitiveType.Sphere, "Fist_L", handLObj.transform, Vector3.zero, new Vector3(0.085f, 0.085f, 0.085f), luffySkin);

            // 6. Right Arm (Shoulder -> Upper Arm -> Elbow -> Forearm -> Wrist -> Hand holding Weapon)
            GameObject upperArmRObj = new GameObject("UpperArm_R");
            upperArmRObj.transform.SetParent(torsoObj.transform, false);
            upperArmRObj.transform.localPosition = new Vector3(0.20f, 0.10f, 0.02f);
            upperArmRObj.transform.localRotation = defaultUpperArmRRot;
            upperArmR = upperArmRObj.transform;

            CreatePart(PrimitiveType.Capsule, "Sleeve_R", upperArmRObj.transform, new Vector3(0f, -0.08f, 0f), new Vector3(0.11f, 0.14f, 0.11f), vestRed);

            GameObject forearmRObj = new GameObject("Forearm_R");
            forearmRObj.transform.SetParent(upperArmRObj.transform, false);
            forearmRObj.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            forearmRObj.transform.localRotation = defaultForearmRRot;
            forearmR = forearmRObj.transform;

            CreatePart(PrimitiveType.Capsule, "BareForearm_R", forearmRObj.transform, new Vector3(0f, -0.08f, 0f), new Vector3(0.08f, 0.16f, 0.08f), luffySkin);

            GameObject handRObj = new GameObject("Hand_R");
            handRObj.transform.SetParent(forearmRObj.transform, false);
            handRObj.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            handRObj.transform.localRotation = defaultHandRRot;
            handR = handRObj.transform;

            CreatePart(PrimitiveType.Sphere, "Fist_R", handRObj.transform, Vector3.zero, new Vector3(0.085f, 0.085f, 0.085f), luffySkin);

            // 7. Third-Person Tactical Carbine parented directly to Right Hand
            GameObject rifleObj = new GameObject("ThirdPersonRifle");
            rifleObj.transform.SetParent(handRObj.transform, false);
            rifleObj.transform.localPosition = new Vector3(0.03f, 0.02f, 0.12f);
            rifleObj.transform.localRotation = Quaternion.identity;
            thirdPersonRifle = rifleObj.transform;

            Color gunMetal = new Color(0.15f, 0.16f, 0.18f);
            CreatePart(PrimitiveType.Capsule, "Rifle_Body", rifleObj.transform, Vector3.zero, new Vector3(0.05f, 0.16f, 0.06f), gunMetal, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cylinder, "Rifle_Barrel", rifleObj.transform, new Vector3(0f, 0.012f, 0.24f), new Vector3(0.02f, 0.11f, 0.02f), sandalStrap, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cube, "Rifle_Mag", rifleObj.transform, new Vector3(0f, -0.07f, 0.04f), new Vector3(0.035f, 0.10f, 0.06f), sandalStrap, Quaternion.Euler(15f, 0f, 0f));

            // Third-Person Muzzle Tip (where bullet tracers visually fire from)
            GameObject muzzleObj = new GameObject("ThirdPersonMuzzlePoint");
            muzzleObj.transform.SetParent(rifleObj.transform, false);
            muzzleObj.transform.localPosition = new Vector3(0f, 0.012f, 0.36f);
            thirdPersonMuzzle = muzzleObj.transform;

            // 8. Head, Face, Organic Black Hair & Straw Hat (No blocky box eyes)
            GameObject headObj = new GameObject("Head");
            headObj.transform.SetParent(torsoObj.transform, false);
            headObj.transform.localPosition = new Vector3(0f, 0.34f, 0f);

            CreatePart(PrimitiveType.Sphere, "Face", headObj.transform, Vector3.zero, new Vector3(0.24f, 0.25f, 0.23f), luffySkin);

            // Smooth curved hair volume (seamlessly cupping the back of the head, NOT a block!)
            CreatePart(PrimitiveType.Sphere, "HairCap", headObj.transform, new Vector3(0f, 0.03f, -0.02f), new Vector3(0.255f, 0.24f, 0.25f), hairBlack);

            // Anime Bangs & Spikes
            CreatePart(PrimitiveType.Capsule, "Bang_L", headObj.transform, new Vector3(-0.06f, 0.07f, 0.11f), new Vector3(0.035f, 0.08f, 0.035f), hairBlack, Quaternion.Euler(30f, -15f, -25f));
            CreatePart(PrimitiveType.Capsule, "Bang_C", headObj.transform, new Vector3(0f, 0.08f, 0.12f), new Vector3(0.04f, 0.09f, 0.04f), hairBlack, Quaternion.Euler(25f, 0f, 0f));
            CreatePart(PrimitiveType.Capsule, "Bang_R", headObj.transform, new Vector3(0.06f, 0.07f, 0.11f), new Vector3(0.035f, 0.08f, 0.035f), hairBlack, Quaternion.Euler(30f, 15f, 25f));

            CreatePart(PrimitiveType.Capsule, "HairTuft_L", headObj.transform, new Vector3(-0.12f, 0.01f, 0.02f), new Vector3(0.035f, 0.10f, 0.035f), hairBlack, Quaternion.Euler(0f, 0f, 20f));
            CreatePart(PrimitiveType.Capsule, "HairTuft_R", headObj.transform, new Vector3(0.12f, 0.01f, 0.02f), new Vector3(0.035f, 0.10f, 0.035f), hairBlack, Quaternion.Euler(0f, 0f, -20f));

            // Straw Hat (Mugiwara)
            GameObject hatObj = new GameObject("StrawHat");
            hatObj.transform.SetParent(headObj.transform, false);
            hatObj.transform.localPosition = new Vector3(0f, 0.08f, -0.02f);
            hatObj.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            strawHatRoot = hatObj.transform;

            CreatePart(PrimitiveType.Cylinder, "HatBrim", hatObj.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.48f, 0.012f, 0.50f), strawYellow);
            CreatePart(PrimitiveType.Cylinder, "HatBand", hatObj.transform, new Vector3(0f, 0.075f, 0f), new Vector3(0.285f, 0.04f, 0.285f), ribbonRed);
            CreatePart(PrimitiveType.Sphere, "HatCrown", hatObj.transform, new Vector3(0f, 0.095f, 0f), new Vector3(0.28f, 0.16f, 0.28f), strawYellow);

            // Character body is active by default so player is visible immediately
            root.SetActive(true);
            return root;
        }

        private GameObject CreatePart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color, Quaternion? localRot = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (localRot.HasValue) part.transform.localRotation = localRot.Value;

            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = null;
                if (rend.sharedMaterial != null)
                {
                    mat = new Material(rend.sharedMaterial);
                }
                else
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
                    mat = s != null ? new Material(s) : new Material(Shader.Find("Unlit/Color"));
                }

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                mat.color = color;
                rend.material = mat;
            }

            return part;
        }

        private void LateUpdate()
        {
            UpdateVisibility();
            AnimateLocomotion();
        }

        private void UpdateVisibility()
        {
            if (bodyRoot == null) return;

            bool isThirdPerson = combatController != null ? combatController.IsThirdPerson : true;

            if (bodyRoot.activeSelf != isThirdPerson)
            {
                bodyRoot.SetActive(isThirdPerson);
            }

            // Aim alignment in 3rd person
            if (isThirdPerson && upperBody != null && playerCamera != null)
            {
                float pitch = playerCamera.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                pitch = Mathf.Clamp(pitch, -40f, 40f);
                upperBody.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void AnimateLocomotion()
        {
            if (bodyRoot == null || !bodyRoot.activeSelf) return;

            bool isDead = combatController != null && combatController.PlayerHealth != null && !combatController.PlayerHealth.IsAlive;
            if (isDead)
            {
                bodyRoot.transform.localRotation = Quaternion.Slerp(bodyRoot.transform.localRotation, Quaternion.Euler(-75f, 0f, 0f), Time.deltaTime * 4.5f);
                bodyRoot.transform.localPosition = Vector3.Lerp(bodyRoot.transform.localPosition, new Vector3(0f, -0.65f, -0.35f), Time.deltaTime * 4.5f);
                return;
            }
            else if (bodyRoot.transform.localRotation != Quaternion.identity)
            {
                bodyRoot.transform.localRotation = Quaternion.Slerp(bodyRoot.transform.localRotation, Quaternion.identity, Time.deltaTime * 8f);
                bodyRoot.transform.localPosition = Vector3.Lerp(bodyRoot.transform.localPosition, new Vector3(0f, -0.10f, 0f), Time.deltaTime * 8f);
            }

            if (customAnimator != null)
            {
                float animSpeed = characterController != null ? characterController.velocity.magnitude : 0f;
                bool animMoving = animSpeed > 0.15f;
                bool animRunning = animSpeed > 6.0f;

                customAnimator.SetFloat(SpeedHash, animSpeed);
                customAnimator.SetBool(IsMovingHash, animMoving);
                customAnimator.SetBool(IsRunningHash, animRunning);
                return;
            }

            float currentSpeed = characterController != null ? characterController.velocity.magnitude : 0f;
            bool isGrounded = characterController != null && characterController.isGrounded;
            bool isMoving = isGrounded && currentSpeed > 0.15f;
            bool isRunning = isGrounded && currentSpeed > 6.0f; // Left Shift Sprint

            // Wall clearance detection: check if facing a wall up close
            bool isNearWall = false;
            if (Physics.Raycast(transform.position + Vector3.up * 1.1f, transform.forward, out RaycastHit wallHit, 0.65f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (wallHit.collider != null && !wallHit.collider.transform.IsChildOf(transform) && wallHit.collider.gameObject != gameObject)
                {
                    isNearWall = true;
                }
            }

            if (!isGrounded)
            {
                // Airborne Jumping Pose: knees slightly flexed, feet poised for landing
                float airBlend = Time.deltaTime * 12f;

                if (thighL != null) thighL.localRotation = Quaternion.Slerp(thighL.localRotation, Quaternion.Euler(-22f, 0f, -4f), airBlend);
                if (shinL != null) shinL.localRotation = Quaternion.Slerp(shinL.localRotation, Quaternion.Euler(46f, 0f, 0f), airBlend);
                if (footL != null) footL.localRotation = Quaternion.Slerp(footL.localRotation, Quaternion.Euler(-15f, 0f, 0f), airBlend);

                if (thighR != null) thighR.localRotation = Quaternion.Slerp(thighR.localRotation, Quaternion.Euler(-14f, 0f, 4f), airBlend);
                if (shinR != null) shinR.localRotation = Quaternion.Slerp(shinR.localRotation, Quaternion.Euler(38f, 0f, 0f), airBlend);
                if (footR != null) footR.localRotation = Quaternion.Slerp(footR.localRotation, Quaternion.Euler(-10f, 0f, 0f), airBlend);

                if (pelvis != null)
                {
                    pelvis.localPosition = Vector3.Lerp(pelvis.localPosition, defaultPelvisPos, airBlend);
                    pelvis.localRotation = Quaternion.Slerp(pelvis.localRotation, Quaternion.identity, airBlend);
                }

                if (strawHatRoot != null) strawHatRoot.localRotation = Quaternion.Slerp(strawHatRoot.localRotation, Quaternion.Euler(-18f, 0f, 0f), airBlend);
                if (sashTail != null) sashTail.localRotation = Quaternion.Slerp(sashTail.localRotation, Quaternion.Euler(22f, 0f, 25f), airBlend);
            }
            else if (isMoving)
            {
                // Stride cadence frequency
                float cycleSpeed = isRunning ? 15.5f : 9.5f;
                strideCycle += Time.deltaTime * cycleSpeed;

                float swingAngle = isRunning ? 48f : 26f;
                float kneeBend = isRunning ? 70f : 34f;

                // 1. Leg Gait Mechanics (Natural grounded walking steps)
                float phaseL = Mathf.Sin(strideCycle);
                float phaseR = -phaseL;

                if (thighL != null) thighL.localRotation = Quaternion.Euler(phaseL * swingAngle, 0f, 0f);
                if (shinL != null)
                {
                    // Knee flexes forward during swing to lift sandal, straightens on stance to plant firmly on floor
                    float kneeAngle = Mathf.Clamp(phaseL * kneeBend, 0f, kneeBend);
                    shinL.localRotation = Quaternion.Euler(kneeAngle, 0f, 0f);
                }
                if (footL != null)
                {
                    // Ankle pitch: heel strikes ground on forward reach, flexes flat on stance
                    float footAngle = Mathf.Clamp(-phaseL * 18f, -12f, 15f);
                    footL.localRotation = Quaternion.Euler(footAngle, 0f, 0f);
                }

                if (thighR != null) thighR.localRotation = Quaternion.Euler(phaseR * swingAngle, 0f, 0f);
                if (shinR != null)
                {
                    float kneeAngle = Mathf.Clamp(phaseR * kneeBend, 0f, kneeBend);
                    shinR.localRotation = Quaternion.Euler(kneeAngle, 0f, 0f);
                }
                if (footR != null)
                {
                    float footAngle = Mathf.Clamp(-phaseR * 18f, -12f, 15f);
                    footR.localRotation = Quaternion.Euler(footAngle, 0f, 0f);
                }

                // 2. Hand & Arm Locomotion (Opposes leg movement with natural forward elbow flexion)
                // Negative X tilts limb forward (+Z), positive X tilts limb backward (-Z).
                float armSwingAngle = isRunning ? 44f : 24f;
                float armAngleL = -phaseL * armSwingAngle;

                if (upperArmL != null)
                {
                    upperArmL.localRotation = Quaternion.Euler(armAngleL - 6f, 0f, -8f);
                }
                if (forearmL != null)
                {
                    // Elbow always flexes forward (negative X), never backwards
                    float elbowFlex = isRunning ? 48f : 24f;
                    float elbowAngleL = -(18f + Mathf.Clamp(-phaseL, 0f, 1f) * elbowFlex);
                    forearmL.localRotation = Quaternion.Euler(elbowAngleL, 0f, 0f);
                }

                // Right Arm (Tactical firearm ready-carry + sprint pump + close-wall clearance)
                if (upperArmR != null)
                {
                    if (isNearWall)
                    {
                        upperArmR.localRotation = Quaternion.Euler(-55f, -14f, 6f);
                    }
                    else if (isRunning)
                    {
                        float sprintArmBob = Mathf.Sin(strideCycle) * 10f;
                        upperArmR.localRotation = Quaternion.Euler(-34f + sprintArmBob, -12f, 5f);
                    }
                    else
                    {
                        float walkArmBob = Mathf.Sin(strideCycle * 2f) * 2.5f;
                        upperArmR.localRotation = Quaternion.Euler(-28f + walkArmBob, -10f, 4f);
                    }
                }

                if (forearmR != null)
                {
                    if (isNearWall)
                    {
                        forearmR.localRotation = Quaternion.Euler(-80f, 0f, 0f);
                    }
                    else if (isRunning)
                    {
                        float sprintForearmBob = Mathf.Sin(strideCycle) * 6f;
                        forearmR.localRotation = Quaternion.Euler(-62f + sprintForearmBob, 0f, 0f);
                    }
                    else
                    {
                        float walkForearmBob = Mathf.Sin(strideCycle * 2f) * 1.5f;
                        forearmR.localRotation = Quaternion.Euler(-55f + walkForearmBob, 0f, 0f);
                    }
                }

                if (handR != null)
                {
                    if (isNearWall)
                    {
                        handR.localRotation = Quaternion.Euler(135f, 0f, 0f);
                    }
                    else if (isRunning)
                    {
                        handR.localRotation = Quaternion.Euler(96f, 0f, 0f);
                    }
                    else
                    {
                        handR.localRotation = Quaternion.Euler(83f, 0f, 0f);
                    }
                }

                // 3. Pelvis Step Bob & Lateral Hip Sway
                if (pelvis != null)
                {
                    float bobHeight = isRunning ? 0.04f : 0.02f;
                    // In real walking, pelvis drops slightly at step plant to keep feet firmly on floor
                    float stepBob = -Mathf.Abs(Mathf.Sin(strideCycle)) * bobHeight;
                    float hipRoll = Mathf.Sin(strideCycle) * (isRunning ? 3.5f : 1.8f);

                    pelvis.localPosition = defaultPelvisPos + new Vector3(0f, stepBob, 0f);
                    pelvis.localRotation = Quaternion.Euler(0f, 0f, hipRoll);
                }

                // 4. Torso Counter-Torsion & Athletic Forward Lean
                if (upperBody != null)
                {
                    float forwardLean = isRunning ? 14f : 4f;
                    float shoulderTwist = -Mathf.Sin(strideCycle) * (isRunning ? 6.5f : 3.0f);

                    float pitch = 0f;
                    if (playerCamera != null)
                    {
                        pitch = playerCamera.localEulerAngles.x;
                        if (pitch > 180f) pitch -= 360f;
                        pitch = Mathf.Clamp(pitch, -40f, 40f);
                    }

                    upperBody.localRotation = Quaternion.Euler(pitch + forwardLean, shoulderTwist, 0f);
                }

                // 5. Secondary Physics (Sash Sway & Straw Hat Bob)
                if (sashTail != null)
                {
                    float sashSway = Mathf.Sin(strideCycle * 0.9f) * (isRunning ? 26f : 12f);
                    sashTail.localRotation = Quaternion.Euler(10f + sashSway, 0f, 15f + sashSway * 0.5f);
                }

                if (strawHatRoot != null)
                {
                    float hatBob = Mathf.Sin(strideCycle * 1.8f) * (isRunning ? 5f : 2f);
                    strawHatRoot.localRotation = Quaternion.Euler(-10f + hatBob, 0f, 0f);
                }
            }
            else
            {
                // Smooth transition to natural idle resting stance
                float returnSpeed = Time.deltaTime * 10f;

                if (thighL != null) thighL.localRotation = Quaternion.Slerp(thighL.localRotation, Quaternion.identity, returnSpeed);
                if (shinL != null) shinL.localRotation = Quaternion.Slerp(shinL.localRotation, Quaternion.identity, returnSpeed);
                if (footL != null) footL.localRotation = Quaternion.Slerp(footL.localRotation, Quaternion.identity, returnSpeed);

                if (thighR != null) thighR.localRotation = Quaternion.Slerp(thighR.localRotation, Quaternion.identity, returnSpeed);
                if (shinR != null) shinR.localRotation = Quaternion.Slerp(shinR.localRotation, Quaternion.identity, returnSpeed);
                if (footR != null) footR.localRotation = Quaternion.Slerp(footR.localRotation, Quaternion.identity, returnSpeed);

                if (upperArmL != null) upperArmL.localRotation = Quaternion.Slerp(upperArmL.localRotation, defaultUpperArmLRot, returnSpeed);
                if (forearmL != null) forearmL.localRotation = Quaternion.Slerp(forearmL.localRotation, defaultForearmLRot, returnSpeed);

                Quaternion targetUpperArmR = isNearWall ? Quaternion.Euler(-55f, -14f, 6f) : defaultUpperArmRRot;
                Quaternion targetForearmR = isNearWall ? Quaternion.Euler(-80f, 0f, 0f) : defaultForearmRRot;
                Quaternion targetHandR = isNearWall ? Quaternion.Euler(135f, 0f, 0f) : defaultHandRRot;

                if (upperArmR != null) upperArmR.localRotation = Quaternion.Slerp(upperArmR.localRotation, targetUpperArmR, returnSpeed);
                if (forearmR != null) forearmR.localRotation = Quaternion.Slerp(forearmR.localRotation, targetForearmR, returnSpeed);
                if (handR != null) handR.localRotation = Quaternion.Slerp(handR.localRotation, targetHandR, returnSpeed);

                // Gentle breathing idle
                if (pelvis != null)
                {
                    float breathBob = Mathf.Sin(Time.time * 2.2f) * 0.005f;
                    pelvis.localPosition = Vector3.Lerp(pelvis.localPosition, defaultPelvisPos + new Vector3(0f, breathBob, 0f), returnSpeed);
                    pelvis.localRotation = Quaternion.Slerp(pelvis.localRotation, Quaternion.identity, returnSpeed);
                }

                if (upperBody != null && playerCamera != null)
                {
                    float pitch = playerCamera.localEulerAngles.x;
                    if (pitch > 180f) pitch -= 360f;
                    pitch = Mathf.Clamp(pitch, -40f, 40f);
                    upperBody.localRotation = Quaternion.Slerp(upperBody.localRotation, Quaternion.Euler(pitch, 0f, 0f), returnSpeed);
                }

                if (sashTail != null) sashTail.localRotation = Quaternion.Slerp(sashTail.localRotation, Quaternion.Euler(10f, 0f, 15f), returnSpeed);
                if (strawHatRoot != null) strawHatRoot.localRotation = Quaternion.Slerp(strawHatRoot.localRotation, Quaternion.Euler(-10f, 0f, 0f), returnSpeed);
            }
        }

        private Transform FindRightHandBone(Transform root)
        {
            if (customAnimator != null && customAnimator.avatar != null && customAnimator.avatar.isHuman)
            {
                Transform bone = customAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                if (bone != null) return bone;
            }

            string[] handKeywords = { "righthand", "hand.r", "hand_r", "r_hand", "mixamorig:righthand", "wrist_r" };
            foreach (var child in root.GetComponentsInChildren<Transform>())
            {
                string lower = child.name.ToLower();
                foreach (var key in handKeywords)
                {
                    if (lower.Contains(key)) return child;
                }
            }
            return null;
        }

        private Transform CreateThirdPersonWeapon(Transform handTransform)
        {
            GameObject rifleObj = new GameObject("ThirdPersonRifle");
            rifleObj.transform.SetParent(handTransform, false);
            rifleObj.transform.localPosition = new Vector3(0.05f, 0.05f, 0.1f);
            rifleObj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            Color gunMetal = new Color(0.15f, 0.16f, 0.18f);
            CreatePart(PrimitiveType.Cube, "Rifle_Body", rifleObj.transform, Vector3.zero, new Vector3(0.05f, 0.08f, 0.34f), gunMetal);
            CreatePart(PrimitiveType.Cylinder, "Rifle_Barrel", rifleObj.transform, new Vector3(0f, 0.012f, 0.26f), new Vector3(0.02f, 0.11f, 0.02f), Color.black, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cube, "Rifle_Mag", rifleObj.transform, new Vector3(0f, -0.07f, 0.04f), new Vector3(0.035f, 0.10f, 0.06f), Color.black, Quaternion.Euler(15f, 0f, 0f));

            GameObject muzzleObj = new GameObject("ThirdPersonMuzzlePoint");
            muzzleObj.transform.SetParent(rifleObj.transform, false);
            muzzleObj.transform.localPosition = new Vector3(0f, 0.012f, 0.38f);
            thirdPersonMuzzle = muzzleObj.transform;

            return rifleObj.transform;
        }
    }
}
