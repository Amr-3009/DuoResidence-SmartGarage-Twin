using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Animator))]
public class UmbrellaTwinMQTTController : MonoBehaviour
{
    private enum SystemMode { Automatic, ForcedOpen, ForcedClose }

    [Header("MQTT Topics")]
    [SerializeField] private string windTopic = "DuoResidence/Amr/SmartShades/Wind";
    [SerializeField] private string rainTopic = "DuoResidence/Amr/SmartShades/Rain";
    [SerializeField] private string solarTopic = "DuoResidence/Amr/SmartShades/Solar";
    [SerializeField] private string controlTopic = "DuoResidence/Amr/SmartShades/Control";

    [Header("UI Visibility Control")]
    [SerializeField] private GameObject dashboardPanel;
    [SerializeField] private KeyCode toggleKey = KeyCode.U;

    [Header("UI Readouts")]
    [SerializeField] private TextMeshProUGUI windDisplay;
    [SerializeField] private TextMeshProUGUI rainDisplay;
    [SerializeField] private TextMeshProUGUI solarDisplay;
    [SerializeField] private TextMeshProUGUI systemModeDisplay;

    [Header("UI Buttons")]
    [SerializeField] private Button forceOpenButton;
    [SerializeField] private Button forceCloseButton;
    [SerializeField] private Button resumeAutoButton;

    [Header("Threshold Configuration")]
    [SerializeField] private float windSpeedLimit = 12.0f;    
    [SerializeField] private float rainIntensityLimit = 15.0f; 
    [SerializeField] private float solarOpeningThreshold = 300.0f;

    private Animator _animator;
    private SystemMode _currentMode = SystemMode.Automatic;

    private float _windSpeed = 0f;
    private float _rainIntensity = 0f;
    private float _solarIrradiance = 0f;
    private bool _lastCanopyState = false;

    private void OnEnable()
    {
        // Connect directly into your manager's static thread-safe message event
        MQTTConnectionManager.OnTelemetryMessageReceived += OnMqttMessageArrived;
    }

    private void OnDisable()
    {
        // Clean up listeners when disabled or changing scenes to prevent memory leaks
        MQTTConnectionManager.OnTelemetryMessageReceived -= OnMqttMessageArrived;
    }

    void Start()
    {
        _animator = GetComponent<Animator>();

        // Link button click events to MQTT command dispatchers
        if (forceOpenButton != null) forceOpenButton.onClick.AddListener(() => DispatchCommand(SystemMode.ForcedOpen, "FORCE_OPEN"));
        if (forceCloseButton != null) forceCloseButton.onClick.AddListener(() => DispatchCommand(SystemMode.ForcedClose, "FORCE_CLOSE"));
        if (resumeAutoButton != null) resumeAutoButton.onClick.AddListener(() => DispatchCommand(SystemMode.Automatic, "RESUME_AUTO"));

        UpdateModeUI();

        // Ensure overlay starts hidden on launch
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(false);
        }

        // Auto-register subscriptions through your manager instance on startup
        if (MQTTConnectionManager.Instance != null)
        {
            MQTTConnectionManager.Instance.SubscribeToTopic(windTopic);
            MQTTConnectionManager.Instance.SubscribeToTopic(rainTopic);
            MQTTConnectionManager.Instance.SubscribeToTopic(solarTopic);
            MQTTConnectionManager.Instance.SubscribeToTopic(controlTopic);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleDashboardVisibility();
        }
    }

    private void ToggleDashboardVisibility()
    {
        if (dashboardPanel != null)
        {
            bool currentStatus = dashboardPanel.activeSelf;
            dashboardPanel.SetActive(!currentStatus);
        }
    }

    /// <summary>
    /// Triggered automatically whenever your MQTTConnectionManager update loop drains its message queue.
    /// </summary>
    private void OnMqttMessageArrived(string topic, string payload)
    {
        if (topic == windTopic && float.TryParse(payload, out _windSpeed))
        {
            windDisplay.text = $"Wind Speed: {_windSpeed:F1} m/s";
        }
        else if (topic == rainTopic && float.TryParse(payload, out _rainIntensity))
        {
            rainDisplay.text = $"Rain Intensity: {_rainIntensity:F0}%";
        }
        else if (topic == solarTopic && float.TryParse(payload, out _solarIrradiance))
        {
            solarDisplay.text = $"Solar Intensity: {_solarIrradiance:F0} W/m²";
        }
        else if (topic == controlTopic)
        {
            if (payload == "FORCE_OPEN") _currentMode = SystemMode.ForcedOpen;
            else if (payload == "FORCE_CLOSE") _currentMode = SystemMode.ForcedClose;
            else if (payload == "RESUME_AUTO") _currentMode = SystemMode.Automatic;
            
            UpdateModeUI();
        }

        EvaluateTwinCanopyState();
    }

    private void EvaluateTwinCanopyState()
    {
        bool shouldBeOpen = false;

        switch (_currentMode)
        {
            case SystemMode.ForcedOpen:
                shouldBeOpen = true;
                break;
            case SystemMode.ForcedClose:
                shouldBeOpen = false;
                break;
            case SystemMode.Automatic:
                if (_windSpeed >= windSpeedLimit) shouldBeOpen = false;
                else if (_rainIntensity >= rainIntensityLimit) shouldBeOpen = true;
                else shouldBeOpen = _solarIrradiance >= solarOpeningThreshold;
                break;
        }

        if (shouldBeOpen != _lastCanopyState)
        {
            _lastCanopyState = shouldBeOpen;
            _animator.SetBool("IsOpen", shouldBeOpen);
        }
    }

    private void DispatchCommand(SystemMode targetMode, string payload)
    {
        if (MQTTConnectionManager.Instance != null)
        {
            _currentMode = targetMode;
            UpdateModeUI();
            EvaluateTwinCanopyState();

            // Broadcast command out so your broader smart residence ecosystem can process it
            MQTTConnectionManager.Instance.PublishTopic(controlTopic, payload, retain: false);
            Debug.Log($"[Twin Control Override] Published to global broker: {payload} on topic: {controlTopic}");
        }
    }

    private void UpdateModeUI()
    {
        if (systemModeDisplay == null) return;

        switch (_currentMode)
        {
            case SystemMode.Automatic:
                systemModeDisplay.text = "Mode: <color=#66FF66>AUTOMATIC</color>";
                break;
            case SystemMode.ForcedOpen:
                systemModeDisplay.text = "Mode: <color=#FFFF66>FORCED OPEN</color>";
                break;
            case SystemMode.ForcedClose:
                systemModeDisplay.text = "Mode: <color=#FF5555>FORCED CLOSED</color>";
                break;
        }
    }
}