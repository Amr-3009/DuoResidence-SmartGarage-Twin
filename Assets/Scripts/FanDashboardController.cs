using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DuoResidence — Fan Dashboard Controller (UGUI)
///
/// Displays live big/small fan RPM (driven via <see cref="UpdateFanTelemetry"/> from
/// TwinSyncManager) and provides a manual ventilation override button that publishes
/// an MQTT command and temporarily locks the controls for the chosen duration.
/// Its UGUI canvas is normally hidden behind the UI Toolkit dashboards popup
/// (GarageDashboardsController), which reads its public accessors and forwards
/// button/dropdown interactions to this controller.
/// </summary>
public class FanDashboardController : MonoBehaviour
{
    [Header("RPM Display Text Objects")]
    [SerializeField] private TextMeshProUGUI bigFanRpmText;
    [SerializeField] private TextMeshProUGUI smallFanRpmText;

    [Header("Interactive Control Elements")]
    [SerializeField] private Button increaseRpmButton;
    [SerializeField] private TMP_Dropdown durationDropdown;
    [SerializeField] private TextMeshProUGUI buttonLabelText;

    [Header("Network Settings")]
    [SerializeField] private string overridePublishTopic = "DuoResidence/Amr/Garage/HVAC/Override";

    // Tracking variables for incoming network states
    private float _currentOperatingPercentage = 20f;

    // Tracking variables for manual countdown logic
    private float _lockoutTimer = 0f;
    private bool _isOverrideActive = false;
    private string _originalButtonText = "INCREASE VENTILATION";

    // ── Read-only accessors for the UI Toolkit dashboard bridge ──────
    public TextMeshProUGUI BigFanRpmText => bigFanRpmText;
    public TextMeshProUGUI SmallFanRpmText => smallFanRpmText;
    public Button IncreaseRpmButton => increaseRpmButton;
    public TMP_Dropdown DurationDropdown => durationDropdown;
    public TextMeshProUGUI ButtonLabelText => buttonLabelText;
    public bool IsOverrideActive => _isOverrideActive;

    /// <summary>
    /// Caches the button's label text (if not explicitly assigned), remembers its
    /// original text for restoring after a lockout, and binds the increase-RPM
    /// button's click to <see cref="ExecuteVentilationOverride"/>.
    /// </summary>
    void Start()
    {
        // Cache the original button label if it isn't explicitly linked
        if (increaseRpmButton != null && buttonLabelText == null)
        {
            buttonLabelText = increaseRpmButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (buttonLabelText != null)
        {
            _originalButtonText = buttonLabelText.text;
        }

        // Bind the button click event programmatically
        if (increaseRpmButton != null)
        {
            increaseRpmButton.onClick.AddListener(ExecuteVentilationOverride);
        }
    }

    /// <summary>
    /// While a manual override is active, counts down <see cref="_lockoutTimer"/>,
    /// shows the remaining seconds on the button label, and re-enables the
    /// controls (restoring the original label) once the timer reaches zero.
    /// </summary>
    void Update()
    {
        // Run the real-time frame countdown if an override is actively ticking
        if (_isOverrideActive)
        {
            _lockoutTimer -= Time.deltaTime;

            if (buttonLabelText != null)
            {
                buttonLabelText.text = $"LOCKED ({_lockoutTimer:F0}s)";
            }

            // Lift lockouts when the timer expires entirely
            if (_lockoutTimer <= 0f)
            {
                _isOverrideActive = false;
                
                if (increaseRpmButton != null) increaseRpmButton.interactable = true;
                if (durationDropdown != null) durationDropdown.interactable = true;
                if (buttonLabelText != null) buttonLabelText.text = _originalButtonText;
                
                Debug.Log("<color=green><b>[Fan Control]:</b></color> Manual override period ended. Control yielded back to automated sensors.");
            }
        }
    }

    /// <summary>
    /// Receives live incoming telemetry tracking strings from the TwinSyncManager router.
    /// </summary>
    public void UpdateFanTelemetry(float bigRPM, float smallRPM, float operatingPercentage)
    {
        _currentOperatingPercentage = operatingPercentage;

        if (bigFanRpmText != null) bigFanRpmText.text = $"{bigRPM:F0} RPM";
        if (smallFanRpmText != null) smallFanRpmText.text = $"{smallRPM:F0} RPM";
    }

    /// <summary>
    /// Evaluates current speeds, locks the UI, and publishes commands over the network.
    /// Exposed publicly so the UI Toolkit dashboard's button can trigger the same override.
    /// </summary>
    public void ExecuteVentilationOverride()
    {
        if (_isOverrideActive || MQTTConnectionManager.Instance == null) return;

        // 1. Calculate the target stepped increase requested by the user
        float targetPercentage = 50f;
        if (_currentOperatingPercentage >= 50f)
        {
            targetPercentage = 100f;
        }

        // 2. Map the selection index to a raw duration frame count
        float chosenDuration = 20f; // Default case (Index 0)
        if (durationDropdown != null)
        {
            switch (durationDropdown.value)
            {
                case 1: chosenDuration = 40f; break;
                case 2: chosenDuration = 60f; break; // 1 Minute
            }
        }

        // 3. Lock out interactive structural components
        _lockoutTimer = chosenDuration;
        _isOverrideActive = true;
        
        if (increaseRpmButton != null) increaseRpmButton.interactable = false;
        if (durationDropdown != null) durationDropdown.interactable = false;

        // 4. Construct payload and send out over the global network broker channel
        // Formatted as standard payload text: targetPercentage,duration
        string commandPayload = $"{targetPercentage},{chosenDuration}";
        MQTTConnectionManager.Instance.PublishTopic(overridePublishTopic, commandPayload, retain: false);

        Debug.Log($"<color=orange><b>[Fan Override Sent]:</b></color> Target {targetPercentage}% for {chosenDuration}s. Interface Locked.");
    }

    // Removes the click listener added in Start() to avoid a dangling reference.
    private void OnDestroy()
    {
        if (increaseRpmButton != null)
        {
            increaseRpmButton.onClick.RemoveListener(ExecuteVentilationOverride);
        }
    }
}
