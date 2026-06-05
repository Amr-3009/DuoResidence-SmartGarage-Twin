using UnityEngine;
using System.Collections.Generic;

public class TwinGarageBuilder : MonoBehaviour
{
    [Header("Twin Asset Prefab")]
    [Tooltip("Drag a low-profile Cube prefab here that contains the TwinCubeIndicator script.")]
    [SerializeField] private GameObject twinCubePrefab;

    [Header("Matched Dimension Matrix")]
    [SerializeField] private int slotsPerRow = 20; // Kept at 15 to mirror asset layout exactly
    [SerializeField] private float slotWidth = 2.5f; //
    [SerializeField] private float slotDepth = 5f; //
    [SerializeField] private float laneRoadWidth = 6f; //
    [SerializeField] private float slotSpacingGap = 0.5f; //

    private float[] aisleCentersX = new float[] { -18.5f, 0f, 18.5f }; // Exact center arrays from Project A
    private string[] laneNames = new string[] { "A", "B", "C" }; //
    private float totalRowLength;

    // Master cache for the live synchronization engine to reference later
    public Dictionary<string, TwinCubeIndicator> SpawnedTwinSlots { get; private set; } = new Dictionary<string, TwinCubeIndicator>();

    void Awake()
    {
        totalRowLength = (slotsPerRow * slotWidth) + ((slotsPerRow - 1) * slotSpacingGap); //
        BuildAbstractTwinGrid();
    }

    private void BuildAbstractTwinGrid()
    {
        // Generate the matching topological mapping row by row
        for (int i = 0; i < aisleCentersX.Length; i++)
        {
            BuildTwinRow(laneNames[i], aisleCentersX[i], isRightSide: false, startSlotNumber: 1); //
            BuildTwinRow(laneNames[i], aisleCentersX[i], isRightSide: true, startSlotNumber: 21); //
        }
    }

    private void BuildTwinRow(string laneID, float aisleCenterX, bool isRightSide, int startSlotNumber)
    {
        float offsetX = (laneRoadWidth / 2f) + (slotDepth / 2f); //
        float slotX = isRightSide ? aisleCenterX + offsetX : aisleCenterX - offsetX; //
        float rotationY = isRightSide ? -90f : 90f; //
        float effectiveSlotSpacing = slotWidth + slotSpacingGap; //

        for (int i = 0; i < slotsPerRow; i++)
        {
            float slotZ = -(totalRowLength / 2f) + (slotWidth / 2f) + (i * effectiveSlotSpacing); //
            string slotID = laneID + (startSlotNumber + i).ToString("D2"); //
            
            SpawnTwinCube(slotID, slotX, slotZ, rotationY);
        }
    }

private void SpawnTwinCube(string slotID, float x, float z, float rotationY)
    {
        if (twinCubePrefab == null) return;

        // Position the uniform master root container flat on the floor surface
        Vector3 position = new Vector3(x, 0f, z);
        Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);

        GameObject cubeObj = Instantiate(twinCubePrefab, position, rotation, this.transform);
        cubeObj.name = "Slot_" + slotID;

        TwinCubeIndicator indicator = cubeObj.GetComponent<TwinCubeIndicator>();
        if (indicator != null)
        {
            // Pass the layout metrics down to safely scale the mesh child without warping the text font
            indicator.ConfigureSlot(slotID, slotWidth, slotDepth);
            indicator.SetTwinState(true); 
            SpawnedTwinSlots.Add(slotID, indicator);
        }
    }
}