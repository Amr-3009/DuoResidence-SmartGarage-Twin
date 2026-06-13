using UnityEngine;

public class GarageBoundaryEnforcer : MonoBehaviour
{
    [Header("Spatial Bounding Box (55 x 81 Boundary Grid)")]
    [Tooltip("The minimum allowed X coordinate of your garage layout.")]
    [SerializeField] private float minX = -27.5f;
    [Tooltip("The maximum allowed X coordinate of your garage layout.")]
    [SerializeField] private float maxX = 27.5f;
    [Tooltip("The minimum allowed Z coordinate of your garage layout.")]
    [SerializeField] private float maxZ = 40.5f;
    [Tooltip("The minimum allowed Z coordinate of your garage layout.")]
    [SerializeField] private float minZ = -40.5f;

    [Header("Optional Vertical Constraints")]
    [Tooltip("Check this true if you want to prevent the camera from flying too high or clipping under the floor tile.")]
    [SerializeField] private bool limitHeight = true;
    [SerializeField] private float minY = 1f;
    [SerializeField] private float maxY = 15f;

    private void LateUpdate()
    {
        // 1. Capture the current frame's calculated position
        Vector3 targetPosition = transform.position;

        // 2. Clamp the horizontal planes to hold the object inside the 55x81 footprint grid
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);

        // 3. Optional: Prevent flying out of the roof or sinking underground
        if (limitHeight)
        {
            targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);
        }

        // 4. Force the restricted position back onto the transform coordinates
        transform.position = targetPosition;
    }
}