using UnityEngine;

/// <summary>
/// Attach to any GameObject in Scene_3D_Environment.
/// On Play : starts on Camera1.
/// TAB     : cycles Camera1 -> Camera2 -> Camera3 -> Camera4 -> Camera1 -> ...
/// F5      : toggles between Main Camera and the last active security camera.
/// </summary>
public class GarageCameraSwitcher : MonoBehaviour
{
    [Header("Security Cameras")]
    [Tooltip("Assign Camera1, Camera2, Camera3, Camera4 in order.")]
    public Camera[] securityCameras;

    [Header("Main Camera")]
    [Tooltip("Leave empty to auto-find Camera.main at startup.")]
    public Camera mainCamera;

    private int _currentIndex = 0;
    private int _lastSecurityIndex = 0;
    private bool _mainCameraActive = false;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        SetCamera(0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_mainCameraActive)
            {
                _lastSecurityIndex = (_lastSecurityIndex + 1) % securityCameras.Length;
                _currentIndex = _lastSecurityIndex;
            }
            else
            {
                _currentIndex = (_currentIndex + 1) % securityCameras.Length;
                _lastSecurityIndex = _currentIndex;
            }
            _mainCameraActive = false;
            SetCamera(_currentIndex);
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            if (_mainCameraActive)
            {
                _mainCameraActive = false;
                SetCamera(_lastSecurityIndex);
            }
            else
            {
                _lastSecurityIndex = _currentIndex;
                _mainCameraActive = true;
                SetCamera(-1);
            }
        }
    }

    void SetCamera(int index)
    {
        if (mainCamera != null)
            mainCamera.enabled = (index == -1);

        for (int i = 0; i < securityCameras.Length; i++)
        {
            if (securityCameras[i] != null)
                securityCameras[i].enabled = (i == index);
        }

        if (index == -1)
            Debug.Log("[GarageCameraSwitcher] Active: Main Camera");
        else if (index < securityCameras.Length && securityCameras[index] != null)
            Debug.Log("[GarageCameraSwitcher] Active: " + securityCameras[index].name);
    }
}
