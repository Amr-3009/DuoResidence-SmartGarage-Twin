using UnityEngine;

[RequireComponent(typeof(Camera))]
public class Mover : MonoBehaviour
{
    [Header("Flight Dynamics")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float sprintMultiplier = 2.5f; // Hold Left Shift to fly faster
    [SerializeField] private float lookSensitivity = 1.5f;

    [Header("Height Lock Settings")]
    [Tooltip("The permanent, immutable Y-coordinate altitude of the spectator camera.")]
    [SerializeField] private float lockedHeight = 3.0f;

    private float _rotationX = 0f;
    private float _rotationY = 0f;
    private bool _isCursorFree = false;
    private Camera _targetCamera;

    void Start()
    {
        _targetCamera = GetComponent<Camera>();

        // Start with the cursor hidden and locked for standard mouse-look flight
        SetCursorState(false);

        // Sync our tracking variables perfectly with the camera's active orientation matrix
        Vector3 currentRotation = transform.localEulerAngles;
        _rotationX = currentRotation.y;
        _rotationY = currentRotation.x;

        // Force initial height alignment immediately on spawn
        transform.position = new Vector3(transform.position.x, lockedHeight, transform.position.z);
    }

    void Update()
    {
        HandleCursorToggle();
        
        if (!_isCursorFree)
        {
            HandleMouseLook();
        }

        MovePlayer();
    }

    /// <summary>
    /// Listens for the Left Alt key to instantly swap between mouse-look flight and UI cursor clicking.
    /// </summary>
    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            _isCursorFree = !_isCursorFree;
            SetCursorState(_isCursorFree);
        }
    }

    private void SetCursorState(bool free)
    {
        if (free)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// Rotates the camera viewport. Forcing the Z-Euler angle to 0 prevents annoying horizon roll.
    /// </summary>
    private void HandleMouseLook()
    {
        _rotationX += Input.GetAxis("Mouse X") * lookSensitivity;
        _rotationY -= Input.GetAxis("Mouse Y") * lookSensitivity; 
        _rotationY = Mathf.Clamp(_rotationY, -85f, 85f);          

        transform.localRotation = Quaternion.Euler(_rotationY, _rotationX, 0f);
    }

    /// <summary>
    /// Translates position along a flattened XZ-plane and clamps altitude rigidly.
    /// </summary>
    private void MovePlayer() 
    {
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? (moveSpeed * sprintMultiplier) : moveSpeed;

        // 1. Extract the raw directional vectors from the camera optics
        Vector3 forward = _targetCamera.transform.forward;
        Vector3 right = _targetCamera.transform.right;

        // 2. THE FLATTENING: Force the Y components to zero.
        // This isolates movement exclusively to the horizontal grid plane.
        forward.y = 0f;
        right.y = 0f;

        // Re-normalize so looking steeply downward doesn't make horizontal translation slower
        forward.Normalize();
        right.Normalize();

        Vector3 moveInput = Vector3.zero;

        // Calculate travel vectors based on our newly flattened directions
        if (Input.GetKey(KeyCode.W)) moveInput += forward;
        if (Input.GetKey(KeyCode.S)) moveInput -= forward;
        if (Input.GetKey(KeyCode.D)) moveInput += right;
        if (Input.GetKey(KeyCode.A)) moveInput -= right;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            transform.position += moveInput.normalized * currentSpeed * Time.deltaTime;
        }

        // 3. THE ABSOLUTE CLAMP: Rigidly override any unexpected micro-physics drifts 
        // to guarantee your camera stays pinned exactly at Y = 3.
        transform.position = new Vector3(transform.position.x, lockedHeight, transform.position.z);
    }
}