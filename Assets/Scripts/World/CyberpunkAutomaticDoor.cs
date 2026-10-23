using System;
using System.Collections.Generic;
using UnityEngine;
using PolygonMech;

namespace Cyberpunk.World
{
    public enum DoorMovementStyle
    {
        SlideHorizontal,
        SlideVerticalSplit,
        SlideVerticalUp,
        HingeRotation,
        CanopyGullWing
    }

    [Serializable]
    public class DoorPanelConfig
    {
        public string panelName;
        public Transform panelTransform;
        public Vector3 closedLocalPos;
        public Vector3 openLocalPos;
        public Quaternion closedLocalRot;
        public Quaternion openLocalRot;
        public Collider panelCollider;
        public bool disableColliderWhenOpen = true;
    }

    /// <summary>
    /// Autonomous proximity-activated cybernetic pneumatic sliding door controller.
    /// Automatically senses approaching player, smoothly glides open with hydraulic easing,
    /// plays pneumatic whoosh / mechanical latch SFX, and safely passes the player through.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class CyberpunkAutomaticDoor : MonoBehaviour
    {
        [Header("Proximity Activation Settings")]
        [Tooltip("Distance from door center at which the door begins opening.")]
        public float openDistance = 7.5f;
        [Tooltip("Distance at which the door begins closing (hysteresis).")]
        public float closeDistance = 10.0f;
        [Tooltip("Movement interpolation speed.")]
        public float openSpeed = 5.5f;

        [Header("Door State")]
        [SerializeField] private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        [Header("Movement Architecture")]
        public DoorMovementStyle movementStyle = DoorMovementStyle.SlideHorizontal;
        public List<DoorPanelConfig> panels = new List<DoorPanelConfig>();

        [Header("Root Frame Collision Management")]
        [Tooltip("Root frame collider (e.g. Synty solid collision meshes). Disabled to allow completely unhindered passage through the doorway.")]
        public Collider rootFrameCollider;
        public bool disableRootFrameColliderAlways = true;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip sfxDoorOpen;
        public AudioClip sfxDoorClose;
        [Range(0f, 1f)] public float soundVolume = 0.65f;

        [Header("Visual Feedback (Optional)")]
        public Renderer statusLightRenderer;
        public Color lockedColor = new Color(1.5f, 0.2f, 0.1f);
        public Color accessGrantedColor = new Color(0.1f, 1.8f, 1.5f);

        // Runtime state
        private Transform _playerTransform;
        private float _currentProgress = 0f; // 0 = Closed, 1 = Fully Open
        private bool _wasOpenLastFrame = false;
        private float _lastPlayerCheckTime = 0f;

        public Vector3 GetDoorWorldCenter()
        {
            if (panels != null && panels.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < panels.Count; i++)
                {
                    if (panels[i].panelTransform != null)
                    {
                        sum += panels[i].panelTransform.position;
                        count++;
                    }
                }
                if (count > 0) return sum / count;
            }
            return transform.position;
        }

        private void Awake()
        {
            EnsureAudioSource();
            LocatePlayer();
            ValidatePanels();
        }

        private void OnEnable()
        {
            LocatePlayer();
            ValidatePanels();
        }

        private void Start()
        {
            // Snap to closed state initially
            _currentProgress = 0f;
            ApplyPanelTransforms(0f);
        }

        private void Update()
        {
            // Verify player reference if null or inactive
            if (_playerTransform == null || !_playerTransform.gameObject.activeInHierarchy)
            {
                LocatePlayer();
                if (_playerTransform == null) return;
            }

            // Proximity detection using 2D horizontal distance to actual door opening center
            Vector3 doorCenter = GetDoorWorldCenter();
            Vector3 playerPos = _playerTransform.position;

            float dx = doorCenter.x - playerPos.x;
            float dz = doorCenter.z - playerPos.z;
            float horizDist = Mathf.Sqrt(dx * dx + dz * dz);
            float vertDist = Mathf.Abs(doorCenter.y - playerPos.y);

            // Allow generous vertical tolerance of up to 6.0m for ramps, catwalks, stairs and multi-level plazas
            if (vertDist <= 6.0f)
            {
                if (!_isOpen && horizDist <= openDistance)
                {
                    SetDoorOpen(true);
                }
                else if (_isOpen && horizDist >= closeDistance)
                {
                    SetDoorOpen(false);
                }
            }
            else if (_isOpen && (horizDist >= closeDistance || vertDist > 7.0f))
            {
                SetDoorOpen(false);
            }

            // Animate progress smoothly
            float target = _isOpen ? 1.0f : 0.0f;
            if (!Mathf.Approximately(_currentProgress, target))
            {
                _currentProgress = Mathf.MoveTowards(_currentProgress, target, openSpeed * Time.deltaTime);
                float smooth = Mathf.SmoothStep(0f, 1f, _currentProgress);
                ApplyPanelTransforms(smooth);
            }
        }

        public void SetDoorOpen(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;

            PlayDoorAudio(open);
            UpdateStatusLight(open);
        }

        public void PreviewOpenState(float progress)
        {
            _currentProgress = Mathf.Clamp01(progress);
            _isOpen = _currentProgress > 0.5f;
            float smooth = Mathf.SmoothStep(0f, 1f, _currentProgress);
            ApplyPanelTransforms(smooth);
        }

        private void ApplyPanelTransforms(float t)
        {
            // Disable root frame collider whenever the door is open or if configured to always disable
            if (rootFrameCollider != null)
            {
                bool rootActive = !disableRootFrameColliderAlways && t < 0.15f;
                if (rootFrameCollider.enabled != rootActive)
                {
                    rootFrameCollider.enabled = rootActive;
                }
            }

            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                if (p.panelTransform == null) continue;

                // Interpolate position and rotation
                p.panelTransform.localPosition = Vector3.Lerp(p.closedLocalPos, p.openLocalPos, t);
                p.panelTransform.localRotation = Quaternion.Slerp(p.closedLocalRot, p.openLocalRot, t);

                // Disable collider as soon as the door starts opening (t >= 0.20f) so player never snags or bumps into opening leaves
                if (p.panelCollider != null && p.disableColliderWhenOpen)
                {
                    bool colliderActive = t < 0.20f;
                    if (p.panelCollider.enabled != colliderActive)
                    {
                        p.panelCollider.enabled = colliderActive;
                    }
                }
            }
        }

        private void PlayDoorAudio(bool opening)
        {
            EnsureAudioSource();
            if (audioSource == null) return;

            AudioClip clip = opening ? sfxDoorOpen : sfxDoorClose;
            if (clip == null)
            {
                // Procedural pneumatic release sound from MechAudioSynth!
                clip = MechAudioSynth.GetPneumaticRelease();
            }

            if (clip != null)
            {
                audioSource.pitch = opening ? UnityEngine.Random.Range(1.05f, 1.15f) : UnityEngine.Random.Range(0.85f, 0.95f);
                audioSource.PlayOneShot(clip, soundVolume);
            }
        }

        private void UpdateStatusLight(bool opening)
        {
            if (statusLightRenderer == null) return;
            var mat = statusLightRenderer.material;
            if (mat != null)
            {
                Color targetCol = opening ? accessGrantedColor : lockedColor;
                mat.SetColor("_EmissionColor", targetCol);
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.spatialBlend = 0.90f;
                audioSource.minDistance = 2.0f;
                audioSource.maxDistance = 18.0f;
                audioSource.playOnAwake = false;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }
        }

        private void LocatePlayer()
        {
            if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy)
            {
                return;
            }

            var alita = GameObject.Find("Alita_BattleAngel_Hero");
            if (alita != null && alita.activeInHierarchy)
            {
                _playerTransform = alita.transform;
                return;
            }

            var p = GameObject.FindWithTag("Player");
            if (p != null && p.activeInHierarchy)
            {
                _playerTransform = p.transform;
                return;
            }

            var cc = UnityEngine.Object.FindAnyObjectByType<CharacterController>();
            if (cc != null && cc.gameObject.activeInHierarchy)
            {
                _playerTransform = cc.transform;
            }
        }

        private void ValidatePanels()
        {
            // Cache and disable root frame collider so it doesn't block the doorway passage
            if (rootFrameCollider == null)
            {
                rootFrameCollider = GetComponent<Collider>();
            }
            if (rootFrameCollider != null && disableRootFrameColliderAlways)
            {
                rootFrameCollider.enabled = false;
            }

            // For Single_04 doors, Door_03 is the decorative top header frame whose convex mesh collider
            // extends down into the doorway passage (Y=1.16m to 2.12m), acting as an invisible concrete barrier.
            // Ensure its collider is permanently disabled so passage is always completely clear.
            var door03 = transform.Find("SM_Bld_Door_Single_04_Door_03");
            if (door03 != null)
            {
                var col03 = door03.GetComponent<Collider>();
                if (col03 != null)
                {
                    col03.enabled = false;
                }
            }

            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                if (p.panelTransform != null)
                {
                    if (p.panelName != null && p.panelName.Contains("Door_03"))
                    {
                        // Ensure Door_03 collider is never enabled or re-enabled
                        p.panelCollider = null;
                    }
                    else if (p.panelCollider == null)
                    {
                        p.panelCollider = p.panelTransform.GetComponent<Collider>();
                    }
                }
            }
        }

        [ContextMenu("Auto-Configure Panels From Assembly")]
        public void AutoConfigurePanels()
        {
            panels.Clear();
            string n = gameObject.name;

            // 1. Single_01 & Single_05: Single horizontal sliding panel
            if (n.Contains("Single_01") || n.Contains("Single_05"))
            {
                movementStyle = DoorMovementStyle.SlideHorizontal;
                Transform panel = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("_Door_01")) { panel = ch; break; }
                }

                if (panel != null)
                {
                    float slideOffset = n.Contains("Single_05") ? 1.60f : 1.50f;
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = panel.name,
                        panelTransform = panel,
                        closedLocalPos = panel.localPosition,
                        openLocalPos = panel.localPosition + Vector3.right * slideOffset,
                        closedLocalRot = panel.localRotation,
                        openLocalRot = panel.localRotation,
                        panelCollider = panel.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 2. Single_02 & Single_03: Vertical split blast doors
            else if (n.Contains("Single_02") || n.Contains("Single_03"))
            {
                movementStyle = DoorMovementStyle.SlideVerticalSplit;
                Transform p1 = null, p2 = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("_Door_01")) p1 = ch;
                    else if (ch.name.Contains("_Door_02")) p2 = ch;
                }

                if (p1 != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p1.name,
                        panelTransform = p1,
                        closedLocalPos = p1.localPosition,
                        openLocalPos = p1.localPosition + Vector3.down * 1.40f,
                        closedLocalRot = p1.localRotation,
                        openLocalRot = p1.localRotation,
                        panelCollider = p1.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
                if (p2 != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p2.name,
                        panelTransform = p2,
                        closedLocalPos = p2.localPosition,
                        openLocalPos = p2.localPosition + Vector3.up * 1.40f,
                        closedLocalRot = p2.localRotation,
                        openLocalRot = p2.localRotation,
                        panelCollider = p2.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 3. Single_04: Double sliding panel with vertical sliding top hatch
            else if (n.Contains("Single_04"))
            {
                movementStyle = DoorMovementStyle.SlideHorizontal;
                Transform p1 = null, p2 = null, p3 = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("_Door_01")) p1 = ch;
                    else if (ch.name.Contains("_Door_02")) p2 = ch;
                    else if (ch.name.Contains("_Door_03")) p3 = ch;
                }

                if (p1 != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p1.name,
                        panelTransform = p1,
                        closedLocalPos = p1.localPosition,
                        openLocalPos = p1.localPosition + Vector3.right * 1.10f,
                        closedLocalRot = p1.localRotation,
                        openLocalRot = p1.localRotation,
                        panelCollider = p1.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
                if (p2 != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p2.name,
                        panelTransform = p2,
                        closedLocalPos = p2.localPosition,
                        openLocalPos = p2.localPosition + Vector3.left * 1.10f,
                        closedLocalRot = p2.localRotation,
                        openLocalRot = p2.localRotation,
                        panelCollider = p2.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
                if (p3 != null)
                {
                    var c3 = p3.GetComponent<Collider>();
                    if (c3 != null) c3.enabled = false;

                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p3.name,
                        panelTransform = p3,
                        closedLocalPos = p3.localPosition,
                        openLocalPos = p3.localPosition + Vector3.up * 1.85f,
                        closedLocalRot = p3.localRotation,
                        openLocalRot = p3.localRotation,
                        panelCollider = null,
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 4. Double_04: Double wide sliding doors
            else if (n.Contains("Double_04"))
            {
                movementStyle = DoorMovementStyle.SlideHorizontal;
                Transform p1 = null, p2 = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("_Door_01")) p1 = ch;
                    else if (ch.name.Contains("_Door_02")) p2 = ch;
                }

                if (p1 != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p1.name,
                        panelTransform = p1,
                        closedLocalPos = p1.localPosition,
                        openLocalPos = p1.localPosition + Vector3.left * 2.0f,
                        closedLocalRot = p1.localRotation,
                        openLocalRot = p1.localRotation,
                        panelCollider = p1.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
                if (p2 != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p2.name,
                        panelTransform = p2,
                        closedLocalPos = p2.localPosition,
                        openLocalPos = p2.localPosition + Vector3.right * 2.0f,
                        closedLocalRot = p2.localRotation,
                        openLocalRot = p2.localRotation,
                        panelCollider = p2.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 5. Vendor 02: Dual sliding shutter panels
            else if (n.Contains("Vendor_02"))
            {
                movementStyle = DoorMovementStyle.SlideHorizontal;
                Transform p1 = null, p2 = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("_Door_01")) p1 = ch;
                    else if (ch.name.Contains("_Door_02")) p2 = ch;
                }

                if (p1 != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p1.name,
                        panelTransform = p1,
                        closedLocalPos = p1.localPosition,
                        openLocalPos = p1.localPosition + Vector3.right * 0.65f,
                        closedLocalRot = p1.localRotation,
                        openLocalRot = p1.localRotation,
                        panelCollider = p1.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
                if (p2 != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = p2.name,
                        panelTransform = p2,
                        closedLocalPos = p2.localPosition,
                        openLocalPos = p2.localPosition + Vector3.left * 0.65f,
                        closedLocalRot = p2.localRotation,
                        openLocalRot = p2.localRotation,
                        panelCollider = p2.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 6. Vendor 01, 05, 07: Single sliding or swinging door
            else if (n.Contains("Vendor_01") || n.Contains("Vendor_05") || n.Contains("Vendor_07"))
            {
                Transform panel = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("Door_01")) { panel = ch; break; }
                }

                if (panel != null)
                {
                    if (n.Contains("Vendor_05") || n.Contains("Vendor_07"))
                    {
                        movementStyle = DoorMovementStyle.HingeRotation;
                        panels.Add(new DoorPanelConfig
                        {
                            panelName = panel.name,
                            panelTransform = panel,
                            closedLocalPos = panel.localPosition,
                            openLocalPos = panel.localPosition,
                            closedLocalRot = panel.localRotation,
                            openLocalRot = panel.localRotation * Quaternion.Euler(0f, -95f, 0f),
                            panelCollider = panel.GetComponent<Collider>(),
                            disableColliderWhenOpen = true
                        });
                    }
                    else
                    {
                        movementStyle = DoorMovementStyle.SlideHorizontal;
                        panels.Add(new DoorPanelConfig
                        {
                            panelName = panel.name,
                            panelTransform = panel,
                            closedLocalPos = panel.localPosition,
                            openLocalPos = panel.localPosition + Vector3.right * 1.35f,
                            closedLocalRot = panel.localRotation,
                            openLocalRot = panel.localRotation,
                            panelCollider = panel.GetComponent<Collider>(),
                            disableColliderWhenOpen = true
                        });
                    }
                }
            }
            // 7. Loading Bay Cargo Door: Vertical slide up
            else if (n.Contains("Loading_Bay"))
            {
                movementStyle = DoorMovementStyle.SlideVerticalUp;
                Transform panel = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("Cargo_Door")) { panel = ch; break; }
                }

                if (panel != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = panel.name,
                        panelTransform = panel,
                        closedLocalPos = panel.localPosition,
                        openLocalPos = panel.localPosition + Vector3.up * 2.80f,
                        closedLocalRot = panel.localRotation,
                        openLocalRot = panel.localRotation,
                        panelCollider = panel.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 8. Shipping Container: Hinge swing open
            else if (n.Contains("Container_01"))
            {
                movementStyle = DoorMovementStyle.HingeRotation;
                Transform panel = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("Door_01")) { panel = ch; break; }
                }

                if (panel != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = panel.name,
                        panelTransform = panel,
                        closedLocalPos = panel.localPosition,
                        openLocalPos = panel.localPosition,
                        closedLocalRot = panel.localRotation,
                        openLocalRot = panel.localRotation * Quaternion.Euler(0f, -110f, 0f),
                        panelCollider = panel.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 9. Starship Hovercraft Canopy Door: Gull-wing upward tilt
            else if (n.Contains("Starship") || n.Contains("HoverCraft"))
            {
                movementStyle = DoorMovementStyle.CanopyGullWing;
                Transform panel = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("Door_01")) { panel = ch; break; }
                }

                if (panel != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = panel.name,
                        panelTransform = panel,
                        closedLocalPos = panel.localPosition,
                        openLocalPos = panel.localPosition + Vector3.up * 0.4f,
                        closedLocalRot = panel.localRotation,
                        openLocalRot = panel.localRotation * Quaternion.Euler(-55f, 0f, 0f),
                        panelCollider = panel.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 10. Vendor Tug Vehicle Door: Van door swing
            else if (n.Contains("Vendor_Tug"))
            {
                movementStyle = DoorMovementStyle.HingeRotation;
                Transform panel = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("Door_01")) { panel = ch; break; }
                }

                if (panel != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = panel.name,
                        panelTransform = panel,
                        closedLocalPos = panel.localPosition,
                        openLocalPos = panel.localPosition,
                        closedLocalRot = panel.localRotation,
                        openLocalRot = panel.localRotation * Quaternion.Euler(0f, 90f, 0f),
                        panelCollider = panel.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 11. Wall Unit Cabinet Door: Hinge drop down
            else if (n.Contains("Wall_Unit"))
            {
                movementStyle = DoorMovementStyle.HingeRotation;
                Transform panel = null;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("Door_01")) { panel = ch; break; }
                }

                if (panel != null)
                {
                    panels.Add(new DoorPanelConfig
                    {
                        panelName = panel.name,
                        panelTransform = panel,
                        closedLocalPos = panel.localPosition,
                        openLocalPos = panel.localPosition,
                        closedLocalRot = panel.localRotation,
                        openLocalRot = panel.localRotation * Quaternion.Euler(-90f, 0f, 0f),
                        panelCollider = panel.GetComponent<Collider>(),
                        disableColliderWhenOpen = true
                    });
                }
            }
            // 12. Fallback: Any generic door leaf found among children
            if (panels.Count == 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.ToLower().Contains("door") || ch.name.ToLower().Contains("panel") || ch.name.ToLower().Contains("shutter"))
                    {
                        panels.Add(new DoorPanelConfig
                        {
                            panelName = ch.name,
                            panelTransform = ch,
                            closedLocalPos = ch.localPosition,
                            openLocalPos = ch.localPosition + Vector3.right * 1.4f,
                            closedLocalRot = ch.localRotation,
                            openLocalRot = ch.localRotation,
                            panelCollider = ch.GetComponent<Collider>(),
                            disableColliderWhenOpen = true
                        });
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isOpen ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, openDistance);
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, closeDistance);
        }
    }
}
