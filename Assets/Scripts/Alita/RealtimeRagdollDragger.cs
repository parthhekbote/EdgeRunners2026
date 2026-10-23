using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Enables real-time interactive physics ragdoll on Alita.
    /// Allows the user to click, grab, and toss/drag any limb in real-time with full physical simulation.
    /// </summary>
    public class RealtimeRagdollDragger : MonoBehaviour
    {
        [Header("Physics Drag Settings")]
        public float dragSpring = 250f;
        public float dragDamping = 18f;
        public float dragDistance = 4f;

        [Header("State")]
        public bool ragdollActiveOnStart = true;

        private Rigidbody _grabbedRigidbody;
        private SpringJoint _springJoint;
        private Transform _dragHandle;
        private Camera _cam;

        private void Start()
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindAnyObjectByType<Camera>();

            // Enable dynamic physics on all child ragdoll rigidbodies
            if (ragdollActiveOnStart)
            {
                EnableRealtimePhysics();
            }

            // Create invisible anchor for spring dragging
            GameObject handle = new GameObject("Ragdoll_Drag_Anchor");
            handle.hideFlags = HideFlags.DontSave;
            var handleRb = handle.AddComponent<Rigidbody>();
            handleRb.isKinematic = true;
            _dragHandle = handle.transform;
        }

        public void EnableRealtimePhysics()
        {
            var anim = GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            var rbs = GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                if (rb.transform == transform) continue;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearDamping = 0.8f;
                rb.angularDamping = 1.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                var col = rb.GetComponent<Collider>();
                if (col != null) col.enabled = true;
            }
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // 1. Mouse Click to Grab Bone
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 50f))
                {
                    var rb = hit.rigidbody;
                    if (rb != null && rb.transform.IsChildOf(transform))
                    {
                        GrabBone(rb, hit.point);
                    }
                }
            }

            // 2. Drag Bone with Mouse
            if (Input.GetMouseButton(0) && _grabbedRigidbody != null)
            {
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
                _dragHandle.position = ray.GetPoint(dragDistance);
            }

            // 3. Release Bone
            if (Input.GetMouseButtonUp(0) && _grabbedRigidbody != null)
            {
                ReleaseBone();
            }

            // 4. Quick physics nudge (Spacebar / Right Click)
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space))
            {
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 50f))
                {
                    var rb = hit.rigidbody;
                    if (rb != null && rb.transform.IsChildOf(transform))
                    {
                        rb.AddForce(ray.direction * 300f + Vector3.up * 200f, ForceMode.Impulse);
                    }
                }
            }
        }

        private void GrabBone(Rigidbody targetRb, Vector3 hitPoint)
        {
            _grabbedRigidbody = targetRb;
            dragDistance = Vector3.Distance(_cam.transform.position, hitPoint);
            _dragHandle.position = hitPoint;

            if (_springJoint == null)
            {
                _springJoint = _dragHandle.gameObject.AddComponent<SpringJoint>();
                _springJoint.autoConfigureConnectedAnchor = false;
            }

            _springJoint.connectedBody = _grabbedRigidbody;
            _springJoint.connectedAnchor = _grabbedRigidbody.transform.InverseTransformPoint(hitPoint);
            _springJoint.spring = dragSpring;
            _springJoint.damper = dragDamping;
        }

        private void ReleaseBone()
        {
            if (_springJoint != null)
            {
                Destroy(_springJoint);
                _springJoint = null;
            }
            _grabbedRigidbody = null;
        }

        private void OnDestroy()
        {
            if (_dragHandle != null) Destroy(_dragHandle.gameObject);
        }

        private void OnGUI()
        {
            GUIStyle hintStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hintStyle.normal.textColor = Color.cyan;

            Rect rect = new Rect(15, Screen.height - 75, 420, 32);
            GUI.Box(rect, "[Left Click & Drag] to move Alita's limbs in real-time ragdoll", hintStyle);
        }
    }
}
