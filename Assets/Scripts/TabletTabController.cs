using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DuoResidence — Tablet Hub Tab Controller (UGUI)
///
/// Drives the in-scene tablet's bottom navigation bar, switching between the
/// Parking, Air Quality and CCTV content panels by enabling only the panel
/// matching the clicked tab.
/// </summary>
public class TabletTabController : MonoBehaviour
{
    [Header("📱 Tablet Navigation Tab Buttons")]
    [SerializeField] private Button btnTabParking;
    [SerializeField] private Button btnTabAirQuality;
    [SerializeField] private Button btnTabCCTV;

    [Header("📦 Main Hub Content Panels")]
    [SerializeField] private GameObject panelParking;
    [SerializeField] private GameObject panelAirQuality;
    [SerializeField] private GameObject panelCCTV;

    // Wires each nav button to switch to its corresponding tab, and selects
    // the Parking tab as the default view.
    private void Start()
    {
        // Programmatically bind click actions to the navigation bar buttons
        if (btnTabParking != null) btnTabParking.onClick.AddListener(() => SwitchActiveTab(0));
        if (btnTabAirQuality != null) btnTabAirQuality.onClick.AddListener(() => SwitchActiveTab(1));
        if (btnTabCCTV != null) btnTabCCTV.onClick.AddListener(() => SwitchActiveTab(2));

        // Enforce the Parking screen as our default landing view on initialization
        SwitchActiveTab(0);
    }

    /// <summary>
    /// Smoothly swaps out the visible workspace profile depending on the navigation bar click index
    /// </summary>
    private void SwitchActiveTab(int targetTabIndex)
    {
        if (panelParking != null) panelParking.SetActive(targetTabIndex == 0);
        if (panelAirQuality != null) panelAirQuality.SetActive(targetTabIndex == 1);
        if (panelCCTV != null) panelCCTV.SetActive(targetTabIndex == 2);

        Debug.Log($"[Tablet System Router] Active view panel context shifted to Index: {targetTabIndex}");
    }
}