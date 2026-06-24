using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DuoResidence — HVAC Dashboard Controller (UGUI)
///
/// Displays live CO2 and NO readings as sliders/progress bars with text labels,
/// blending the fill colour between safe/caution/danger based on configurable
/// thresholds. Driven via <see cref="UpdateEnvironmentalReadings"/> from
/// TwinSyncManager. Its UGUI canvas is normally hidden behind the UI Toolkit
/// dashboards popup (GarageDashboardsController), which polls the read-only
/// accessors below to mirror these values into the new themed dashboard.
/// </summary>
public class HvacDashboardController : MonoBehaviour
{
    [Header("CO2 Display Elements")]
    [SerializeField] private Slider co2Slider;
    [SerializeField] private Image co2SliderFill; // <-- NEW: Targets the internal progress color
    [SerializeField] private TextMeshProUGUI co2ValueText;
    [SerializeField] private float minCO2Limits = 400f;  // Project A Min
    [SerializeField] private float maxCO2Limits = 5000f; // Project A Max

    [Header("NO Display Elements")]
    [SerializeField] private Slider noSlider;
    [SerializeField] private Image noSliderFill;   // <-- NEW: Targets the internal progress color
    [SerializeField] private TextMeshProUGUI noValueText;
    [SerializeField] private float minNOLimits = 0f;     // Project A Min
    [SerializeField] private float maxNOLimits = 100f;   // Project A Max

    [Header("Dynamic Progress Palette")]
    [SerializeField] private Color safeColor    = new Color(0.239f, 0.863f, 0.518f, 1f); // #3ddc84
    [SerializeField] private Color cautionColor = new Color(1.000f, 0.596f, 0.000f, 1f); // #ff9800
    [SerializeField] private Color dangerColor  = new Color(0.878f, 0.361f, 0.361f, 1f); // #e05c5c

    // ── Read-only accessors for the UI Toolkit dashboard bridge ──────
    // (GarageDashboardsController polls these to mirror live values
    //  into the new themed dashboard without touching the MQTT logic below.)
    public Slider Co2Slider => co2Slider;
    public Image  Co2SliderFill => co2SliderFill;
    public TextMeshProUGUI Co2ValueText => co2ValueText;
    public float MinCO2Limits => minCO2Limits;
    public float MaxCO2Limits => maxCO2Limits;

    public Slider NoSlider => noSlider;
    public Image  NoSliderFill => noSliderFill;
    public TextMeshProUGUI NoValueText => noValueText;
    public float MinNOLimits => minNOLimits;
    public float MaxNOLimits => maxNOLimits;

    // Initialises the CO2 and NO sliders' min/max ranges from the configured
    // limits and resets their values to the minimum.
    void Start()
    {
        // Enforce exact mathematical boundaries on the slider UI components
        if (co2Slider != null)
        {
            co2Slider.minValue = minCO2Limits;
            co2Slider.maxValue = maxCO2Limits;
            co2Slider.value = minCO2Limits;
        }

        if (noSlider != null)
        {
            noSlider.minValue = minNOLimits;
            noSlider.maxValue = maxNOLimits;
            noSlider.value = minNOLimits;
        }
    }

    /// <summary>
    /// Updates text readouts, updates progress bar tracking layout scales, and handles independent color blends.
    /// </summary>
    public void UpdateEnvironmentalReadings(float co2, float no)
    {
        // =========================================================
        // 1. CO2 PROGRESS & COLOR LOGIC
        // =========================================================
        if (co2Slider != null) co2Slider.value = co2;
        if (co2ValueText != null) co2ValueText.text = $"{co2:F0} PPM";

        if (co2SliderFill != null)
        {
            // Sync color shifts smoothly over Project A's custom alert break points
            if (co2 < 1000f)
            {
                float t = Mathf.InverseLerp(400f, 1000f, co2);
                co2SliderFill.color = Color.Lerp(safeColor, cautionColor, t);
            }
            else
            {
                float t = Mathf.InverseLerp(1000f, 2000f, co2);
                co2SliderFill.color = Color.Lerp(cautionColor, dangerColor, t);
            }
        }

        // =========================================================
        // 2. NO PROGRESS & COLOR LOGIC
        // =========================================================
        if (noSlider != null) noSlider.value = no;
        if (noValueText != null) noValueText.text = $"{no:F1} PPM";

        if (noSliderFill != null)
        {
            // Sync color shifts smoothly over Project A's custom alert break points
            if (no < 25f)
            {
                float t = Mathf.InverseLerp(0f, 25f, no);
                noSliderFill.color = Color.Lerp(safeColor, cautionColor, t);
            }
            else
            {
                float t = Mathf.InverseLerp(25f, 50f, no);
                noSliderFill.color = Color.Lerp(cautionColor, dangerColor, t);
            }
        }
    }
}
