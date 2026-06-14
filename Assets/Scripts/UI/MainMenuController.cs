using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// DuoResidence — Main Menu Controller (UI Toolkit)
/// Sidebar: Load Scene / Connect / Settings / Quit.
/// Center: live camera viewport (RenderTexture).
/// Each sidebar button (except Quit) opens a semi-transparent popup
/// over the viewport.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names (must match Build Settings)")]
    public string garageSceneName          = "GarageTwin_Phase1";
    public string streetLightsSceneName    = "StreetLights";
    public string accessControlSceneName   = "AccessControl";
    public string hvacSceneName            = "HVACSystem";
    public string evChargersSceneName      = "EVChargers";
    public string securityCamerasSceneName = "SecurityCameras";

    [Header("Live Camera Preview")]
    [Tooltip("RenderTexture fed by the garage camera (Assets/UI/GarageCameraPreview.renderTexture)")]
    public RenderTexture cameraPreviewTexture;

    // UI references
    private VisualElement _viewport;
    private Label         _viewportPlaceholder;

    private Button _navLoadScene;
    private Button _navConnect;
    private Button _navSettings;

    private VisualElement _loadScenePopup;
    private VisualElement _connectPopup;
    private VisualElement _settingsPopup;

    /// <summary>
    /// Binds the main menu UI: applies the live camera preview to the viewport,
    /// wires sidebar nav buttons to their popups (Load Scene / Connect / Settings),
    /// wires popup close buttons and outside-click-to-close, scene cards,
    /// settings toggles and the Quit button. Also ensures the cursor is visible.
    /// </summary>
    private void OnEnable()
    {
        // UnityEngine.Cursor.visible/lockState are global and persist across scene loads.
        // The garage scene's fly camera (Mover.cs) hides and locks the cursor
        // by default, so make sure it's visible again whenever the menu shows.
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        var root = GetComponent<UIDocument>().rootVisualElement;

        // Viewport
        _viewport            = root.Q<VisualElement>("Viewport");
        _viewportPlaceholder = root.Q<Label>("ViewportPlaceholder");
        ApplyCameraPreview();

        // Nav buttons
        _navLoadScene = root.Q<Button>("NavLoadScene");
        _navConnect   = root.Q<Button>("NavConnect");
        _navSettings  = root.Q<Button>("NavSettings");

        // Popups
        _loadScenePopup = root.Q<VisualElement>("LoadScenePopup");
        _connectPopup   = root.Q<VisualElement>("ConnectPopup");
        _settingsPopup  = root.Q<VisualElement>("SettingsPopup");

        // Nav -> popup wiring
        _navLoadScene.clicked += () => TogglePopup(_loadScenePopup);
        _navConnect.clicked   += () => TogglePopup(_connectPopup);
        _navSettings.clicked  += () => TogglePopup(_settingsPopup);

        // Close buttons
        root.Q<Button>("CloseLoadSceneBtn").clicked += () => HidePopup(_loadScenePopup);
        root.Q<Button>("CloseConnectBtn").clicked    += () => HidePopup(_connectPopup);
        root.Q<Button>("CloseSettingsBtn").clicked   += () => HidePopup(_settingsPopup);

        // Click outside card closes popup
        RegisterOverlayClose(_loadScenePopup);
        RegisterOverlayClose(_connectPopup);
        RegisterOverlayClose(_settingsPopup);

        // Scene cards inside Load Scene popup
        root.Q<Button>("PopupCard_Garage").clicked       += LoadGarageScene;
        root.Q<Button>("PopupCard_StreetLights").clicked += LoadStreetLightsScene;

        // Settings toggles
        WireToggle(root.Q<Button>("ToggleDarkMode"));
        WireToggle(root.Q<Button>("ToggleAutoConnect"));
        WireToggle(root.Q<Button>("ToggleFPS"));

        // Quit
        root.Q<Button>("QuitButton").clicked += QuitApplication;
    }

    /// <summary>
    /// Continuously re-forces the cursor to be visible/unlocked while the main
    /// menu is active, in case another script (e.g. the garage fly camera) hides it.
    /// </summary>
    private void Update()
    {
        // Belt-and-braces: keep the cursor visible while the menu is active,
        // in case any other script tries to hide/lock it during this frame.
        if (UnityEngine.Cursor.visible == false || UnityEngine.Cursor.lockState != CursorLockMode.None)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
    }

    // ─── Camera Preview ──────────────────────────────────────────

    // Shows the live garage camera feed in the viewport if a RenderTexture is
    // assigned, otherwise shows the placeholder label instead.
    private void ApplyCameraPreview()
    {
        if (cameraPreviewTexture != null)
        {
            _viewport.style.backgroundImage =
                new StyleBackground(Background.FromRenderTexture(cameraPreviewTexture));

            if (_viewportPlaceholder != null)
                _viewportPlaceholder.style.display = DisplayStyle.None;
        }
        else
        {
            if (_viewportPlaceholder != null)
                _viewportPlaceholder.style.display = DisplayStyle.Flex;
        }
    }

    // ─── Popups ────────────────────────────────────────────────

    /// <summary>
    /// Closes all sidebar popups, then opens <paramref name="popup"/> only if it
    /// wasn't already the visible one (so clicking the same nav button again closes it).
    /// </summary>
    private void TogglePopup(VisualElement popup)
    {
        bool isVisible = popup.ClassListContains("popup-overlay--visible");

        // Close all popups first
        HidePopup(_loadScenePopup);
        HidePopup(_connectPopup);
        HidePopup(_settingsPopup);

        // Toggle: if it was already open, leave it closed; otherwise open it
        if (!isVisible)
            ShowPopup(popup);
    }

    // Shows / hides a popup overlay via its USS visibility class.
    private void ShowPopup(VisualElement popup) => popup.AddToClassList("popup-overlay--visible");
    private void HidePopup(VisualElement popup) => popup.RemoveFromClassList("popup-overlay--visible");

    // Closes the popup when the user clicks the overlay background itself
    // (i.e. outside the popup card).
    private void RegisterOverlayClose(VisualElement overlay)
    {
        overlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == overlay) HidePopup(overlay);
        });
    }

    // ─── Settings Toggles ────────────────────────────────────────

    // Hooks up a settings toggle button to flip its "on" visual state on click.
    // (Display-only: no persisted settings logic yet.)
    private void WireToggle(Button toggle)
    {
        if (toggle == null) return;
        toggle.clicked += () => toggle.ToggleInClassList("settings-toggle--on");
    }

    // ─── Scene Loading (routed through LoadingScreen) ───────────

    // Sidebar scene-card actions: both route through LoadingScreenController
    // so a themed loading screen is shown during the scene switch.
    private void LoadGarageScene()
        => LoadingScreenController.LoadScene(garageSceneName, "Smart Garage", "blue");

    private void LoadStreetLightsScene()
        => LoadingScreenController.LoadScene(streetLightsSceneName, "Street Lights", "amber");

    // ─── Quit ───────────────────────────────────────────────────

    // Exits Play Mode in the Editor, or quits the built application.
    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
