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

    private VisualElement _popupOverlay;
    private VisualElement _dashboardsOverlay;
    private GarageDashboardsController _dashboardsController;

    private Button _hamburgerBtn;
    private VisualElement _hamburgerMenu;

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

        // ── Dashboards popup ───────────────────────────────────────
        root.Q<Button>("CloseDashboardsBtn").clicked += CloseDashboards;

        _dashboardsOverlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _dashboardsOverlay) CloseDashboards();
        });
    }

    private void ToggleHamburgerMenu()
    {
        if (_hamburgerMenu.ClassListContains("hud-menu-dropdown--open"))
            CloseHamburgerMenu();
        else
            OpenHamburgerMenu();
    }

    private void OpenHamburgerMenu()
    {
        _hamburgerMenu.AddToClassList("hud-menu-dropdown--open");
        _hamburgerMenu.pickingMode = PickingMode.Position;
    }

    private void CloseHamburgerMenu()
    {
        _hamburgerMenu.RemoveFromClassList("hud-menu-dropdown--open");
        _hamburgerMenu.pickingMode = PickingMode.Ignore;
    }

    private void OpenPopup()  => _popupOverlay.AddToClassList("popup-overlay--visible");
    private void ClosePopup() => _popupOverlay.RemoveFromClassList("popup-overlay--visible");

    private void OpenDashboards()
    {
        if (_dashboardsController != null) _dashboardsController.Open();
        else _dashboardsOverlay.AddToClassList("popup-overlay--visible");
    }

    private void CloseDashboards()
    {
        if (_dashboardsController != null) _dashboardsController.Close();
        else _dashboardsOverlay.RemoveFromClassList("popup-overlay--visible");
    }

    private void GoToMainMenu()
        => LoadingScreenController.LoadScene(mainMenuSceneName, "Main Menu", "blue");

    private void GoToStreetLights()
        => LoadingScreenController.LoadScene(streetLightsSceneName, "Street Lights", "amber");
}
