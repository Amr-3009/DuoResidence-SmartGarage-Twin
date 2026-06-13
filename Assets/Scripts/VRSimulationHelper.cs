using UnityEngine;

public class VRSimulationHelper : MonoBehaviour
{
    [Header("Elevation Control Matrix")]
    [SerializeField] private float lockedWorldHeight = 4.5f;

    [Header("Spatial Bounding Box (55 x 81 Boundary Grid)")]
    [Tooltip("The minimum allowed X coordinate of your garage layout.")]
    [SerializeField] private float minX = -27.5f;
    [Tooltip("The maximum allowed X coordinate of your garage layout.")]
    [SerializeField] private float maxX = 27.5f;
    [Tooltip("The minimum allowed Z coordinate of your garage layout.")]
    [SerializeField] private float minZ = -40.5f;
    [Tooltip("The maximum allowed Z coordinate of your garage layout.")]
    [SerializeField] private float maxZ = 40.5f;

    [Header("Tracking Node Assignments")]
    [SerializeField] private Transform mainCamera;
    [SerializeField] private Transform leftHandContainer;
    [SerializeField] private Transform rightHandContainer;

    private void LateUpdate()
    {
        // 1. Calculate bounded positions across the X and Z horizontal planes
        // Mathf.Clamp forces the target value to remain locked inside the min/max limits
        float clampedX = Mathf.Clamp(transform.position.x, minX, maxX);
        float clampedZ = Mathf.Clamp(transform.position.z, minZ, maxZ);

        // 2. Apply the strict location lock
        // This overrides keyboard walking and teleportation instantly if boundaries are breached
        transform.position = new Vector3(clampedX, lockedWorldHeight, clampedZ);

        // 3. Force the hands to tilt up/down (Pitch) in perfect synchronization with the eyes
        if (mainCamera != null)
        {
            float targetPitchX = mainCamera.localEulerAngles.x;

            if (leftHandContainer != null)
            {
                Vector3 leftEuler = leftHandContainer.localEulerAngles;
                leftHandContainer.localEulerAngles = new Vector3(targetPitchX, leftEuler.y, leftEuler.z);
            }

            if (rightHandContainer != null)
            {
                Vector3 rightEuler = rightHandContainer.localEulerAngles;
                rightHandContainer.localEulerAngles = new Vector3(targetPitchX, rightEuler.y, rightEuler.z);
            }
        }
    }
}