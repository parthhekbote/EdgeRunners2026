using UnityEngine;

namespace WarriorAnims
{
	public class CameraController:MonoBehaviour
	{
		public GameObject cameraTarget;
		public float cameraTargetOffsetY = 1.0f;
		private Vector3 cameraTargetOffset;
		public float rotateSpeed = 1.0f;
		private float rotate;
		public float height = 2.75f;
		public float distance = 1.25f;
		public float zoomAmount = 0.2f;
		public float smoothing = 2.0f;
		private Vector3 offset;
		private bool following = true;
		private Vector3 lastPosition;

		private void Awake()
		{
			if (!cameraTarget) { cameraTarget = GameObject.FindWithTag("Player"); }
		}

		private void Start()
		{
			offset = new Vector3(0f, height, -distance);
			lastPosition = cameraTarget.transform.position + offset;
			transform.position = lastPosition;
		}

		private void Update()
		{
			// Follow cam.
			if (Input.GetKeyDown(KeyCode.F)) {
				if (following) { following = false; }
				else { following = true; }
			}
			if (following) { CameraFollow(); }
			else { transform.position = lastPosition; }

			// Rotate cam.
			if (Input.GetKey(KeyCode.Q)) { rotate = -1; }
			else if (Input.GetKey(KeyCode.E)) { rotate = 1; }
			else { rotate = 0; }

			// Mouse zoom.
			if (Input.mouseScrollDelta.y == 1) { distance += zoomAmount; height += zoomAmount; }
			else if (Input.mouseScrollDelta.y == -1) { distance -= zoomAmount; height -= zoomAmount; }

			// Set cameraTargetOffset as cameraTarget + cameraTargetOffsetY.
			cameraTargetOffset = cameraTarget.transform.position + new Vector3(0, cameraTargetOffsetY, 0);

			// Smoothly look at cameraTargetOffset.
			transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(cameraTargetOffset - transform.position), Time.deltaTime * smoothing);
		}

		private void CameraFollow()
		{
			offset = Quaternion.AngleAxis(rotate * rotateSpeed, Vector3.up) * offset;
			Vector3 targetPos = cameraTarget.transform.position + offset;
			transform.position = Vector3.Lerp(lastPosition, targetPos, smoothing * Time.deltaTime);
		}

		private void LateUpdate()
		{ lastPosition = transform.position; }
	}
}