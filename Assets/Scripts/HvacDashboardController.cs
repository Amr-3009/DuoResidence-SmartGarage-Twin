using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private Color safeColor = Color.green;
    [SerializeField] private Color cautionColor = Color.yellow;
    [SerializeField] private Color dangerColor = Color.red;

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