using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

/// <summary>
/// DuoResidence — Loading Screen Controller
/// 
/// Usage: Instead of calling SceneManager.LoadScene("TargetScene") directly,
/// call LoadingScreenController.LoadScene("TargetScene") from anywhere.
/// The loading screen will handle the transition automatically.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LoadingScreenController : MonoBehaviour
{
    // ── Static API ─────────────────────────────────────────────────
    public static string TargetScene   { get; private set; }
    public static string TargetLabel   { get; private set; }
    public static string IconColorHint { get; private set; } // "blue" | "amber"

    /// <summary>Call this instead of SceneManager.LoadScene() from any script.</summary>
    public static void LoadScene(string sceneName, string displayLabel = null, string iconHint = "blue")
    {
        TargetScene   = sceneName;
        TargetLabel   = string.IsNullOrEmpty(displayLabel) ? sceneName : displayLabel;
        IconColorHint = iconHint;
        SceneManager.LoadScene("LoadingScreen");
    }

    // ── Status messages shown during load ──────────────────────────
    private static readonly string[] StatusMessages =
    {
        "Initialising scene...",
        "Loading assets...",
        "Building digital twin...",
        "Connecting IoT nodes...",
        "Calibrating sensors...",
        "Finalising environment...",
        "Almost ready..."
    };

    // ── UI References ──────────────────────────────────────────────
    private Label         _sceneNameLabel;
    private Label         _loadingStatusLabel;
    private Label         _progressPct;
    private VisualElement _progressFill;
    private VisualElement _sceneIconDot;

    // ── Icon tint colours ──────────────────────────────────────────
    private static readonly Color BlueColor  = new Color(70f/255f, 140f/255f, 220f/255f);
    private static readonly Color AmberColor = new Color(200f/255f, 130f/255f, 50f/255f);
    private static readonly Color GrayColor  = new Color(150f/255f, 150f/255f, 150f/255f);

    /// <summary>
    /// Sets up the loading screen on scene enter: shows the cursor, binds the
    /// progress UI, displays the target scene's label/icon colour, and starts
    /// the async load of <see cref="TargetScene"/> (set by <see cref="LoadScene"/>).
    /// </summary>
    private void OnEnable()
    {
        // UnityEngine.Cursor.visible/lockState are global and persist across scene loads.
        // Always show the cursor on the loading screen — if the destination
        // scene wants it hidden (e.g. the garage's fly camera), it will hide
        // it again in its own Start().
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        var root = GetComponent<UIDocument>().rootVisualElement;

        _sceneNameLabel     = root.Q<Label>("SceneNameLabel");
        _loadingStatusLabel = root.Q<Label>("LoadingStatusLabel");
        _progressPct        = root.Q<Label>("ProgressPct");
        _progressFill       = root.Q<VisualElement>("ProgressFill");
        _sceneIconDot       = root.Q<VisualElement>("SceneIconDot");

        // Apply scene label and icon tint
        if (_sceneNameLabel != null)
            _sceneNameLabel.text = TargetLabel ?? "Loading...";

        if (_sceneIconDot != null)
        {
            var col = (IconColorHint == "amber") ? AmberColor : (IconColorHint == "blue") ? BlueColor : GrayColor;
            _sceneIconDot.style.backgroundColor = new StyleColor(col);
        }

        // Set initial progress
        SetProgress(0f, StatusMessages[0]);

        // Begin async load
        if (!string.IsNullOrEmpty(TargetScene))
            StartCoroutine(LoadAsync());
        else
            Debug.LogWarning("[DuoResidence] LoadingScreen: No target scene set. " +
                             "Use LoadingScreenController.LoadScene(\"SceneName\").");
    }

    // ── Async Load Coroutine ───────────────────────────────────────
    /// <summary>
    /// Asynchronously loads <see cref="TargetScene"/>, smoothing the displayed
    /// progress bar and cycling through <see cref="StatusMessages"/> as it goes,
    /// then activates the new scene once loading reaches 100%.
    /// </summary>
    private IEnumerator LoadAsync()
    {
        yield return null; // let the UI render one frame first

        var op = SceneManager.LoadSceneAsync(TargetScene);
        op.allowSceneActivation = false;

        float displayedProgress = 0f;
        int   lastMsgIndex      = 0;

        while (!op.isDone)
        {
            // Unity reports 0–0.9 while loading; 0.9–1.0 is activation
            float realProgress = Mathf.Clamp01(op.progress / 0.9f);

            // Smooth the displayed progress toward real progress
            displayedProgress = Mathf.MoveTowards(displayedProgress, realProgress, Time.deltaTime * 0.6f);

            // Pick a status message proportional to progress
            int msgIndex = Mathf.Clamp(
                Mathf.FloorToInt(displayedProgress * (StatusMessages.Length - 1)),
                0, StatusMessages.Length - 1);

            if (msgIndex != lastMsgIndex)
            {
                lastMsgIndex = msgIndex;
                SetProgress(displayedProgress, StatusMessages[msgIndex]);
            }
            else
            {
                SetProgress(displayedProgress, null);
            }

            // Once fully loaded, smooth to 100% then activate
            if (op.progress >= 0.9f)
            {
                displayedProgress = Mathf.MoveTowards(displayedProgress, 1f, Time.deltaTime * 0.8f);
                SetProgress(displayedProgress, "Ready!");

                if (displayedProgress >= 0.999f)
                {
                    yield return new WaitForSeconds(0.25f); // brief pause at 100%
                    op.allowSceneActivation = true;
                }
            }

            yield return null;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────
    // Updates the progress bar fill/percentage label, and optionally the
    // status message (pass null to leave the current status text unchanged).
    private void SetProgress(float t, string statusMsg)
    {
        int pct = Mathf.RoundToInt(t * 100f);

        if (_progressFill != null)
            _progressFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));

        if (_progressPct != null)
            _progressPct.text = $"{pct}%";

        if (statusMsg != null && _loadingStatusLabel != null)
            _loadingStatusLabel.text = statusMsg;
    }
}
