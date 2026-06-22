using UnityEngine;
using System.Collections.Generic;

public class TwinGarageBuilder : MonoBehaviour
{
    [Header("Twin Asset Prefabs")]
    [Tooltip("Drag a low-profile Cube prefab here that contains the TwinCubeIndicator script.")]
    [SerializeField] private GameObject twinCubePrefab;
    [SerializeField] private GameObject pavementPrefab; 
    [SerializeField] private GameObject wallPrefab;     
    [SerializeField] private GameObject yellowLinePrefab;

    [Header("Matched Dimension Matrix")]
    [SerializeField] private int slotsPerRow = 20; 
    [SerializeField] private float slotWidth = 2.5f; 
    [SerializeField] private float slotDepth = 5f; 
    [SerializeField] private float laneRoadWidth = 6f; 
    [SerializeField] private float slotSpacingGap = 0.5f; 

    [Header("Structural Dimensions")]
    [SerializeField] private float wallThickness = 0.5f;
    [SerializeField] private float wallHeight = 7.5f; 
    [SerializeField] private float pavementLengthExtension = 0.8f; 
    [SerializeField] private float yellowLineWidth = 0.15f;
    [SerializeField] private float laneLineOffset = 0.2f;

    [Header("Spatial Alignment Coordinates")]
    [SerializeField] private float[] aisleCentersX = new float[] { -18.5f, 0f, 18.5f };
    [SerializeField] private string[] laneNames = new string[] { "A", "B", "C" }; 

    private float totalRowLength;

    // Master cache for the live synchronization engine to reference later
    public Dictionary<string, TwinCubeIndicator> SpawnedTwinSlots { get; private set; } = new Dictionary<string, TwinCubeIndicator>();

    void Awake()
    {
        totalRowLength = (slotsPerRow * slotWidth) + ((slotsPerRow - 1) * slotSpacingGap); 
        
        // Execute comprehensive procedural initialization
        BuildAbstractTwinGrid();
        BuildDividingWalls();
        BuildLaneLines();
    }

    private void BuildAbstractTwinGrid()
    {
        for (int i = 0; i < aisleCentersX.Length; i++)
        {
            BuildTwinRow(laneNames[i], aisleCentersX[i], isRightSide: false, startSlotNumber: 1); 
            BuildTwinRow(laneNames[i], aisleCentersX[i], isRightSide: true, startSlotNumber: 21); 
        }
    }

    private void BuildTwinRow(string laneID, float aisleCenterX, bool isRightSide, int startSlotNumber)
    {
        float offsetX = (laneRoadWidth / 2f) + (slotDepth / 2f); 
        float slotX = isRightSide ? aisleCenterX + offsetX : aisleCenterX - offsetX; 
        float rotationY = isRightSide ? -90f : 90f; 
        float effectiveSlotSpacing = slotWidth + slotSpacingGap; 

        for (int i = 0; i < slotsPerRow; i++)
        {
            float slotZ = -(totalRowLength / 2f) + (slotWidth / 2f) + (i * effectiveSlotSpacing); 
            string slotID = laneID + (startSlotNumber + i).ToString("D2"); 
            
            // 1. Spawn Core Tracking Twin Block
            SpawnTwinCube(slotID, slotX, slotZ, rotationY);

            // 2. Procedurally Generate Pavement Alignment Tracks
            if (i == 0 && pavementPrefab != null)
            {
                float firstPavementZ = slotZ - (slotWidth / 2f) - (slotSpacingGap / 2f);
                Vector3 pavementPos = new Vector3(slotX, 0.1f, firstPavementZ); 
                GameObject pavement = Instantiate(pavementPrefab, pavementPos, Quaternion.identity, this.transform);
                pavement.name = "Pavement_Start_" + laneID;
                pavement.transform.localScale = new Vector3(slotDepth + pavementLengthExtension, 0.2f, slotSpacingGap);
            }

            if (pavementPrefab != null)
            {
                float pavementZ = slotZ + (slotWidth / 2f) + (slotSpacingGap / 2f);
                Vector3 pavementPos = new Vector3(slotX, 0.1f, pavementZ); 
                GameObject pavement = Instantiate(pavementPrefab, pavementPos, Quaternion.identity, this.transform);
                pavement.name = "Pavement_" + slotID;
                pavement.transform.localScale = new Vector3(slotDepth + pavementLengthExtension, 0.2f, slotSpacingGap);
            }
        }
    }

    private void SpawnTwinCube(string slotID, float x, float z, float rotationY)
    {
        if (twinCubePrefab == null) return;

        Vector3 position = new Vector3(x, 0f, z);
        Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);

        GameObject cubeObj = Instantiate(twinCubePrefab, position, rotation, this.transform);
        cubeObj.name = "Slot_" + slotID;

        TwinCubeIndicator indicator = cubeObj.GetComponent<TwinCubeIndicator>();
        if (indicator != null)
        {
            indicator.ConfigureSlot(slotID, slotWidth, slotDepth);
            indicator.SetTwinState(true); 
            SpawnedTwinSlots.Add(slotID, indicator);
        }
    }

    private void BuildDividingWalls()
    {
        if (wallPrefab == null) return;

        float gapCenterX_AB = (aisleCentersX[0] + aisleCentersX[1]) / 2f; 
        float gapCenterX_BC = (aisleCentersX[1] + aisleCentersX[2]) / 2f; 

        Vector3 wallScale = new Vector3(wallThickness, wallHeight, totalRowLength + (2f * slotSpacingGap));

        GameObject wallAB = Instantiate(wallPrefab, new Vector3(gapCenterX_AB, wallHeight / 2f, 0f), Quaternion.identity, this.transform);
        wallAB.name = "Wall_Between_A_and_B";
        wallAB.transform.localScale = wallScale;

        GameObject wallBC = Instantiate(wallPrefab, new Vector3(gapCenterX_BC, wallHeight / 2f, 0f), Quaternion.identity, this.transform);
        wallBC.name = "Wall_Between_B_and_C";
        wallBC.transform.localScale = wallScale;
    }

    private void BuildLaneLines()
    {
        if (yellowLinePrefab == null) return;

        float lineLength = totalRowLength + slotSpacingGap;
        float lineY = 0.02f; 

        for (int i = 0; i < aisleCentersX.Length; i++)
        {
            float aisleCenterX = aisleCentersX[i];
            string laneName = laneNames[i];

            float leftX = aisleCenterX - (laneRoadWidth / 2f) + laneLineOffset;
            Vector3 leftPos = new Vector3(leftX, lineY, 0f);
            GameObject leftLine = Instantiate(yellowLinePrefab, leftPos, Quaternion.identity, this.transform);
            leftLine.name = $"YellowLine_Left_Lane_{laneName}";
            leftLine.transform.localScale = new Vector3(yellowLineWidth, 0.01f, lineLength);

            float rightX = aisleCenterX + (laneRoadWidth / 2f) - laneLineOffset;
            Vector3 rightPos = new Vector3(rightX, lineY, 0f);
            GameObject rightLine = Instantiate(yellowLinePrefab, rightPos, Quaternion.identity, this.transform);
            rightLine.name = $"YellowLine_Right_Lane_{laneName}";
            rightLine.transform.localScale = new Vector3(yellowLineWidth, 0.01f, lineLength);
        }
    }
}