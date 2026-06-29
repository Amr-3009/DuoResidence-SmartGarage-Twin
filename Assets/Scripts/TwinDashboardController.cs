using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// DuoResidence — Wall/Parking Dashboard Controller (UGUI)
///
/// Builds the 3-lane (A/B/C, 40 slots each) parking grid UI from a prefab,
/// subscribes to MQTT slot-status telemetry to colour each tile vacant/occupied,
/// and keeps the lane summary labels, capacity gauge and "FACILITY FULL" /
/// "MONITORING ACTIVE" status text in sync. GarageDashboardsController
/// re-implements this same view for the UI Toolkit popup and disables this
/// controller's canvas, but this script keeps running underneath it.
/// </summary>
public class TwinDashboardController : MonoBehaviour
{
    [Header("Node-RED Style Visual Grid Prefabs")]
    [SerializeField] private GameObject gridSlotUiPrefab;

    [Header("Lane Grid Layout Target Panels")]
    [SerializeField] private Transform laneAGridParent;
    [SerializeField] private Transform laneBGridParent;
    [SerializeField] private Transform laneCGridParent;

    [Header("Lane Status Text Summary Fields")]
    [SerializeField] private TextMeshProUGUI laneAText;
    [SerializeField] private TextMeshProUGUI laneBText;
    [SerializeField] private TextMeshProUGUI laneCText;

    [Header("Capacity Gauge Components")]
    [SerializeField] private Slider capacitySlider;
    [SerializeField] private Image sliderFillImage;
    [SerializeField] private TextMeshProUGUI capacityPercentageText;
    [SerializeField] private TextMeshProUGUI facilityStatusText;

        [Header("Capacity Percentage Slider")]
    [SerializeField] private Slider capacityPercentageSlider;
    [SerializeField] private TextMeshProUGUI capacityPercentageSliderLabel;

    
[Header("Dynamic Dashboard Palette")]
    [SerializeField] private Color vacantZoneColor = new Color(0.239f, 0.863f, 0.518f, 1f);  // #3ddc84
    [SerializeField] private Color occupiedZoneColor = new Color(0.878f, 0.361f, 0.361f, 1f); // #e05c5c

    private int totalSlotsPerLane = 40; // <-- UPDATED: 40 slots per lane
    private int grandTotalFacilityCapacity = 120; // 3 lanes * 40 slots = 120

    private Dictionary<string, bool> twinCapacityMap = new Dictionary<string, bool>();
    private Dictionary<string, Image> gridUiElementMap = new Dictionary<string, Image>();

    // Builds the parking grid, subscribes to MQTT telemetry, and does an
    // initial refresh of the summary/gauge UI.
    void Start()
    {
        InitializeDashboardMatrices();
        MQTTConnectionManager.OnTelemetryMessageReceived += ProcessIncomingDashboardTelemetry;
        UpdateDashboardVisuals();
    }

    // Unsubscribes from MQTT telemetry to avoid leaking the event handler.
    private void OnDestroy()
    {
        MQTTConnectionManager.OnTelemetryMessageReceived -= ProcessIncomingDashboardTelemetry;
    }

    /// <summary>
    /// Builds all three lane grids (A, B, C), each split into two rows of 20
    /// slots (1-20 and 21-40), under their respective parent containers.
    /// </summary>
    private void InitializeDashboardMatrices()
    {
        string[] targetLanes = { "A", "B", "C" }; 
        foreach (string lane in targetLanes)
        {
            Transform targetParentContainer = null;
            if (lane == "A") targetParentContainer = laneAGridParent;
            else if (lane == "B") targetParentContainer = laneBGridParent;
            else if (lane == "C") targetParentContainer = laneCGridParent;

            // UPDATED: Spawns 20 slots per row to equal 40 total per lane
            BuildLaneUISequence(lane, 1, 20, targetParentContainer);
            BuildLaneUISequence(lane, 21, 40, targetParentContainer);
        }
    }

    /// <summary>
    /// Instantiates one grid-slot prefab per slot ID in the given range
    /// (e.g. A01..A20), labels it (TextMeshPro or legacy Text), sets it to
    /// the vacant colour, registers it in <see cref="twinCapacityMap"/> and
    /// <see cref="gridUiElementMap"/> for later telemetry-driven updates.
    /// </summary>
    private void BuildLaneUISequence(string laneID, int start, int end, Transform parentContainer)
    {
        for (int i = start; i <= end; i++)
        {
            string slotID = laneID + i.ToString("D2");
            twinCapacityMap[slotID] = true;

            if (gridSlotUiPrefab != null && parentContainer != null)
            {
                GameObject uiBlock = Instantiate(gridSlotUiPrefab, parentContainer);
                uiBlock.name = "UiTile_" + slotID;

                // Bulletproof Text Catch
                TextMeshProUGUI tmpLabel = uiBlock.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmpLabel != null) tmpLabel.text = slotID;
                else
                {
                    Text legacyLabel = uiBlock.GetComponentInChildren<Text>(true);
                    if (legacyLabel != null) legacyLabel.text = slotID;
                }

                Image imgComp = uiBlock.GetComponent<Image>();
                if (imgComp != null)
                {
                    imgComp.color = vacantZoneColor;
                    gridUiElementMap[slotID] = imgComp;
                }
            }
        }
    }

    /// <summary>
    /// MQTT telemetry handler. If the topic's last segment matches a known slot ID,
    /// updates that slot's vacant/occupied state and tile colour, then refreshes
    /// the lane summaries and capacity gauge.
    /// </summary>
    private void ProcessIncomingDashboardTelemetry(string topic, string payload)
    {
        string[] structuralParts = topic.Split('/');
        if (structuralParts.Length < 6) return;

        string slotID = structuralParts[structuralParts.Length - 1];
        bool isVacant = payload.Contains("IS VACANT");

        if (twinCapacityMap.ContainsKey(slotID))
        {
            twinCapacityMap[slotID] = isVacant;
            if (gridUiElementMap.TryGetValue(slotID, out Image targetTileGraphic))
            {
                targetTileGraphic.color = isVacant ? vacantZoneColor : occupiedZoneColor;
            }
            UpdateDashboardVisuals();
        }
    }

    /// <summary>
    /// Recomputes occupied counts per lane from <see cref="twinCapacityMap"/>,
    /// then updates the lane summary labels, the capacity slider/percentage/fill
    /// colour, and the facility status text (FULL vs MONITORING ACTIVE).
    /// </summary>
    private void UpdateDashboardVisuals()
    {
        int occupiedCountA = 0;
        int occupiedCountB = 0;
        int occupiedCountC = 0;

        foreach (var slot in twinCapacityMap)
        {
            if (!slot.Value) 
            {
                if (slot.Key.StartsWith("A")) occupiedCountA++;
                else if (slot.Key.StartsWith("B")) occupiedCountB++;
                else if (slot.Key.StartsWith("C")) occupiedCountC++;
            }
        }

        int cumulativeOccupiedSpaces = occupiedCountA + occupiedCountB + occupiedCountC;

        if (laneAText != null) laneAText.text = $"Lane A: {occupiedCountA} / {totalSlotsPerLane} Occupied";
        if (laneBText != null) laneBText.text = $"Lane B: {occupiedCountB} / {totalSlotsPerLane} Occupied";
        if (laneCText != null) laneCText.text = $"Lane C: {occupiedCountC} / {totalSlotsPerLane} Occupied";

        if (capacitySlider != null)
        {
            capacitySlider.maxValue = grandTotalFacilityCapacity;
            capacitySlider.value = cumulativeOccupiedSpaces;
        }

        float computedFillRatio = ((float)cumulativeOccupiedSpaces / grandTotalFacilityCapacity) * 100f;
        if (capacityPercentageText != null)
        {
            capacityPercentageText.text = $"{computedFillRatio:F0}% Full";
        }

        if (capacityPercentageSlider != null)
        {
            capacityPercentageSlider.value = computedFillRatio;
        }
        if (capacityPercentageSliderLabel != null)
        {
            capacityPercentageSliderLabel.text = $"{computedFillRatio:F0}%";
        }

        if (sliderFillImage != null)
        {
            sliderFillImage.color = Color.Lerp(vacantZoneColor, occupiedZoneColor, computedFillRatio / 100f);
        }

        if (facilityStatusText != null)
        {
            if (cumulativeOccupiedSpaces >= grandTotalFacilityCapacity) facilityStatusText.text = "<color=#FF0055><B>FACILITY FULL</B></color>";
            else facilityStatusText.text = "<color=#00FFCC><B>MONITORING ACTIVE</B></color>";
        }
    }
}