using UnityEngine;

public class AirQualityVisualizer : MonoBehaviour
{
    [Header("Smog System")]
    [SerializeField] private ParticleSystem smogParticleSystem;

    [Header("CO2 Thresholds (PPM)")]
    [SerializeField] private float safeCO2 = 400f;     
    [SerializeField] private float cautionCO2 = 1000f; 
    [SerializeField] private float dangerCO2 = 2000f;  

    [Header("NO Thresholds (PPM)")]
    [SerializeField] private float safeNO = 0.5f;     
    [SerializeField] private float cautionNO = 2.0f; 
    [SerializeField] private float dangerNO = 5.0f;  

    [Header("Smog Colors")]
    [SerializeField] private Color safeColor = new Color(1f, 1f, 1f, 0f); 
    [SerializeField] private Color cautionColor = new Color(1f, 0.8f, 0f, 0.3f); 
    [SerializeField] private Color dangerColor = new Color(1f, 0f, 0f, 0.8f); 

    [Header("Thickness (Emission Rate)")]
    [SerializeField] private float maxParticlesPerSecond = 300f; 

    // Internal cache array to manipulate active particles without generating garbage collection lag
    private ParticleSystem.Particle[] particleCache;

    /// <summary>
    /// Processes incoming dual-sensor data and shifts the environment based on the worst reading.
    /// </summary>
    public void UpdateAirQuality(float currentCO2, float currentNO)
    {
        if (smogParticleSystem == null) return;

        var main = smogParticleSystem.main;
        var emission = smogParticleSystem.emission;

        // 1. Calculate severities
        float co2Severity = Mathf.InverseLerp(safeCO2, dangerCO2, currentCO2);
        float noSeverity = Mathf.InverseLerp(safeNO, dangerNO, currentNO);
        float highestSeverity = Mathf.Max(co2Severity, noSeverity);

        Color targetColor = safeColor;

        // 2. Determine target color match
        if (highestSeverity < 0.5f)
        {
             float lowerBlend = Mathf.InverseLerp(0f, 0.5f, highestSeverity);
             targetColor = Color.Lerp(safeColor, cautionColor, lowerBlend);
        }
        else
        {
             float upperBlend = Mathf.InverseLerp(0.5f, 1f, highestSeverity);
             targetColor = Color.Lerp(cautionColor, dangerColor, upperBlend);
        }

        // 3. Set properties for future particles
        main.startColor = targetColor;
        emission.rateOverTime = Mathf.Lerp(0f, maxParticlesPerSecond, highestSeverity);

        // =========================================================
        // FORCE REAL-TIME CHANGES ON ALREADY ALIVE PARTICLES
        // =========================================================
        if (particleCache == null || particleCache.Length < main.maxParticles)
        {
            particleCache = new ParticleSystem.Particle[main.maxParticles];
        }

        // Grab all currently active clouds drifting in the scene
        int aliveCount = smogParticleSystem.GetParticles(particleCache);

        for (int i = 0; i < aliveCount; i++)
        {
            // Force their color to match the new telemetry state instantly
            particleCache[i].startColor = targetColor;

            // If the air is clean (severity is 0), rapidly decay remaining lifespans so they dissolve away
            if (highestSeverity <= 0f)
            {
                particleCache[i].remainingLifetime = Mathf.Min(particleCache[i].remainingLifetime, 0.5f);
            }
        }

        // Apply changes back into the engine particle pool
        smogParticleSystem.SetParticles(particleCache, aliveCount);
    }
}