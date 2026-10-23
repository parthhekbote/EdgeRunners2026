using UnityEngine;

namespace Cyberpunk.World
{
    /// <summary>
    /// Smooth continuous rotation and hovering bob for the TRON Identity Disc.
    /// </summary>
    public class TronDiscRotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        public float spinSpeed = 35f;
        public Vector3 rotationAxis = Vector3.forward;

        [Header("Hover Bobbing Settings")]
        public float bobAmplitude = 0.08f;
        public float bobFrequency = 1.2f;

        private Vector3 _initialLocalPos;

        private void Awake()
        {
            EnsureChamberColliders();
        }

        public static void EnsureChamberColliders()
        {
            var chamber = GameObject.Find("Clinic_Attached_Tron_Chamber");
            if (chamber == null) return;

            // Ensure solid 0.5m thick floor collider below floor plane (covers 10.5m x 12.1m room area)
            AddOrUpdatePlaneBoxCollider(chamber.transform.Find("Tron_Floor_Obsidian"), 0.5f);

            // Ensure solid wall colliders so player cannot walk through walls
            AddOrUpdatePlaneBoxCollider(chamber.transform.Find("Tron_Wall_North_Back"), 0.5f);
            AddOrUpdatePlaneBoxCollider(chamber.transform.Find("Tron_Wall_West_Back"), 0.5f);
            AddOrUpdatePlaneBoxCollider(chamber.transform.Find("Tron_Wall_East_Back"), 0.5f);
            AddOrUpdatePlaneBoxCollider(chamber.transform.Find("Tron_Wall_South_Left_Back"), 0.5f);
            AddOrUpdatePlaneBoxCollider(chamber.transform.Find("Tron_Wall_South_Right_Back"), 0.5f);

            // Ensure solid ceiling collider
            AddOrUpdatePlaneBoxCollider(chamber.transform.Find("Tron_Ceiling_Obsidian"), 0.5f);
        }

        private static void AddOrUpdatePlaneBoxCollider(Transform planeTransform, float thickness)
        {
            if (planeTransform == null) return;
            var box = planeTransform.GetComponent<BoxCollider>();
            if (box == null) box = planeTransform.gameObject.AddComponent<BoxCollider>();
            // Standard Unity Plane mesh extends -5 to +5 on X and Z (10x10 units)
            box.size = new Vector3(10f, thickness, 10f);
            box.center = new Vector3(0f, -thickness * 0.5f, 0f);
        }

        private void Start()
        {
            _initialLocalPos = transform.localPosition;
        }

        private void Update()
        {
            // Continuous spin around disc axis
            transform.Rotate(rotationAxis, spinSpeed * Time.deltaTime, Space.Self);

            // Gentle quantum levitation bob
            float bobOffset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            transform.localPosition = _initialLocalPos + new Vector3(0f, bobOffset, 0f);
        }
    }
}
