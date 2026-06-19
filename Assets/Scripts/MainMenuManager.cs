using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// DuoResidence — Main Menu Manager (legacy UGUI)
///
/// Wires the platform-gateway buttons (3D, VR, Tablet Dashboard, Quit) to load
/// scenes by build index with an on-screen loading overlay and progress bar.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Platform Gateway Buttons")]
    [SerializeField] private Button launch3DButton;
    [SerializeField] private Button launchVRButton;
    [SerializeField] private Button launchDashboardButton;
    [SerializeField] private Button quitApplicationButton;

    [Header("Asynchronous Loading Screen Interface")]
    [SerializeField] private GameObject loadingOverlayPanel;
    [SerializeField] private Slider loadingProgressBar;
    [SerializeField] private TextMeshProUGUI loadingStatusText;

    /// <summary>
    /// Resets time scale, hides the loading overlay, and binds each gateway
    /// button to load its target scene (or quit, for the Quit button).
    /// </summary>
    private void Start()
    {
        // Enforce active time dilation initialization on scene entry
        Time.timeScale = 1f;

        // Force initialize loading panel overlay to hidden configuration on start
        if (loadingOverlayPanel != null) 
            loadingOverlayPanel.SetActive(false);

        // Bind interactive UI button click actions programmatically
        if (launch3DButton != null) 
            launch3DButton.onClick.AddListener(() => LoadPlatformModuleAsync(1)); // Index 1: 3D Environment

        if (launchVRButton != null) 
            launchVRButton.onClick.AddListener(() => LoadPlatformModuleAsync(2)); // Index 2: VR Environment

        if (launchDashboardButton != null) 
            launchDashboardButton.onClick.AddListener(() => LoadPlatformModuleAsync(3)); // Index 3: 2D Tablet Dashboard

        if (quitApplicationButton != null) 
            quitApplicationButton.onClick.AddListener(TerminateApplicationRuntime);
    }

    /// <summary>
    /// Safe execution route to initialize background asset loading threads
    /// </summary>
    public void LoadPlatformModuleAsync(int sceneBuildIndex)
    {
        StartCoroutine(SceneLoadingSequenceRoutine(sceneBuildIndex));
    }

    /// <summary>
    /// Shows the loading overlay, disables the menu buttons, then asynchronously
    /// loads the scene at <paramref name="sceneBuildIndex"/> while smoothly
    /// animating the progress bar/status text, activating the scene once it
    /// reaches 100%.
    /// </summary>
    private IEnumerator SceneLoadingSequenceRoutine(int sceneBuildIndex)
    {
        // Reveal loading screen panel over the gateway menu layout
        if (loadingOverlayPanel != null)
        {
            loadingOverlayPanel.SetActive(true);
            if (loadingProgressBar != null) loadingProgressBar.value = 0f;
            if (loadingStatusText != null) loadingStatusText.text = "Initializing hardware pipelines... 0%";
        }

        // Lock button group states to prevent double click overlaps during scene assembly
        SetMenuButtonsInteractivity(false);

        yield return new WaitForSeconds(0.4f); // Minor visual stabilization buffer block

        // Begin streaming the target scene assets into memory background pipelines
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneBuildIndex);
        
        // Block auto-activation to ensure the progress bar fills smoothly before switching
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            // Unity native async tracking maps from 0.0 to 0.9. Normalize to a clean 0.0 to 1.0 scale factor.
            float trueProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            if (loadingProgressBar != null)
            {
                // Smoothly slide the bar forward over frames
                loadingProgressBar.value = Mathf.MoveTowards(loadingProgressBar.value, trueProgress, Time.deltaTime * 2f);
            }

            if (loadingStatusText != null)
            {
                loadingStatusText.text = $"Assembling platform matrices... {(loadingProgressBar.value * 100f):0}%";
            }

            // Once background memory allocations reach 100% and bar catches up, pop the scene active
            if (Mathf.Approximately(loadingProgressBar.value, 1f) && asyncLoad.progress >= 0.9f)
            {
                if (loadingStatusText != null) loadingStatusText.text = "Allocation finalized! Booting module core...";
                yield return new WaitForSeconds(0.3f);
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    // Enables/disables all gateway buttons at once, used to prevent double-clicks
    // while a scene load is in progress.
    private void SetMenuButtonsInteractivity(bool activeState)
    {
        if (launch3DButton != null) launch3DButton.interactable = activeState;
        if (launchVRButton != null) launchVRButton.interactable = activeState;
        if (launchDashboardButton != null) launchDashboardButton.interactable = activeState;
        if (quitApplicationButton != null) quitApplicationButton.interactable = activeState;
    }

    // Exits Play Mode in the Editor, or quits the built application.
    public void TerminateApplicationRuntime()
    {
        Debug.Log("[System Core] Closing multi-platform hub deployment runtime.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}