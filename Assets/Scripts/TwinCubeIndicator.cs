using UnityEngine;
using TMPro;

public class TwinCubeIndicator : MonoBehaviour
{
    [Header("UI Text Display")]
    [SerializeField] private TextMeshPro idTextMesh;

    [Header("Visual Transform Scaling")]
    [Tooltip("Drag the child FloorPlate_Mesh object here.")]
    [SerializeField] private Transform floorPlateTransform;

    [Header("State Materials")]
    [SerializeField] private MeshRenderer cubeRenderer;
    [SerializeField] private Material vacantGreenMaterial;
    [SerializeField] private Material occupiedRedMaterial;

    /// <summary>
    /// Configures the text label and applies independent stretching dimensions to the floor plate mesh.
    /// </summary>
    public void ConfigureSlot(string slotID, float width, float depth)
    {
        if (idTextMesh != null)
        {
            idTextMesh.text = slotID;
        }

        // Apply the tracking rectangle scale ONLY to the floor plate child mesh
        if (floorPlateTransform != null)
        {
            floorPlateTransform.localScale = new Vector3(width, 0.2f, depth);
        }
    }

    /// <summary>
    /// Updates the color block based on live asset telemetry.
    /// </summary>
    public void SetTwinState(bool isVacant)
    {
        if (cubeRenderer != null)
        {
            cubeRenderer.material = isVacant ? vacantGreenMaterial : occupiedRedMaterial;
        }
    }
}