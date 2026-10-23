using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    public static PlayerCamera instance;
    public PlayerManager player;
    public Camera cameraObject;
    [SerializeField] Transform cameraPivotTransform;


    [Header("Camera Settings")]
    //Camera movement
    private float cameraSmoothSpeed = 1.0f; //The bigger, the longer for the camera to move. Smooths movement.
    [SerializeField] float leftAndRightRotationSpeed = 220;
    [SerializeField] float upAndDownRotationSpeed = 220;
    [SerializeField] float minimumPivot = -30; //Lowest you can look down
    [SerializeField] float maximumPivot = 70; //Highest you can look up
    //Camera collision
    [SerializeField] float cameraColisionRadius = 0.2f;
    [SerializeField] LayerMask collideWithLayers;


    [Header("Camera Values")]
    //Camera movement
    private Vector3 cameraVelovity;
    [SerializeField] float leftAndRightLookAngle;
    [SerializeField] float upAndDownLookAngle;
    //Camera collision
    private Vector3 cameraObjectPosition;       //Moves the camera object to this position upon colliding
    private float cameraZPosition;
    private float targetCameraZPosition;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        cameraZPosition = cameraObject.transform.localPosition.z;
    }

    public void HandleAllCameraMovement()
    {
        if (player != null)
        {
            HandleFollowTarget();
            HandleRotations();
            HandleCollisions();
        }
    }

    private void HandleFollowTarget()
    {
        Vector3 targetCameraPosition = Vector3.SmoothDamp
            (transform.position, player.transform.position,
            ref cameraVelovity, cameraSmoothSpeed * Time.deltaTime);
        transform.position = targetCameraPosition;
    }

    private void HandleRotations()
    {
        //Locked on Rotation

        //Normal Rotation
        //Horizontal rotation using joystick using joystick
        leftAndRightLookAngle += (PlayerInputManager.instance.cameraHorizontalInput * leftAndRightRotationSpeed) * Time.deltaTime;
        //Vertical Rotation using joystick
        upAndDownLookAngle -= (PlayerInputManager.instance.cameraVerticalInput * upAndDownRotationSpeed) * Time.deltaTime;
        //Clamping the vertical angles based on the pivots
        upAndDownLookAngle = Mathf.Clamp(upAndDownLookAngle, minimumPivot, maximumPivot);

        Quaternion targetRotation;
        Vector3 cameraRotation = Vector3.zero;

        //Horizontal rotation. This game object rotates
        cameraRotation.y = leftAndRightLookAngle;
        targetRotation = Quaternion.Euler(cameraRotation);
        transform.rotation = targetRotation;

        //Vertical Rotation. The pivot object rotates
        cameraRotation = Vector3.zero;
        cameraRotation.x = upAndDownLookAngle;
        targetRotation = Quaternion.Euler(cameraRotation);
        cameraPivotTransform.localRotation = targetRotation;
    }

    private void HandleCollisions()
    {
        targetCameraZPosition = cameraZPosition;

        RaycastHit hit;
        //Direction for the collision test
        Vector3 direction = cameraObject.transform.position - cameraPivotTransform.position;
        direction.Normalize();

        //Check if there's an object in front of the desired direction
        if (Physics.SphereCast
            (cameraPivotTransform.position, cameraColisionRadius,
            direction, out hit, Mathf.Abs(targetCameraZPosition), collideWithLayers))
        {
            //Then measure the camera's distance from it
            float distanceFromHitObject = Vector3.Distance(cameraPivotTransform.position, hit.point);
            //Then equates the camera's target Z position to the following
            targetCameraZPosition = -(distanceFromHitObject - cameraColisionRadius);
        }

        //If the camera's target position is less than the collision radius, subtract the collision radius (making it snap back)
        if (Mathf.Abs(targetCameraZPosition) < cameraColisionRadius)
        {
            targetCameraZPosition = -cameraColisionRadius;
        }

        //Apply the final position using a lerp over 0.2s time
        cameraObjectPosition.z = Mathf.Lerp(cameraObject.transform.localPosition.z, targetCameraZPosition, 0.2f);
        cameraObject.transform.localPosition = cameraObjectPosition;
    }

}
