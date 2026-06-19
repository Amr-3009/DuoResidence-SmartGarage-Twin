using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// DuoResidence — Garage HUD Controller
/// Handles the hamburger dropdown menu (Main Menu / Dashboards / Scenes),
/// the Scenes popup, the Dashboards popup, and scene navigation.
/// All scene loads route through LoadingScreenController.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GarageHUDController : MonoBehaviour
{
    [Header("Scene Names (must match Build Settings)")]
    public string mainMenuSceneName     = "MainMenu";
    public string streetLightsSceneName = "StreetLights";
    public string vrSceneName           = "Scene_VR_Enviroment";

    /// <summary>
    /// True whenever any popup (Scenes or Dashboards) is visible.
    /// Mover reads this to keep the cursor free while a popup is open.
    /// </summary>
    public static bool IsAnyPopupOpen { get; private set; }

    
private VisualElement _popupOverlay;
    private VisualElement _dashboardsOverlay;
    private GarageDashboardsController _dashboardsController;

    private Button _hamburgerBtn;
    private VisualElement _hamburgerMenu;

    /// <summary>
    /// Binds all HUD UI Toolkit elements and wires up the hamburger dropdown,
    /// the Scenes popup and the Dashboards popup (open/close + outside-click-to-close).
    /// </summary>
    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _popupOverlay      = root.Q<VisualElement>("PopupOverlay");
        _dashboardsOverlay = root.Q<VisualElement>("DashboardsOverlay");
        _dashboardsController = GetComponent<GarageDashboardsController>();

        // ── Hamburger dropdown menu ──────────────────────────────
        _hamburgerBtn  = root.Q<Button>("HamburgerMenuBtn");
        _hamburgerMenu = root.Q<VisualElement>("HamburgerMenu");
        _hamburgerMenu.pickingMode = PickingMode.Ignore;

        _hamburgerBtn.clicked += ToggleHamburgerMenu;

        root.Q<Button>("MenuItem_MainMenu").clicked += () =>
        {
            CloseHamburgerMenu();
            GoToMainMenu();
        };

        root.Q<Button>("MenuItem_Dashboards").clicked += () =>
        {
            CloseHamburgerMenu();
            OpenDashboards();
        };

        root.Q<Button>("MenuItem_Scenes").clicked += () =>
        {
            CloseHamburgerMenu();
            OpenPopup();
        };

        // Close the dropdown on any click outside the menu/button.
        root.RegisterCallback<ClickEvent>(evt =>
        {
            if (!_hamburgerMenu.ClassListContains("hud-menu-dropdown--open"))
                return;

            var target = evt.target as VisualElement;
            if (target == _hamburgerBtn ||
                (target != null && (_hamburgerBtn.Contains(target) || _hamburgerMenu.Contains(target))))
                return;

            CloseHamburgerMenu();
        });

        // ── Scenes popup ──────────────────────────────────────────
        root.Q<Button>("ClosePopupBtn").clicked += ClosePopup;

        _popupOverlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _popupOverlay) ClosePopup();
        });

        root.Q<Button>("PopupCard_Garage").clicked       += ClosePopup;
        root.Q<Button>("PopupCard_StreetLights").clicked += GoToStreetLights;
        root.Q<Button>("PopupCard_VR").clicked           += GoToVR;

        // ── Dashboards popup ───────────────────────────────────────
        root.Q<Button>("CloseDashboardsBtn").clicked += CloseDashboards;

        _dashboardsOverlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _dashboardsOverlay) CloseDashboards();
        });
    }

private void Update()
    {
        if (UnityEngine.Input.GetKeyDown(KeyCode.B))
            OpenDashboards();

        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsAnyPopupOpen || _hamburgerMenu.ClassListContains("hud-menu-dropdown--open"))
            {
                ClosePopup();
                CloseDashboards();
                CloseHamburgerMenu();
            }
            else
            {
                OpenHamburgerMenu();
            }
        }

        // Keep the static flag in sync every frame so Mover always has the right value.
        // Hamburger menu counts as a popup for cursor/movement purposes.
        IsAnyPopupOpen =
            _popupOverlay.ClassListContains("popup-overlay--visible") ||
            _dashboardsOverlay.ClassListContains("popup-overlay--visible") ||
            _hamburgerMenu.ClassListContains("hud-menu-dropdown--open");
    }


    // Opens the hamburger dropdown if closed, or closes it if already open.
    private void ToggleHamburgerMenu()
    {
        if (_hamburgerMenu.ClassListContains("hud-menu-dropdown--open"))
            CloseHamburgerMenu();
        else
            OpenHamburgerMenu();
    }

    // Shows the hamburger dropdown and lets it receive pointer input.
    private void OpenHamburgerMenu()
    {
        _hamburgerMenu.AddToClassList("hud-menu-dropdown--open");
        _hamburgerMenu.pickingMode = PickingMode.Position;
    }

    // Hides the hamburger dropdown and stops it from blocking clicks underneath.
    private void CloseHamburgerMenu()
    {
        _hamburgerMenu.RemoveFromClassList("hud-menu-dropdown--open");
        _hamburgerMenu.pickingMode = PickingMode.Ignore;
    }

    // Shows / hides the "Scenes" selection popup overlay.
    private void OpenPopup()  => _popupOverlay.AddToClassList("popup-overlay--visible");
    private void ClosePopup() => _popupOverlay.RemoveFromClassList("popup-overlay--visible");

    /// <summary>
    /// Opens the dashboards popup, preferring GarageDashboardsController.Open()
    /// when present, otherwise falling back to toggling the overlay's USS class directly.
    /// </summary>
    private void OpenDashboards()
    {
        if (_dashboardsController != null) _dashboardsController.Open();
        else _dashboardsOverlay.AddToClassList("popup-overlay--visible");
    }

    // Closes the dashboards popup, mirroring OpenDashboards()'s fallback logic.
    private void CloseDashboards()
    {
        if (_dashboardsController != null) _dashboardsController.Close();
        else _dashboardsOverlay.RemoveFromClassList("popup-overlay--visible");
    }

    // Scene-navigation shortcuts: both route through LoadingScreenController
    // so a themed loading screen is shown during the scene switch.
    private void GoToMainMenu()
        => LoadingScreenController.LoadScene(mainMenuSceneName, "Main Menu", "blue");

    private void GoToStreetLights()
        => LoadingScreenController.LoadScene(streetLightsSceneName, "Street Lights", "amber");

    private void GoToVR()
        => LoadingScreenController.LoadScene(vrSceneName, "Garage VR", "gray");
}
