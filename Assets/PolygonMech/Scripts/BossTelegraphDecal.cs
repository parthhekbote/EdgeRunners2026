using UnityEngine;

namespace PolygonMech
{
    /// <summary>
    /// AAA Holographic Hazard Telegraph Decal projected onto the Martian terrain.
    /// Provides clear visual telegraphing for the Titan Mech's high-altitude Leap Slam:
    /// - Outer boundary hazard ring indicating lethal blast radius
    /// - Inner contracting countdown circle indicating exact millisecond of impact
    /// - Tactical crosshairs intersecting at target ground zero
    /// - Skyward targeting laser beacon
    /// - Dynamic HDR pulse and escalation
    /// </summary>
    public class BossTelegraphDecal : MonoBehaviour
    {
        private float duration = 1.00f;
        private float maxRadius = 5.5f;
        private float elapsed = 0f;

        private LineRenderer outerRing;
        private LineRenderer innerRing;
        private LineRenderer crosshairX;
        private LineRenderer crosshairZ;
        private LineRenderer skyBeacon;
        private Light hazardLight;

        private static Material decalMat;

        public static BossTelegraphDecal Create(Vector3 landingPos, float flightDuration, float radius = 5.5f)
        {
            GameObject go = new GameObject("Boss_LeapSlam_TelegraphDecal");
            landingPos.y = 0.08f;
            go.transform.position = landingPos;

            var decal = go.AddComponent<BossTelegraphDecal>();
            decal.duration = flightDuration;
            decal.maxRadius = radius;
            return decal;
        }

        private void Awake()
        {
            if (decalMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                decalMat = new Material(shader);
                decalMat.SetColor("_BaseColor", new Color(3.0f, 0.4f, 0.1f, 0.95f)); // Glowing HDR Amber-Red
            }

            // 1. Outer Hazard Boundary Ring
            GameObject outerObj = new GameObject("OuterRing");
            outerObj.transform.SetParent(transform, false);
            outerRing = outerObj.AddComponent<LineRenderer>();
            ConfigureLine(outerRing, 48, 0.14f, new Color(2.0f, 0.25f, 0.08f, 0.9f), true);

            // 2. Inner Contracting Countdown Ring
            GameObject innerObj = new GameObject("InnerCountdownRing");
            innerObj.transform.SetParent(transform, false);
            innerRing = innerObj.AddComponent<LineRenderer>();
            ConfigureLine(innerRing, 48, 0.22f, new Color(3.5f, 1.2f, 0.1f, 0.98f), true);

            // 3. Tactical Target Crosshairs
            GameObject crossXObj = new GameObject("CrosshairX");
            crossXObj.transform.SetParent(transform, false);
            crosshairX = crossXObj.AddComponent<LineRenderer>();
            ConfigureLine(crosshairX, 2, 0.10f, new Color(2.5f, 0.4f, 0.1f, 0.8f), false);
            crosshairX.SetPosition(0, new Vector3(-maxRadius, 0.04f, 0f));
            crosshairX.SetPosition(1, new Vector3(maxRadius, 0.04f, 0f));

            GameObject crossZObj = new GameObject("CrosshairZ");
            crossZObj.transform.SetParent(transform, false);
            crosshairZ = crossZObj.AddComponent<LineRenderer>();
            ConfigureLine(crosshairZ, 2, 0.10f, new Color(2.5f, 0.4f, 0.1f, 0.8f), false);
            crosshairZ.SetPosition(0, new Vector3(0f, 0.04f, -maxRadius));
            crosshairZ.SetPosition(1, new Vector3(0f, 0.04f, maxRadius));

            // 4. Skyward Targeting Laser Beacon
            GameObject beaconObj = new GameObject("SkyBeacon");
            beaconObj.transform.SetParent(transform, false);
            skyBeacon = beaconObj.AddComponent<LineRenderer>();
            skyBeacon.material = decalMat;
            skyBeacon.startWidth = 0.35f;
            skyBeacon.endWidth = 0.04f;
            skyBeacon.positionCount = 2;
            skyBeacon.useWorldSpace = false;
            skyBeacon.SetPosition(0, Vector3.zero);
            skyBeacon.SetPosition(1, Vector3.up * 22.0f);
            skyBeacon.startColor = new Color(3.5f, 0.5f, 0.1f, 0.95f);
            skyBeacon.endColor = new Color(3.5f, 0.5f, 0.1f, 0.0f);

            // 5. Ground Illumination Light
            GameObject lightObj = new GameObject("DecalPointLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = Vector3.up * 0.6f;
            hazardLight = lightObj.AddComponent<Light>();
            hazardLight.type = LightType.Point;
            hazardLight.color = new Color(1f, 0.25f, 0.06f);
            hazardLight.range = 10.0f;
            hazardLight.intensity = 3.5f;
        }

        private void Start()
        {
            BuildCircle(outerRing, maxRadius);
            crosshairX.SetPosition(0, new Vector3(-maxRadius, 0.04f, 0f));
            crosshairX.SetPosition(1, new Vector3(maxRadius, 0.04f, 0f));
            crosshairZ.SetPosition(0, new Vector3(0f, 0.04f, -maxRadius));
            crosshairZ.SetPosition(1, new Vector3(0f, 0.04f, maxRadius));
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Inner ring contracts from maxRadius down to 0 at landing
            float currentInnerRadius = Mathf.Lerp(maxRadius, 0.08f, t);
            BuildCircle(innerRing, currentInnerRadius);

            // Outer ring pulses faster as time runs out
            float pulse = Mathf.PingPong(Time.time * (5f + t * 15f), 1f);
            Color outerCol = Color.Lerp(new Color(2.0f, 0.25f, 0.08f, 0.85f), new Color(4.0f, 0.8f, 0.15f, 1.0f), pulse);
            outerRing.startColor = outerCol;
            outerRing.endColor = outerCol;

            // Inner countdown ring intensifies to white-hot at impact
            Color innerCol = Color.Lerp(new Color(2.5f, 0.9f, 0.1f, 0.95f), new Color(5.0f, 2.0f, 0.8f, 1.0f), t);
            innerRing.startColor = innerCol;
            innerRing.endColor = innerCol;

            // Crosshair pulse
            Color crossCol = Color.Lerp(new Color(2.5f, 0.4f, 0.1f, 0.7f), new Color(4.5f, 1.0f, 0.2f, 1.0f), t);
            crosshairX.startColor = crossCol;
            crosshairX.endColor = crossCol;
            crosshairZ.startColor = crossCol;
            crosshairZ.endColor = crossCol;

            if (hazardLight != null)
            {
                hazardLight.intensity = Mathf.Lerp(2.5f, 7.5f, t) + pulse * 2.0f;
            }

            if (elapsed >= duration + 0.10f)
            {
                Destroy(gameObject);
            }
        }

        private void ConfigureLine(LineRenderer lr, int segments, float width, Color col, bool loop)
        {
            lr.material = decalMat;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.positionCount = segments;
            lr.startColor = col;
            lr.endColor = col;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        private void BuildCircle(LineRenderer lr, float radius)
        {
            int segments = lr.positionCount;
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * angleStep);
                float x = Mathf.Cos(rad) * radius;
                float z = Mathf.Sin(rad) * radius;
                lr.SetPosition(i, new Vector3(x, 0.04f, z));
            }
        }
    }
}
