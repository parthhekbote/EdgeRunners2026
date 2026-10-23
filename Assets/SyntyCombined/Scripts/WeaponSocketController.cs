using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Modular weapon socketing controller for humanoid characters and NPCs.
    /// Supports dynamic attachment of any CyberCity sword (SM_Wep_Sword_01 to 08) and sheath.
    /// Handles drawn vs sheathed states, bone auto-detection, and collider safety without hardcoding.
    /// </summary>
    public class WeaponSocketController : MonoBehaviour
    {
        [Header("Weapon Prefabs (CyberCity Swords 01-08)")]
        [Tooltip("The sword weapon prefab to spawn (e.g. SM_Wep_Sword_01 through 08).")]
        public GameObject swordPrefab;
        [Tooltip("Optional sheath prefab (e.g. SM_Wep_Sword_01_Sheath_01).")]
        public GameObject sheathPrefab;

        [Header("Hand Socket Offsets (Configurable per weapon/character)")]
        [Tooltip("Local position offset when attached to hand/Prop_R. Zero when Prop_R bone is present.")]
        public Vector3 handOffset = new Vector3(0f, 0.1f, 0.01f);
        [Tooltip("Local rotation offset when attached to hand/Prop_R. Zero when Prop_R bone is present.")]
        public Vector3 handRotation = new Vector3(0f, 180f, 90f);

        [Header("Hip Socket Offsets (Configurable per weapon/character)")]
        [Tooltip("Local position offset when attached to hip/pelvis.")]
        public Vector3 hipOffset = new Vector3(0.18f, -0.05f, -0.08f);
        [Tooltip("Local rotation offset when attached to hip/pelvis.")]
        public Vector3 hipRotation = new Vector3(-10f, 15f, 110f);

        [Header("Weapon Scale")]
        public Vector3 weaponScale = Vector3.one;

        [Header("Bone References (Optional - Auto-detected if unassigned)")]
        public Transform handBone;
        public Transform hipBone;

        [Header("State")]
        [SerializeField] private bool isDrawn = true;

        // Runtime instances
        private GameObject _spawnedSword;
        private GameObject _spawnedSheath;
        private Transform _handSocket;
        private Transform _hipSocket;

        public GameObject SpawnedSword => _spawnedSword;
        public GameObject SpawnedSheath => _spawnedSheath;
        public bool IsDrawn => isDrawn;

        private void Awake()
        {
            InitializeSockets();
            SpawnWeapons();
            SetDrawn(isDrawn);
        }

        /// <summary>
        /// Destroys and re-creates weapon sockets + instances. Call this if socket offsets
        /// are changed at runtime (e.g. after prefab setup in editor scripts).
        /// </summary>
        public void Reinitialize()
        {
            if (_spawnedSword  != null) { Destroy(_spawnedSword);  _spawnedSword  = null; }
            if (_spawnedSheath != null) { Destroy(_spawnedSheath); _spawnedSheath = null; }
            _handSocket = null;
            _hipSocket  = null;
            InitializeSockets();
            SpawnWeapons();
            SetDrawn(isDrawn);
        }

        /// <summary>
        /// Auto-locates hand and hip bones if not manually assigned in the inspector.
        /// Checks Humanoid avatar first, then common rig naming conventions.
        /// </summary>
        public void InitializeSockets()
        {
            var anim = GetComponentInChildren<Animator>();

            // 1. Locate Hand Bone
            if (handBone == null && anim != null && anim.isHuman)
            {
                handBone = anim.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (handBone == null)
            {
                handBone = FindChildRecursive(transform, "Prop_R")
                        ?? FindChildRecursive(transform, "Weapon_R")
                        ?? FindChildRecursive(transform, "Hand_R")
                        ?? FindChildRecursive(transform, "hand_r")
                        ?? FindChildRecursive(transform, "mixamorig:RightHand");
            }

            // Check for dedicated Prop_R child under right hand if present
            Transform propChild = handBone != null ? (handBone.Find("Prop_R") ?? FindChildRecursive(handBone, "Prop_R")) : null;
            Transform handParent = propChild != null ? propChild : handBone;

            // 2. Locate Hip Bone
            if (hipBone == null && anim != null && anim.isHuman)
            {
                hipBone = anim.GetBoneTransform(HumanBodyBones.Hips);
            }
            if (hipBone == null)
            {
                hipBone = FindChildRecursive(transform, "pelvis")
                       ?? FindChildRecursive(transform, "Pelvis")
                       ?? FindChildRecursive(transform, "Hips")
                       ?? FindChildRecursive(transform, "hips")
                       ?? FindChildRecursive(transform, "mixamorig:Hips")
                       ?? transform;
            }

            // 3. Create or reuse socket transforms
            if (handParent != null && _handSocket == null)
            {
                Vector3 pos = propChild != null ? Vector3.zero : handOffset;
                Vector3 rot = propChild != null ? Vector3.zero : handRotation;
                _handSocket = GetOrCreateSocket(handParent, "Socket_RHand", pos, rot);
            }

            if (hipBone != null && _hipSocket == null)
            {
                _hipSocket = GetOrCreateSocket(hipBone, "Socket_Hip", hipOffset, hipRotation);
            }
        }

        private Transform GetOrCreateSocket(Transform parent, string socketName, Vector3 localPos, Vector3 localRot)
        {
            Transform existing = parent.Find(socketName);
            if (existing != null) return existing;

            GameObject socketGO = new GameObject(socketName);
            socketGO.transform.SetParent(parent, false);
            socketGO.transform.localPosition = localPos;
            socketGO.transform.localEulerAngles = localRot;
            socketGO.transform.localScale = Vector3.one;
            return socketGO.transform;
        }

        /// <summary>
        /// Spawns the sword and optional sheath, disabling physics colliders so they don't interfere with the CharacterController.
        /// </summary>
        public void SpawnWeapons()
        {
            // Destroy existing instances if respawning
            if (_spawnedSword != null) Destroy(_spawnedSword);
            if (_spawnedSheath != null) Destroy(_spawnedSheath);

            if (swordPrefab != null)
            {
                _spawnedSword = Instantiate(swordPrefab);
                _spawnedSword.name = swordPrefab.name + "_Instance";
                _spawnedSword.transform.localScale = weaponScale;
                DisablePhysicsColliders(_spawnedSword);
            }

            if (sheathPrefab != null && _hipSocket != null)
            {
                _spawnedSheath = Instantiate(sheathPrefab, _hipSocket);
                _spawnedSheath.name = sheathPrefab.name + "_Instance";
                _spawnedSheath.transform.localPosition = Vector3.zero;
                _spawnedSheath.transform.localRotation = Quaternion.identity;
                _spawnedSheath.transform.localScale = weaponScale;
                DisablePhysicsColliders(_spawnedSheath);
            }
        }

        /// <summary>
        /// Switches weapon parent between hand socket and hip sheath socket.
        /// </summary>
        public void SetDrawn(bool drawn)
        {
            isDrawn = drawn;
            if (_spawnedSword == null) return;

            Transform targetSocket = drawn ? _handSocket : _hipSocket;
            if (targetSocket != null)
            {
                _spawnedSword.transform.SetParent(targetSocket, false);
                _spawnedSword.transform.localPosition = Vector3.zero;
                _spawnedSword.transform.localRotation = Quaternion.identity;
                _spawnedSword.transform.localScale = weaponScale;
            }
        }

        /// <summary>
        /// Disables physics colliders on weapon meshes to prevent jitter with CharacterController.
        /// </summary>
        private void DisablePhysicsColliders(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                // Disable solid colliders so they don't push the character
                colliders[i].enabled = false;
            }
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase)) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildRecursive(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (_handSocket != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_handSocket.position, 0.05f);
                Gizmos.DrawRay(_handSocket.position, _handSocket.forward * 0.3f);
            }
            if (_hipSocket != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_hipSocket.position, 0.05f);
                Gizmos.DrawRay(_hipSocket.position, _hipSocket.forward * 0.3f);
            }
        }
    }
}
