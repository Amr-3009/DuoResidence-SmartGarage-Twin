using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

/// <summary>
/// DuoResidence — Main Menu Controller (UI Toolkit)
/// Attach to the UIDocument GameObject in the MainMenu scene.
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

    private VisualElement _dashboardPanel;
    private VisualElement _loadScenePanel;
    private Button        _navDashboard;
    private Button        _navLoadScene;
    private Label         _pageTitle;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _dashboardPanel = root.Q<VisualElement>("DashboardPanel");
        _loadScenePanel = root.Q<VisualElement>("LoadScenePanel");
        _navDashboard   = root.Q<Button>("NavDashboard");
        _navLoadScene   = root.Q<Button>("NavLoadScene");
        _pageTitle      = root.Q<Label>("PageTitle");

        _navDashboard.clicked                       += ShowDashboard;
        _navLoadScene.clicked                       += ShowLoadScene;
        root.Q<Button>("GoToScenesBtn").clicked     += ShowLoadScene;
        root.Q<Button>("Card_Garage").clicked       += LoadGarageScene;
        root.Q<Button>("Card_StreetLights").clicked += LoadStreetLightsScene;
        root.Q<Button>("QuitButton").clicked        += QuitApplication;

        ShowDashboard();
    }

    public void ShowDashboard()
    {
        _dashboardPanel.style.display = DisplayStyle.Flex;
        _loadScenePanel.style.display = DisplayStyle.None;
        _pageTitle.text = "Dashboard";
        _navDashboard.AddToClassList("nav-item--active");
        _navLoadScene.RemoveFromClassList("nav-item--active");
    }

    public void ShowLoadScene()
    {
        _dashboardPanel.style.display = DisplayStyle.None;
        _loadScenePanel.style.display = DisplayStyle.Flex;
        _pageTitle.text = "Load Scene";
        _navLoadScene.AddToClassList("nav-item--active");
        _navDashboard.RemoveFromClassList("nav-item--active");
    }

    // Routes all scene loads through the loading screen
    private void LoadGarageScene()
        => LoadingScreenController.LoadScene(garageSceneName, "Smart Garage", "blue");

    private void LoadStreetLightsScene()
        => LoadingScreenController.LoadScene(streetLightsSceneName, "Street Lights", "amber");

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetBadgeText(string badgeName, string text, bool isWarning = false)
    {
        var root  = GetComponent<UIDocument>().rootVisualElement;
        var badge = root.Q<Label>(badgeName);
        if (badge == null) return;
        badge.text = text;
        badge.RemoveFromClassList(isWarning ? "badge--online" : "badge--warning");
        badge.AddToClassList(isWarning      ? "badge--warning" : "badge--online");
    }
}
