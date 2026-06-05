using System.Collections;
using UnityEngine;
using TMPro; // <-- Ensure this is here for the text field reference

public class TwinSyncManager : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject containing the TwinGarageBuilder script here.")]
    [SerializeField] private TwinGarageBuilder twinBuilder;

    [Header("Network Configuration")]
    [SerializeField] private string slotSubscriptionTopic = "DuoResidence/Amr/Garage/Slots/#";
    [SerializeField] private string hvacSubscriptionTopic = "DuoResidence/Amr/Garage/HVAC/Telemetry";
    [SerializeField] private string trafficSubscriptionTopic = "DuoResidence/Amr/Garage/Traffic/EntranceCount"; // <-- NEW: Traffic topic

    [System.Serializable]
    public class HVACTelemetryPayload
    {
        public float co2;
        public float no;
        public float operatingPercentage;
        public float smallFanRPM;
        public float bigFanRPM;
    }

    [Header("Air Quality Visuals")]
    [SerializeField] private AirQualityVisualizer airQualityVisualizer;
    [SerializeField] private HvacDashboardController hvacDashboardController;

    [Header("Global Interface Readouts")]
    [SerializeField] private TextMeshProUGUI totalCarsText; // <-- NEW: Text field link

    [SerializeField] private FanDashboardController fanDashboardController;

    private bool _isSubscribed = false;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);

        while (MQTTConnectionManager.Instance == null)
        {
            yield return new WaitForSeconds(0.2f);
        }

        SubscribeToAssetStream();
    }

    private void SubscribeToAssetStream()
    {
        if (MQTTConnectionManager.Instance != null && !_isSubscribed)
        {
            MQTTConnectionManager.OnTelemetryMessageReceived += OnTelemetryReceived;
            
            // Subscribe to all 3 necessary data channels cleanly
            MQTTConnectionManager.Instance.SubscribeToTopic(slotSubscriptionTopic);
            MQTTConnectionManager.Instance.SubscribeToTopic(hvacSubscriptionTopic);
            MQTTConnectionManager.Instance.SubscribeToTopic(trafficSubscriptionTopic); // <-- NEW: Subscribe
            
            _isSubscribed = true;
            Debug.Log($"<color=#00FFCC><b>[Twin Sync Engine]:</b></color> Multi-stream network synchronization fully active.");
        }
    }

    private void OnTelemetryReceived(string topic, string payload)
    {
        // =========================================================
        // ROUTE 1: Inbound Cumulative Traffic Counter Check
        // =========================================================
        if (topic == trafficSubscriptionTopic)
        {
            // Payload arrives as a raw string integer number directly
            if (totalCarsText != null)
            {
                totalCarsText.text = $"Total Cars Entered This Session: {payload}";
            }
            return;
        }

        // =========================================================
        // ROUTE 2: HVAC & Air Quality Stream Processing
        // =========================================================
        if (topic == hvacSubscriptionTopic)
        {
            HVACTelemetryPayload hvacData = JsonUtility.FromJson<HVACTelemetryPayload>(payload);

            if (airQualityVisualizer != null)
            {
                airQualityVisualizer.UpdateAirQuality(hvacData.co2, hvacData.no);
            }

            if (hvacDashboardController != null)
            {
                hvacDashboardController.UpdateEnvironmentalReadings(hvacData.co2, hvacData.no);
            }

            // 3. Update your fan telemetry values and RPM readouts
            if (fanDashboardController != null)
            {
                fanDashboardController.UpdateFanTelemetry(hvacData.bigFanRPM, hvacData.smallFanRPM, hvacData.operatingPercentage);
            }
            return; 
        }

        // =========================================================
        // ROUTE 3: Structural Garage Slot Matrix Processing
        // =========================================================
        string[] topicParts = topic.Split('/');
        if (topicParts.Length < 6) return;

        string slotID = topicParts[topicParts.Length - 1];
        bool isVacant = payload.Contains("IS vacant") || payload.Contains("IS VACANT");

        if (twinBuilder != null && twinBuilder.SpawnedTwinSlots.TryGetValue(slotID, out TwinCubeIndicator targetCube))
        {
            targetCube.SetTwinState(isVacant);
        }
    }

    private void OnDisable()
    {
        MQTTConnectionManager.OnTelemetryMessageReceived -= OnTelemetryReceived;
    }
}