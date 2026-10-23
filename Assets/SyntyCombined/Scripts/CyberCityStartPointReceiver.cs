using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Receives the selected spawn sector index set by MainMenuController
    /// and positions the player at the corresponding coordinates in Demo_City.
    /// </summary>
    public class CyberCityStartPointReceiver : MonoBehaviour
    {
        [Header("Player Reference")]
        public Transform playerTransform;

        [Header("Sector Spawn Coordinates in Cyber City")]
        public Vector3 plazaSpawnPosition = new Vector3(19.00f, -17.98f, 49.50f);
        public Vector3 alleySpawnPosition = new Vector3(35f, -15.5f, 20f);
        public Vector3 towerSpawnPosition = new Vector3(-20f, -15.5f, 60f);

        private void Start()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name.IndexOf("Demo_City", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                activeScene.name.IndexOf("CyberCity", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.Log($"[CyberCityReceiver] Skipping sector spawn override for non-city scene '{activeScene.name}' - preserving scene spawn position.");
                return;
            }

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null)
                {
                    SyntySwordLocomotionController controller = Object.FindAnyObjectByType<SyntySwordLocomotionController>();
                    if (controller != null) player = controller.gameObject;
                }

                if (player != null) playerTransform = player.transform;
            }

            int sectorIndex = PlayerPrefs.GetInt(MainMenuController.PREF_SELECTED_SECTOR, 0);
            Vector3 targetSpawn = plazaSpawnPosition;

            switch (sectorIndex)
            {
                case 0: targetSpawn = plazaSpawnPosition; break;
                case 1: targetSpawn = alleySpawnPosition; break;
                case 2: targetSpawn = towerSpawnPosition; break;
            }

            // Near-Ido spawn: sector 0 parks Alita right outside Dr. Ido's cyber clinic
            // (19.00,-17.98,49.50) facing Dr. Ido (18.50,-17.98,51.00).
            // Runtime-only; other sectors keep their explicit menu choice.
            Vector3 idoSpot = new Vector3(18.50f, -17.98f, 51.00f);
            bool faceIdo = false;
            if (sectorIndex == 0)
            {
                targetSpawn = new Vector3(19.00f, -17.98f, 49.50f);
                faceIdo = true;
            }

            if (Cyberpunk.World.ClinicDoorTrigger.PendingSpawnPos.HasValue)
            {
                targetSpawn = Cyberpunk.World.ClinicDoorTrigger.PendingSpawnPos.Value;
                faceIdo = false;
            }

            // Raycast downward to ensure exact ground contact
            Vector3 rayStart = new Vector3(targetSpawn.x, targetSpawn.y + 15f, targetSpawn.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 40f))
            {
                targetSpawn.y = hit.point.y + 0.08f;
            }

            if (playerTransform != null)
            {
                CharacterController cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerTransform.position = targetSpawn;

                if (cc != null) cc.enabled = true;

                if (faceIdo)
                {
                    Vector3 toIdo = idoSpot - playerTransform.position;
                    toIdo.y = 0f;
                    if (toIdo.sqrMagnitude > 0.01f)
                        playerTransform.rotation = Quaternion.LookRotation(toIdo.normalized);
                }

                InsaneGun.Combat.SmoothOrbitCamera.Instance?.SnapToTarget();

                Debug.Log($"[CyberCityReceiver] Spawned player at Sector {sectorIndex} ({targetSpawn})" + (faceIdo ? " beside Dr. Ido, facing him." : ""));
            }
        }
    }
}
