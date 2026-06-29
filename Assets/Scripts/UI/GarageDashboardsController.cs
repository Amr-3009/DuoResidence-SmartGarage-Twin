using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;
using TMPro;

using UIButton = UnityEngine.UIElements.Button;
using UISlider = UnityEngine.UIElements.Slider;



/// <summary>
/// DuoResidence — Garage Dashboards Controller (UI Toolkit)
///
/// Bridges the existing UGUI dashboards (Wall/Parking, HVAC, Fans, CCTV)
/// into a new themed UI Toolkit popup, without touching their underlying
/// MQTT / networking / streaming logic:
///   - Wall (Parking): re-implements the grid + capacity/lane summary by
///     listening to the same MQTT telemetry events as TwinDashboardController.
///   - HVAC / Fans: polls the live values already being written into the
///     hidden HvacDashboardController / FanDashboardController, and mirrors
///     them into the new UI. Button/dropdown actions call straight through
///     to the original controllers' public methods.
///   - CCTV: wires buttons/sliders directly to CCTVClient's public API and
///     mirrors its live video texture + status text.
///
/// The original Canvas dashboards are disabled (Canvas + GraphicRaycaster)
/// so only this new UI is visible, but their GameObjects/components stay
/// active and keep running.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GarageDashboardsController : MonoBehaviour
{
    // ── Parking / Wall config (mirrors TwinDashboardController) ─────
    private const int SlotsPerLane = 40;
    private const int TotalCapacity = SlotsPerLane * 3;

    private readonly Color _vacantColor   = new Color(100f/255f, 180f/255f, 40f/255f);
    private readonly Color _occupiedColor = new Color(200f/255f, 70f/255f, 70f/255f);

    private Dictionary<string, bool> _capacityMap = new Dictionary<string, bool>();
    private Dictionary<string, VisualElement> _tileMap = new Dictionary<string, VisualElement>();

    // ── References to existing (now-hidden) controllers ─────────────
    private HvacDashboardController _hvac;
    private FanDashboardController  _fan;
    private CCTVClient               _cctv;
    private TwinDashboardController  _wall;

    // ── Manual override: 'M' key forces the 4 original Canvas dashboards
    // visible/hidden, independent of the new UI Toolkit popup above. ───
    private bool _originalCanvasesForcedVisible;

    // ── UI references ────────────────────────────────────────────────
    private VisualElement _overlay;
    private Label _facilityStatusLabel;
    private Label _capacityPercentLabel;
    private VisualElement _capacityFill;
    private Label _totalCarsLabel;
    private Label _laneAStatus, _laneBStatus, _laneCStatus;
    private VisualElement _laneAGrid, _laneBGrid, _laneCGrid;

    private VisualElement _co2Fill, _noFill;
    private Label _co2ValueLabel, _noValueLabel;

    private Label _bigFanLabel, _smallFanLabel;
    private DropdownField _durationDropdown;
    private UIButton _increaseRpmBtn;

    private Label _camLabel;
    private UIButton _sessionBtn;
    private VisualElement _cctvStream;
    private UIButton _btnOverview, _btnEntrance, _btnExit, _btnResetView;
    private UIButton[] _camButtons;
    private UISlider _sliderZoom, _sliderPanX, _sliderPanY;
    private bool _cctvSlidersWired;


    // Tab buttons and their corresponding content panels (Parking/HVAC/Fans/CCTV).
    private UIButton[] _tabButtons;
    private VisualElement[] _tabContents;

    /// <summary>
    /// Caches references to every UI Toolkit element used by the dashboards popup,
    /// wires up the tab buttons, and builds the parking grid on startup.
    /// </summary>
private void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // ── Overlay & tabs ────────────────────────────────────────
        _overlay = root.Q<VisualElement>("DashboardsOverlay");

        _tabButtons = new[]
        {
            root.Q<UIButton>("Tab_Parking"),
            root.Q<UIButton>("Tab_HVAC"),
            root.Q<UIButton>("Tab_Fans"),
            root.Q<UIButton>("Tab_CCTV"),
        };
        _tabContents = new[]
        {
            root.Q<VisualElement>("Tab_Parking_Content"),
            root.Q<VisualElement>("Tab_HVAC_Content"),
            root.Q<VisualElement>("Tab_Fans_Content"),
            root.Q<VisualElement>("Tab_CCTV_Content"),
        };

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int idx = i;
            _tabButtons[i].clicked += () => SelectTab(idx);
        }

        // ── Parking (Wall) ──────────────────────────────────────────
        _facilityStatusLabel  = root.Q<Label>("FacilityStatusLabel");
        _capacityPercentLabel = root.Q<Label>("CapacityPercentLabel");
        _capacityFill         = root.Q<VisualElement>("CapacityProgressFill");
        _totalCarsLabel       = root.Q<Label>("TotalCarsLabel");
        _laneAStatus = root.Q<Label>("LaneA_StatusLabel");
        _laneBStatus = root.Q<Label>("LaneB_StatusLabel");
        _laneCStatus = root.Q<Label>("LaneC_StatusLabel");
        _laneAGrid = root.Q<VisualElement>("LaneA_Grid");
        _laneBGrid = root.Q<VisualElement>("LaneB_Grid");
        _laneCGrid = root.Q<VisualElement>("LaneC_Grid");

        // ── HVAC ────────────────────────────────────────────────────
        _co2Fill       = root.Q<VisualElement>("CO2ProgressFill");
        _noFill        = root.Q<VisualElement>("NOProgressFill");
        _co2ValueLabel = root.Q<Label>("CO2ValueLabel");
        _noValueLabel  = root.Q<Label>("NOValueLabel");

        // ── Fans ────────────────────────────────────────────────────
        _bigFanLabel      = root.Q<Label>("BigFanRpmLabel");
        _smallFanLabel    = root.Q<Label>("SmallFanRpmLabel");
        _durationDropdown = root.Q<DropdownField>("DurationDropdown");
        _increaseRpmBtn   = root.Q<UIButton>("IncreaseRpmBtn");

        // ── CCTV ────────────────────────────────────────────────────
        _camLabel    = root.Q<Label>("CamLabel");
        _sessionBtn  = root.Q<UIButton>("SessionBtn");
        _cctvStream  = root.Q<VisualElement>("CCTVStreamDisplay");
        _btnOverview = root.Q<UIButton>("Btn_Overview");
        _btnEntrance = root.Q<UIButton>("Btn_Entrance");
        _btnExit     = root.Q<UIButton>("Btn_Exit");
        _btnResetView = root.Q<UIButton>("Btn_ResetView");
        _camButtons  = new[] { _btnOverview, _btnEntrance, _btnExit };
        _sliderZoom  = root.Q<UISlider>("SliderOverview_Zoom");
        _sliderPanX  = root.Q<UISlider>("SliderOverview_X");
        _sliderPanY  = root.Q<UISlider>("SliderOverview_Y");

        BuildParkingGrid();
        SelectTab(0);
    }

    /// <summary>
    /// Locates the original UGUI dashboard controllers (HVAC, Fan, CCTV, Twin),
    /// hides their canvases so only this UI Toolkit popup renders, subscribes to
    /// MQTT telemetry, and wires up the Fan and CCTV controls to those controllers.
    /// </summary>
private void Start()
    {
        // ── Locate original controllers, disable their canvases ─────
        _hvac = FindObjectOfType<HvacDashboardController>(true);
        _fan  = FindObjectOfType<FanDashboardController>(true);
        _cctv = FindObjectOfType<CCTVClient>(true);

                _wall = FindObjectOfType<TwinDashboardController>(true);
        DisableCanvas(_wall);
        DisableCanvas(_hvac);
        DisableCanvas(_fan);
        DisableCanvas(_cctv);

        // ── Telemetry subscription (mirrors TwinDashboardController) ─
        MQTTConnectionManager.OnTelemetryMessageReceived += OnTelemetry;

        // ── Fans: wire controls ──────────────────────────────────────
        if (_increaseRpmBtn != null)
            _increaseRpmBtn.clicked += () => _fan?.ExecuteVentilationOverride();

        if (_durationDropdown != null)
        {
            _durationDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_fan?.DurationDropdown != null)
                    _fan.DurationDropdown.value = _durationDropdown.index;
            });
        }

        // ── CCTV: wire camera buttons ────────────────────────────────
        WireCamButton(_btnOverview, "OVERVIEW");
        WireCamButton(_btnEntrance, "ENTRANCE");
        WireCamButton(_btnExit,     "EXIT");
        SetActiveCamButton(_btnOverview);

        if (_sessionBtn != null)
            _sessionBtn.clicked += () => _cctv?.ToggleMasterSession();

        if (_btnResetView != null)
            _btnResetView.clicked += () => _cctv?.ResetSlidersAndTransform();

        WireCctvSliders();
    }

    /// <summary>
    /// Unsubscribes from MQTT telemetry to avoid leaking the event handler
    /// when this object is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        MQTTConnectionManager.OnTelemetryMessageReceived -= OnTelemetry;
    }

    // ─────────────────────────────────────────────────────────────────
    // Canvas hiding
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Turns off the Canvas and GraphicRaycaster on an original UGUI dashboard
    /// so it stops rendering/blocking input, while leaving its GameObject and
    /// scripts active (their logic keeps running in the background).
    /// </summary>
    private void DisableCanvas(Component c)
    {
        if (c == null) return;
        var canvas = c.GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = false;
        var raycaster = c.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;
    }

    /// <summary>
    /// Turns the Canvas and GraphicRaycaster back on for an original UGUI
    /// dashboard, re-enabling rendering and input on it.
    /// </summary>
    private void EnableCanvas(Component c)
    {
        if (c == null) return;
        var canvas = c.GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = true;
        var raycaster = c.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = true;
    }

    /// <summary>
    /// 'M' key handler: force-toggles the 4 original Canvas dashboards
    /// (Wall/Parking, HVAC, Fans, CCTV) visible/hidden, independent of the
    /// new UI Toolkit popup's own open/closed state.
    /// </summary>
    public void ToggleOriginalCanvases()
    {
        _originalCanvasesForcedVisible = !_originalCanvasesForcedVisible;

        if (_originalCanvasesForcedVisible)
        {
            EnableCanvas(_wall);
            EnableCanvas(_hvac);
            EnableCanvas(_fan);
            EnableCanvas(_cctv);
        }
        else
        {
            DisableCanvas(_wall);
            DisableCanvas(_hvac);
            DisableCanvas(_fan);
            DisableCanvas(_cctv);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Popup open/close + Tabs
    // ─────────────────────────────────────────────────────────────────

    // Shows / hides the dashboards popup overlay via USS class toggling.
    public void Open()  => _overlay.AddToClassList("popup-overlay--visible");
    public void Close() => _overlay.RemoveFromClassList("popup-overlay--visible");

    /// <summary>
    /// Switches the visible tab: shows the content panel matching <paramref name="index"/>
    /// and highlights its tab button, hiding/unhighlighting all the others.
    /// </summary>
    private void SelectTab(int index)
    {
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            bool active = i == index;
            _tabContents[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (active) _tabButtons[i].AddToClassList("dash-tab--active");
            else        _tabButtons[i].RemoveFromClassList("dash-tab--active");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Parking / Wall grid
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds all three parking lane grids (A, B, C) and refreshes the
    /// capacity summary labels/progress bar.
    /// </summary>
    private void BuildParkingGrid()
    {
        BuildLane("A", _laneAGrid);
        BuildLane("B", _laneBGrid);
        BuildLane("C", _laneCGrid);
        RefreshParkingSummary();
    }

    /// <summary>
    /// Creates one tile per slot for the given lane (e.g. A01..A40), starting
    /// in the vacant state, and registers each tile in <see cref="_tileMap"/>
    /// so it can be updated later from telemetry.
    /// </summary>
    private void BuildLane(string lane, VisualElement grid)
    {
        if (grid == null) return;
        for (int i = 1; i <= SlotsPerLane; i++)
        {
            string slotID = lane + i.ToString("D2");
            _capacityMap[slotID] = true; // vacant by default

            var tile = new VisualElement();
            tile.AddToClassList("dash-parking-tile");
            tile.tooltip = slotID;
            grid.Add(tile);
            _tileMap[slotID] = tile;
        }
    }

    /// <summary>
    /// MQTT telemetry handler. Updates the "total cars entered" label when the
    /// entrance-count topic arrives, and updates the matching parking tile's
    /// occupied/vacant state (plus the lane summary) when a slot-status topic arrives.
    /// </summary>
    private void OnTelemetry(string topic, string payload)
    {
        // Total cars (matches TwinSyncManager's traffic topic)
        if (topic == "DuoResidence/Amr/Garage/Traffic/EntranceCount")
        {
            if (_totalCarsLabel != null)
                _totalCarsLabel.text = $"Total Cars Entered This Session: {payload}";
            return;
        }

        // Slot occupancy (matches TwinDashboardController)
        string[] parts = topic.Split('/');
        if (parts.Length < 6) return;

        string slotID = parts[parts.Length - 1];
        bool isVacant = payload.ToUpperInvariant().Contains("IS VACANT");

        if (_capacityMap.ContainsKey(slotID))
        {
            _capacityMap[slotID] = isVacant;
            if (_tileMap.TryGetValue(slotID, out var tile))
            {
                if (isVacant) tile.RemoveFromClassList("dash-parking-tile--occupied");
                else tile.AddToClassList("dash-parking-tile--occupied");
            }
            RefreshParkingSummary();
        }
    }

    /// <summary>
    /// Recomputes occupied counts for lanes A/B/C from <see cref="_capacityMap"/>,
    /// then updates the lane status labels, the capacity progress bar/percentage,
    /// and the overall "FACILITY FULL" / "MONITORING ACTIVE" status pill.
    /// </summary>
    private void RefreshParkingSummary()
    {
        int occA = 0, occB = 0, occC = 0;
        foreach (var slot in _capacityMap)
        {
            if (!slot.Value)
            {
                if (slot.Key.StartsWith("A")) occA++;
                else if (slot.Key.StartsWith("B")) occB++;
                else if (slot.Key.StartsWith("C")) occC++;
            }
        }

        int total = occA + occB + occC;
        float pct = (float)total / TotalCapacity * 100f;

        if (_laneAStatus != null) _laneAStatus.text = $"Lane A: {occA} / {SlotsPerLane} Occupied";
        if (_laneBStatus != null) _laneBStatus.text = $"Lane B: {occB} / {SlotsPerLane} Occupied";
        if (_laneCStatus != null) _laneCStatus.text = $"Lane C: {occC} / {SlotsPerLane} Occupied";

        if (_capacityFill != null)
        {
            _capacityFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            _capacityFill.style.backgroundColor = new StyleColor(Color.Lerp(_vacantColor, _occupiedColor, pct / 100f));
        }

        if (_capacityPercentLabel != null)
            _capacityPercentLabel.text = $"{pct:F0}% Full";

        if (_facilityStatusLabel != null)
        {
            bool full = total >= TotalCapacity;
            _facilityStatusLabel.text = full ? "FACILITY FULL" : "MONITORING ACTIVE";
            _facilityStatusLabel.RemoveFromClassList(full ? "dash-status-pill--ok" : "dash-status-pill--off");
            _facilityStatusLabel.AddToClassList(full ? "dash-status-pill--off" : "dash-status-pill--ok");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Live polling: HVAC / Fans / CCTV
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// While the dashboards popup is visible, refreshes the HVAC, Fan and CCTV
    /// tabs every frame by polling the original (hidden) controllers.
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
            ToggleOriginalCanvases();

        if (!_overlay.ClassListContains("popup-overlay--visible")) return;

        UpdateHvacTab();
        UpdateFanTab();
        UpdateCctvTab();
    }

    // Mirrors the hidden HvacDashboardController's CO2/NO slider values, fill
    // colors and text labels onto this UI's CO2/NO progress bars and labels.
    private void UpdateHvacTab()
    {
        if (_hvac == null) return;

        if (_hvac.Co2Slider != null && _co2Fill != null)
        {
            float pct = Mathf.InverseLerp(_hvac.Co2Slider.minValue, _hvac.Co2Slider.maxValue, _hvac.Co2Slider.value) * 100f;
            _co2Fill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            if (_hvac.Co2SliderFill != null)
                _co2Fill.style.backgroundColor = new StyleColor(_hvac.Co2SliderFill.color);
        }
        if (_hvac.Co2ValueText != null && _co2ValueLabel != null)
            _co2ValueLabel.text = _hvac.Co2ValueText.text;

        if (_hvac.NoSlider != null && _noFill != null)
        {
            float pct = Mathf.InverseLerp(_hvac.NoSlider.minValue, _hvac.NoSlider.maxValue, _hvac.NoSlider.value) * 100f;
            _noFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            if (_hvac.NoSliderFill != null)
                _noFill.style.backgroundColor = new StyleColor(_hvac.NoSliderFill.color);
        }
        if (_hvac.NoValueText != null && _noValueLabel != null)
            _noValueLabel.text = _hvac.NoValueText.text;
    }

    // Mirrors the hidden FanDashboardController's RPM labels, button text and
    // interactable states onto this UI's fan labels, button and dropdown.
    private void UpdateFanTab()
    {
        if (_fan == null) return;

        if (_fan.BigFanRpmText != null && _bigFanLabel != null)
            _bigFanLabel.text = _fan.BigFanRpmText.text;

        if (_fan.SmallFanRpmText != null && _smallFanLabel != null)
            _smallFanLabel.text = _fan.SmallFanRpmText.text;

        if (_fan.ButtonLabelText != null && _increaseRpmBtn != null)
            _increaseRpmBtn.text = _fan.ButtonLabelText.text;

        if (_fan.IncreaseRpmButton != null && _increaseRpmBtn != null)
            _increaseRpmBtn.SetEnabled(_fan.IncreaseRpmButton.interactable);

        if (_fan.DurationDropdown != null && _durationDropdown != null)
            _durationDropdown.SetEnabled(_fan.DurationDropdown.interactable);
    }

    // Mirrors the hidden CCTVClient's camera label, session button text/state
    // and live stream texture onto this UI, then refreshes the calibration sliders.
    private void UpdateCctvTab()
    {
        if (_cctv == null) return;

        if (_cctv.camLabelText != null && _camLabel != null)
        {
            _camLabel.text = _cctv.camLabelText.text;
            bool live = _cctv.camLabelText.text.StartsWith("LIVE");
            _camLabel.RemoveFromClassList(live ? "dash-status-pill--off" : "dash-status-pill--ok");
            _camLabel.AddToClassList(live ? "dash-status-pill--ok" : "dash-status-pill--off");
        }

        if (_cctv.sessionButtonText != null && _sessionBtn != null)
        {
            _sessionBtn.text = _cctv.sessionButtonText.text;
            bool active = _cctv.sessionButtonText.text.Contains("TERMINATE");
            if (active) _sessionBtn.RemoveFromClassList("dash-action-btn--green");
            else _sessionBtn.AddToClassList("dash-action-btn--green");
        }

        if (_cctv.streamDisplay != null && _cctv.streamDisplay.texture != null && _cctvStream != null)
        {
            _cctvStream.style.backgroundImage = new StyleBackground(Background.FromTexture2D((Texture2D)_cctv.streamDisplay.texture));
        }

        UpdateCctvCalibration();
    }

    // ─────────────────────────────────────────────────────────────────
    // Wiring helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// One-time hookup (guarded by <see cref="_cctvSlidersWired"/>) that forwards
    /// changes on this UI's zoom/pan sliders to CCTVClient's corresponding sliders.
    /// </summary>
    private void WireCctvSliders()
    {
        if (_cctvSlidersWired || _cctv == null) return;
        _cctvSlidersWired = true;

        if (_sliderZoom != null && _cctv.sliderOverview_Zoom != null)
            _sliderZoom.RegisterValueChangedCallback(evt => _cctv.sliderOverview_Zoom.value = evt.newValue);

        if (_sliderPanX != null && _cctv.sliderOverview_X != null)
            _sliderPanX.RegisterValueChangedCallback(evt => _cctv.sliderOverview_X.value = evt.newValue);

        if (_sliderPanY != null && _cctv.sliderOverview_Y != null)
            _sliderPanY.RegisterValueChangedCallback(evt => _cctv.sliderOverview_Y.value = evt.newValue);
    }

    /// <summary>
    /// Syncs this UI's zoom/pan-X/pan-Y sliders (range, value, enabled state)
    /// with CCTVClient's sliders, and mirrors the reset-view button's enabled state.
    /// </summary>
    private void UpdateCctvCalibration()
    {
        if (_cctv == null) return;

        var zoom = _cctv.sliderOverview_Zoom;
        var panX = _cctv.sliderOverview_X;
        var panY = _cctv.sliderOverview_Y;

        if (_sliderZoom != null && zoom != null)
        {
            _sliderZoom.lowValue  = zoom.minValue;
            _sliderZoom.highValue = zoom.maxValue;
            _sliderZoom.SetValueWithoutNotify(zoom.value);
            _sliderZoom.SetEnabled(zoom.interactable);
        }

        if (_sliderPanX != null && panX != null)
        {
            _sliderPanX.lowValue  = panX.minValue;
            _sliderPanX.highValue = panX.maxValue;
            _sliderPanX.SetValueWithoutNotify(panX.value);
            _sliderPanX.SetEnabled(panX.interactable);
        }

        if (_sliderPanY != null && panY != null)
        {
            _sliderPanY.lowValue  = panY.minValue;
            _sliderPanY.highValue = panY.maxValue;
            _sliderPanY.SetValueWithoutNotify(panY.value);
            _sliderPanY.SetEnabled(panY.interactable);
        }

        if (_btnResetView != null && _cctv.btnResetView != null)
            _btnResetView.SetEnabled(_cctv.btnResetView.interactable);
    }

    
    /// <summary>
    /// Hooks up a camera-select button: clicking it tells CCTVClient to switch
    /// to <paramref name="mode"/> and marks the button as the active camera button.
    /// </summary>
private void WireCamButton(UIButton btn, string mode)
    {
        if (btn == null) return;
        btn.clicked += () =>
        {
            _cctv?.SelectCameraView(mode);
            SetActiveCamButton(btn);
        };
    }

    // Applies the "active" highlight style to the given camera button and
    // removes it from the others.
    private void SetActiveCamButton(UIButton active)
    {
        foreach (var b in _camButtons)
        {
            if (b == null) continue;
            if (b == active) b.AddToClassList("dash-cam-btn--active");
            else b.RemoveFromClassList("dash-cam-btn--active");
        }
    }
}
