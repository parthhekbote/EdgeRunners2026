using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Alita;
using InsaneGun.Combat;
using SyntyCombined;

namespace Cyberpunk.World
{
    /// <summary>
    /// AAA Interior Teleportation System for Demo_City.
    /// Connects 9 city-street storefront doorways to furnished interiors located
    /// on the subterranean level below the map (Y = -200).
    /// 
    /// Features:
    /// - Automatic walk-through trigger volume teleportation
    /// - Interactive proximity [E] prompt HUD
    /// - Full CyberpunkLoadingScreen integration with district lore tickers
    /// - Zero-lag CharacterController safe warping with camera sync
    /// - Teleport cooldown to prevent reverse bouncing
    /// </summary>
    public class InteriorTeleportSystem : MonoBehaviour
    {
        public static InteriorTeleportSystem Instance { get; private set; }

        [System.Serializable]
        public class InteriorPortal
        {
            public string id;
            public string displayName;
            public string locationName;
            public Vector3 exteriorDoorPos;
            public Vector3 exteriorSpawnPos;
            public float exteriorSpawnYaw;
            public Vector3 interiorDoorPos;
            public Vector3 interiorSpawnPos;
            public float interiorSpawnYaw;
            public string entryStatus;
            public string exitStatus;

            [HideInInspector] public GameObject exteriorTriggerObj;
            [HideInInspector] public GameObject interiorTriggerObj;
        }

        [Header("Configured City Portals")]
        public List<InteriorPortal> portals = new List<InteriorPortal>();

        [Header("Transition Settings")]
        public float transitionDuration = 0.85f;
        public float teleportCooldown = 2.0f;
        public float interactionRadius = 2.8f;

        // Runtime state
        private bool _isTeleporting = false;
        private float _lastTeleportTime = -10f;
        private Transform _playerTransform;
        private InteriorPortal _nearestPortal;
        private bool _nearestIsInterior = false;

        // GUI Styles
        private GUIStyle _hudBoxStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudSubStyle;
        private Texture2D _hudBgTex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (portals == null || portals.Count == 0)
            {
                PopulateDefaultPortals();
            }

            BuildTriggerVolumes();
            BuildGUIStyles();
        }

        private void Start()
        {
            LocatePlayer();
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                LocatePlayer();
                return;
            }

            if (_isTeleporting) return;

            // Update nearest door for interactive prompt & [E] key
            UpdateNearestPortal();

            if (_nearestPortal != null && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)))
            {
                if (Time.time - _lastTeleportTime >= teleportCooldown)
                {
                    StartCoroutine(TeleportRoutine(_nearestPortal, !_nearestIsInterior));
                }
            }
        }

        private void LocatePlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _playerTransform = p.transform;
                return;
            }

            var swordLoco = FindAnyObjectByType<SyntySwordLocomotionController>();
            if (swordLoco != null)
            {
                _playerTransform = swordLoco.transform;
                return;
            }

            var insaneHero = FindAnyObjectByType<InsaneGunSwordController>();
            if (insaneHero != null)
            {
                _playerTransform = insaneHero.transform;
            }
        }

        private void UpdateNearestPortal()
        {
            _nearestPortal = null;
            if (_playerTransform == null) return;

            Vector3 pPos = _playerTransform.position;
            float closestDist = interactionRadius;

            foreach (var portal in portals)
            {
                // Check exterior door
                float distExt = Vector3.Distance(pPos, portal.exteriorDoorPos);
                if (distExt < closestDist)
                {
                    closestDist = distExt;
                    _nearestPortal = portal;
                    _nearestIsInterior = false;
                }

                // Check interior door
                float distInt = Vector3.Distance(pPos, portal.interiorDoorPos);
                if (distInt < closestDist)
                {
                    closestDist = distInt;
                    _nearestPortal = portal;
                    _nearestIsInterior = true;
                }
            }
        }

        public void PopulateDefaultPortals()
        {
            portals = new List<InteriorPortal>
            {
                // 1. Dr. Ido's Clinic (TearingDoc)
                new InteriorPortal
                {
                    id = "IdoClinic",
                    displayName = "DR. IDO'S CYBER CLINIC",
                    locationName = "CLINIC & RECONSTRUCTION LAB",
                    exteriorDoorPos = new Vector3(19.50f, -18.00f, 47.50f),
                    exteriorSpawnPos = new Vector3(18.0f, -17.99f, 48.5f),
                    exteriorSpawnYaw = 315f,
                    interiorDoorPos = new Vector3(-19.25f, -200.13f, 4.37f),
                    interiorSpawnPos = new Vector3(-21.0f, -200.12f, 2.5f),
                    interiorSpawnYaw = 225f,
                    entryStatus = "DECOMPRESSION CHAMBER 04 ONLINE...",
                    exitStatus = "RETURNING TO SECTOR 4 PLAZA..."
                },

                // 2. Noodle Shop (Cyber Ramen)
                new InteriorPortal
                {
                    id = "NoodleShop",
                    displayName = "CYBER RAMEN & NOODLE BAR",
                    locationName = "FOOD & RECOVERY DISTRICT",
                    exteriorDoorPos = new Vector3(29.00f, -18.00f, 60.00f),
                    exteriorSpawnPos = new Vector3(27.5f, -17.99f, 60.0f),
                    exteriorSpawnYaw = 270f,
                    interiorDoorPos = new Vector3(-9.10f, -200.01f, -8.58f),
                    interiorSpawnPos = new Vector3(-10.0f, -200.00f, -10.0f),
                    interiorSpawnYaw = 225f,
                    entryStatus = "ACCESSING RESTAURANT DISTRICT...",
                    exitStatus = "RETURNING TO STREET DISTRICT..."
                },

                // 3. Weapon Store
                new InteriorPortal
                {
                    id = "WeaponStore",
                    displayName = "BLACK MARKET WEAPONS",
                    locationName = "UNDERGROUND ARMS CACHE",
                    exteriorDoorPos = new Vector3(15.00f, -21.00f, 41.25f),
                    exteriorSpawnPos = new Vector3(16.5f, -17.99f, 41.25f),
                    exteriorSpawnYaw = 90f,
                    interiorDoorPos = new Vector3(5.71f, -199.99f, -8.93f),
                    interiorSpawnPos = new Vector3(5.71f, -200.00f, -10.0f),
                    interiorSpawnYaw = 180f,
                    entryStatus = "SCANNING WEAPONS CONTRABAND...",
                    exitStatus = "RETURNING TO MARKET ROW..."
                },

                // 4. Tactical Shooting Range
                new InteriorPortal
                {
                    id = "ShootingRange",
                    displayName = "TACTICAL SHOOTING RANGE",
                    locationName = "COMBAT SIMULATION DOME",
                    exteriorDoorPos = new Vector3(19.98f, -18.00f, 31.52f),
                    exteriorSpawnPos = new Vector3(19.98f, -17.99f, 33.0f),
                    exteriorSpawnYaw = 0f,
                    interiorDoorPos = new Vector3(20.38f, -200.13f, 5.25f),
                    interiorSpawnPos = new Vector3(20.4f, -200.12f, 3.5f),
                    interiorSpawnYaw = 180f,
                    entryStatus = "INITIALIZING FIRING SIMULATOR...",
                    exitStatus = "EXITING COMBAT FACILITY..."
                },

                // 5. Netrunner Hacker Den
                new InteriorPortal
                {
                    id = "HackerDen",
                    displayName = "NETRUNNER HIDEOUT",
                    locationName = "OFF-GRID CYBER DECK",
                    exteriorDoorPos = new Vector3(-17.50f, -18.00f, 58.75f),
                    exteriorSpawnPos = new Vector3(-16.0f, -17.99f, 58.75f),
                    exteriorSpawnYaw = 90f,
                    interiorDoorPos = new Vector3(1.50f, -200.00f, 5.00f),
                    interiorSpawnPos = new Vector3(1.5f, -199.99f, 3.5f),
                    interiorSpawnYaw = 180f,
                    entryStatus = "BYPASSING NETWATCH SECURITY...",
                    exitStatus = "DISCONNECTING FROM NETRUNNER NODE..."
                },

                // 6. 24/7 Convenience Store
                new InteriorPortal
                {
                    id = "ConvenienceStore",
                    displayName = "24/7 CYBER MART",
                    locationName = "AUTOMATED CORPO STORE",
                    exteriorDoorPos = new Vector3(-21.50f, -18.00f, 62.50f),
                    exteriorSpawnPos = new Vector3(-20.0f, -17.99f, 62.5f),
                    exteriorSpawnYaw = 90f,
                    interiorDoorPos = new Vector3(-38.25f, -200.00f, 4.13f),
                    interiorSpawnPos = new Vector3(-38.0f, -199.99f, 2.5f),
                    interiorSpawnYaw = 180f,
                    entryStatus = "ENTERING 24/7 CONVENIENCE MART...",
                    exitStatus = "RETURNING TO STREET CORRIDOR..."
                },

                // 7. Residential Apartment
                new InteriorPortal
                {
                    id = "Apartment",
                    displayName = "HABITATION APARTMENT",
                    locationName = "CITIZEN LIVING QUARTERS",
                    exteriorDoorPos = new Vector3(-23.00f, -18.00f, 63.75f),
                    exteriorSpawnPos = new Vector3(-22.0f, -17.99f, 62.5f),
                    exteriorSpawnYaw = 135f,
                    interiorDoorPos = new Vector3(-8.37f, -200.13f, -0.25f),
                    interiorSpawnPos = new Vector3(-10.0f, -200.12f, 0.5f),
                    interiorSpawnYaw = 270f,
                    entryStatus = "ACCESSING RESIDENTIAL QUARTERS...",
                    exitStatus = "LEAVING HABITATION UNIT..."
                },

                // 8. Capsule Pod Hotel
                new InteriorPortal
                {
                    id = "MicroPod",
                    displayName = "CAPSULE POD HOTEL",
                    locationName = "MICRO-APARTMENT SLEEP CELLS",
                    exteriorDoorPos = new Vector3(-15.00f, -21.00f, 33.75f),
                    exteriorSpawnPos = new Vector3(-13.5f, -18.06f, 33.75f),
                    exteriorSpawnYaw = 90f,
                    interiorDoorPos = new Vector3(-2.36f, -200.01f, -8.84f),
                    interiorSpawnPos = new Vector3(-2.5f, -200.00f, -10.0f),
                    interiorSpawnYaw = 180f,
                    entryStatus = "INITIALIZING CAPSULE POD...",
                    exitStatus = "EXITING CAPSULE HOTEL..."
                },

                // 9. Corpo Penthouse
                new InteriorPortal
                {
                    id = "Penthouse",
                    displayName = "CORPO EXECUTIVE PENTHOUSE",
                    locationName = "SKY-RISE LUXURY SUITE",
                    exteriorDoorPos = new Vector3(-15.00f, -21.00f, 25.88f),
                    exteriorSpawnPos = new Vector3(-13.5f, -17.75f, 25.88f),
                    exteriorSpawnYaw = 90f,
                    interiorDoorPos = new Vector3(-20.47f, -200.01f, -8.58f),
                    interiorSpawnPos = new Vector3(-20.47f, -200.00f, -9.5f),
                    interiorSpawnYaw = 180f,
                    entryStatus = "ELEVATOR TO PENTHOUSE ACTIVE...",
                    exitStatus = "DESCENDING TO GROUND LEVEL..."
                }
            };
        }

        private void BuildTriggerVolumes()
        {
            var triggersRoot = new GameObject("[Interior_Portal_Triggers]");
            triggersRoot.transform.SetParent(transform);

            foreach (var portal in portals)
            {
                // 1. Exterior Door Trigger
                var extObj = new GameObject("PortalTrigger_EXT_" + portal.id);
                extObj.transform.SetParent(triggersRoot.transform);
                extObj.transform.position = portal.exteriorDoorPos;
                var extBox = extObj.AddComponent<BoxCollider>();
                extBox.isTrigger = true;
                extBox.size = new Vector3(1.8f, 2.6f, 1.8f);
                var extTrig = extObj.AddComponent<InteriorDoorTrigger>();
                extTrig.portal = portal;
                extTrig.isEntering = true;
                portal.exteriorTriggerObj = extObj;

                // 2. Interior Door Trigger
                var intObj = new GameObject("PortalTrigger_INT_" + portal.id);
                intObj.transform.SetParent(triggersRoot.transform);
                intObj.transform.position = portal.interiorDoorPos;
                var intBox = intObj.AddComponent<BoxCollider>();
                intBox.isTrigger = true;
                intBox.size = new Vector3(1.8f, 2.6f, 1.8f);
                var intTrig = intObj.AddComponent<InteriorDoorTrigger>();
                intTrig.portal = portal;
                intTrig.isEntering = false;
                portal.interiorTriggerObj = intObj;
            }
        }

        /// <summary>
        /// Called when the player physically walks into a door trigger volume.
        /// </summary>
        public void HandleDoorTriggerEnter(InteriorPortal portal, bool isEntering)
        {
            if (_isTeleporting) return;
            if (Time.time - _lastTeleportTime < teleportCooldown) return;

            StartCoroutine(TeleportRoutine(portal, isEntering));
        }

        private IEnumerator TeleportRoutine(InteriorPortal portal, bool enteringInterior)
        {
            _isTeleporting = true;
            _lastTeleportTime = Time.time;

            if (_playerTransform == null) LocatePlayer();
            if (_playerTransform == null)
            {
                _isTeleporting = false;
                yield break;
            }

            // Lock controls
            SetPlayerInputLocked(true);

            // Show Cyberpunk Loading Screen
            string title = enteringInterior ? portal.displayName : "SECTOR 04 — IRON CITY";
            string status = enteringInterior ? portal.entryStatus : portal.exitStatus;
            CyberpunkLoadingScreen.Show(title);
            CyberpunkLoadingScreen.SetStatus(status);

            // Animate loading bar and glitch transition
            float elapsed = 0f;
            bool warped = false;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / transitionDuration);
                CyberpunkLoadingScreen.SetProgress(progress);

                // Execute physical teleport at peak screen coverage (progress >= 0.65)
                if (progress >= 0.65f && !warped)
                {
                    ExecuteWarp(portal, enteringInterior);
                    warped = true;
                }

                yield return null;
            }

            if (!warped)
            {
                ExecuteWarp(portal, enteringInterior);
            }

            // Settle a frame for physics & camera
            yield return null;

            // Fade out loading screen
            CyberpunkLoadingScreen.Hide();

            // Unlock player input
            SetPlayerInputLocked(false);
            _isTeleporting = false;
        }

        private void ExecuteWarp(InteriorPortal portal, bool enteringInterior)
        {
            if (_playerTransform == null) return;

            Vector3 targetPos = enteringInterior ? portal.interiorSpawnPos : portal.exteriorSpawnPos;
            float targetYaw = enteringInterior ? portal.interiorSpawnYaw : portal.exteriorSpawnYaw;

            // Disable CharacterController to prevent PhysX position overriding
            var cc = _playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _playerTransform.position = targetPos;
            _playerTransform.rotation = Quaternion.Euler(0f, targetYaw, 0f);

            // Sync camera
            if (SmoothOrbitCamera.Instance != null)
            {
                SmoothOrbitCamera.Instance.tightPositionFollow = true;
                Vector3 camOffset = Quaternion.Euler(15f, targetYaw, 0f) * new Vector3(0f, 0.40f, -5.2f);
                SmoothOrbitCamera.Instance.transform.position = targetPos + new Vector3(0f, 1.45f, 0f) + camOffset;
                SmoothOrbitCamera.Instance.transform.rotation = Quaternion.Euler(15f, targetYaw, 0f);
            }

            Physics.SyncTransforms();

            if (cc != null) cc.enabled = true;
        }

        private void SetPlayerInputLocked(bool locked)
        {
            if (_playerTransform == null) return;

            var swordLoco = _playerTransform.GetComponent<SyntySwordLocomotionController>();
            if (swordLoco != null) swordLoco.inputLocked = locked;

            var insaneHero = _playerTransform.GetComponent<InsaneGunSwordController>();
            if (insaneHero != null) insaneHero.inputLocked = locked;
        }

        public static void TeleportTo(string portalId, bool toInterior)
        {
            if (Instance == null) return;
            var p = Instance.portals.Find(x => x.id == portalId);
            if (p != null)
            {
                Instance.StartCoroutine(Instance.TeleportRoutine(p, toInterior));
            }
        }

        private void BuildGUIStyles()
        {
            _hudBgTex = new Texture2D(1, 1);
            _hudBgTex.SetPixel(0, 0, new Color(0.02f, 0.05f, 0.08f, 0.88f));
            _hudBgTex.Apply();

            _hudBoxStyle = new GUIStyle
            {
                normal = { background = _hudBgTex },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(16, 16, 8, 8)
            };

            _hudTitleStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.05f, 0.95f, 1f, 1f) }
            };

            _hudSubStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.82f, 0.2f, 0.9f) }
            };
        }

        private void OnGUI()
        {
            if (_isTeleporting) return;
            if (_nearestPortal == null) return;

            string action = _nearestIsInterior 
                ? "Exit to Sector 4 Plaza" 
                : "Enter " + _nearestPortal.displayName;
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", action);
        }

        private void DrawBorder(Rect r, Color color, float width)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - width, r.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, width, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - width, r.y, width, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnDestroy()
        {
            if (_hudBgTex != null) Destroy(_hudBgTex);
        }
    }

    /// <summary>
    /// Trigger component placed on portal doorway colliders.
    /// </summary>
    public class InteriorDoorTrigger : MonoBehaviour
    {
        public InteriorTeleportSystem.InteriorPortal portal;
        public bool isEntering;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") && 
                other.GetComponent<CharacterController>() == null && 
                other.GetComponent<SyntySwordLocomotionController>() == null)
            {
                return;
            }

            if (InteriorTeleportSystem.Instance != null && portal != null)
            {
                InteriorTeleportSystem.Instance.HandleDoorTriggerEnter(portal, isEntering);
            }
        }
    }
}
