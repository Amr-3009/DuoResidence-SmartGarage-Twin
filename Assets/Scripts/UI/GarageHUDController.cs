using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// DuoResidence — Garage HUD Controller
/// Handles Back to Dashboard, Scenes popup, and scene navigation.
/// All scene loads route through LoadingScreenController.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GarageHUDController : MonoBehaviour
{
    [Header("Scene Names (must match Build Settings)")]
    public string mainMenuSceneName     = "MainMenu";
    public string streetLightsSceneName = "StreetLights";

    private VisualElement _popupOverlay;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _popupOverlay = root.Q<VisualElement>("PopupOverlay");

        root.Q<Button>("BackToDashboardBtn").clicked += GoToDashboard;
        root.Q<Button>("OpenScenesBtn").clicked      += OpenPopup;
        root.Q<Button>("ClosePopupBtn").clicked      += ClosePopup;

        _popupOverlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _popupOverlay) ClosePopup();
        });

        root.Q<Button>("PopupCard_Garage").clicked       += ClosePopup;
        root.Q<Button>("PopupCard_StreetLights").clicked += GoToStreetLights;
    }

    private void OpenPopup()  => _popupOverlay.AddToClassList("popup-overlay--visible");
    private void ClosePopup() => _popupOverlay.RemoveFromClassList("popup-overlay--visible");

    private void GoToDashboard()
        => LoadingScreenController.LoadScene(mainMenuSceneName, "Dashboard", "blue");

    private void GoToStreetLights()
        => LoadingScreenController.LoadScene(streetLightsSceneName, "Street Lights", "amber");
}
