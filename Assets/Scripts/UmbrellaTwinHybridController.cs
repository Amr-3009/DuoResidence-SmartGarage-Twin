using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

[RequireComponent(typeof(Animator))]
public class UmbrellaTwinHybridController : MonoBehaviour
{
    private enum SystemMode { Automatic, ForcedOpen, ForcedClose }

    [Header("Inbound MQTT Telemetry Topics")]
    [SerializeField] private string windTopic = "DuoResidence/Amr/SmartShades/Wind";
    [SerializeField] private string rainTopic = "DuoResidence/Amr/SmartShades/Rain";
    [SerializeField] private string solarTopic = "DuoResidence/Amr/SmartShades/Solar";

    [Header("Outbound WebSocket Command Configuration")]
    [Tooltip("The WebSocket server URL handling your manual override commands.")]
    [SerializeField] private string webSocketURL = "ws://localhost:8080";

    [Header("UI Visibility Control")]
    [SerializeField] private GameObject dashboardOverlay;
    [SerializeField] private KeyCode toggleKey = KeyCode.U;

    [Header("Left Panel: UI Readouts")]
    [SerializeField] private TextMeshProUGUI windDisplay;
    [SerializeField] private TextMeshProUGUI rainDisplay;
    [SerializeField] private TextMeshProUGUI solarDisplay;
    [SerializeField] private TextMeshProUGUI systemModeDisplay;

    [Header("Right Panel: UI Buttons")]
    [SerializeField] private Button forceOpenButton;
    [SerializeField] private Button forceCloseButton;
    [SerializeField] private Button resumeAutoButton;

    [Header("Threshold Configuration (Matches Sim A)")]
    [SerializeField] private float windSpeedLimit = 12.0f;    
    [SerializeField] private float rainIntensityLimit = 15.0f; 
    [SerializeField] private float solarOpeningThreshold = 300.0f;

    private Animator _animator;
    private SystemMode _currentMode = SystemMode.Automatic;
    
    // Native WebSocket objects
    private ClientWebSocket _webSocket = null;
    private CancellationTokenSource _cts;

    // Telemetry storage
    private float _windSpeed = 0f;
    private float _rainIntensity = 0f;
    private float _solarIrradiance = 0f;
    private bool _lastCanopyState = false;

    private void OnEnable()
    {
        // Hook into your custom manager's thread-safe message broadcaster
        MQTTConnectionManager.OnTelemetryMessageReceived += OnMqttMessageArrived;
    }

    private void OnDisable()
    {
        MQTTConnectionManager.OnTelemetryMessageReceived -= OnMqttMessageArrived;
    }

    void Start()
    {
        _animator = GetComponent<Animator>();

        // Link buttons to our new WebSocket command pipeline
        if (forceOpenButton != null) forceOpenButton.onClick.AddListener(() => DispatchWebSocketCommand(SystemMode.ForcedOpen, "FORCE_OPEN"));
        if (forceCloseButton != null) forceCloseButton.onClick.AddListener(() => DispatchWebSocketCommand(SystemMode.ForcedClose, "FORCE_CLOSE"));
        if (resumeAutoButton != null) resumeAutoButton.onClick.AddListener(() => DispatchWebSocketCommand(SystemMode.Automatic, "RESUME_AUTO"));

        UpdateModeUI();

        if (dashboardOverlay != null)
        {
            dashboardOverlay.SetActive(false);
        }

        // 1. Tell MQTT Manager to start listening to weather streams
        if (MQTTConnectionManager.Instance != null)
        {
            MQTTConnectionManager.Instance.SubscribeToTopic(windTopic);
            MQTTConnectionManager.Instance.SubscribeToTopic(rainTopic);
            MQTTConnectionManager.Instance.SubscribeToTopic(solarTopic);
        }

        // 2. Initialize and open the separate WebSocket command pipe
        InitializeWebSocket();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleDashboardVisibility();
        }
    }

    private void ToggleDashboardVisibility()
    {
        if (dashboardOverlay != null)
        {
            bool currentStatus = dashboardOverlay.activeSelf;
            dashboardOverlay.SetActive(!currentStatus);
        }
    }

    /// <summary>
    /// Processes incoming data packets from your thread-safe MQTT Connection Manager queue.
    /// </summary>
    private void OnMqttMessageArrived(string topic, string payload)
    {
        if (topic == windTopic && float.TryParse(payload, out _windSpeed))
        {
            windDisplay.text = $"Wind Speed: {_windSpeed:F1} m/s";
        }
        else if (topic == rainTopic && float.TryParse(payload, out _rainIntensity))
        {
            rainDisplay.text = $"Rain Intensity: {_rainIntensity:F0}%";
        }
        else if (topic == solarTopic && float.TryParse(payload, out _solarIrradiance))
        {
            solarDisplay.text = $"Solar Intensity: {_solarIrradiance:F0} W/m²";
        }

        EvaluateTwinCanopyState();
    }

    private void EvaluateTwinCanopyState()
    {
        bool shouldBeOpen = false;

        switch (_currentMode)
        {
            case SystemMode.ForcedOpen:
                shouldBeOpen = true;
                break;
            case SystemMode.ForcedClose:
                shouldBeOpen = false;
                break;
            case SystemMode.Automatic:
                // Hydrophobic prioritization logic engine rules
                if (_windSpeed >= windSpeedLimit) shouldBeOpen = false;
                else if (_rainIntensity >= rainIntensityLimit) shouldBeOpen = true;
                else shouldBeOpen = _solarIrradiance >= solarOpeningThreshold;
                break;
        }

        if (shouldBeOpen != _lastCanopyState)
        {
            _lastCanopyState = shouldBeOpen;
            _animator.SetBool("IsOpen", shouldBeOpen);
        }
    }

    private void UpdateModeUI()
    {
        if (systemModeDisplay == null) return;

        switch (_currentMode)
        {
            case SystemMode.Automatic:
                systemModeDisplay.text = "Mode: <color=#66FF66>AUTOMATIC</color>";
                break;
            case SystemMode.ForcedOpen:
                systemModeDisplay.text = "Mode: <color=#FFFF66>FORCED OPEN</color>";
                break;
            case SystemMode.ForcedClose:
                systemModeDisplay.text = "Mode: <color=#FF5555>FORCED CLOSED</color>";
                break;
        }
    }

    #region WebSocket Command Pipeline

    private async void InitializeWebSocket()
    {
        _webSocket = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            Uri serverUri = new Uri(webSocketURL);
            Debug.Log($"[Twin Dashboard] Opening WebSocket command connection to: {webSocketURL}");
            await _webSocket.ConnectAsync(serverUri, _cts.Token);
            Debug.Log("<color=#00FF88><b>[Twin Dashboard]: WebSocket command pipe connected successfully!</b></color>");

            // FIX: Fire and forget the background pump loop to process PING/PONG keep-alives
            _ = StartKeepAlivePump();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Twin Dashboard WebSocket Error]: Handshake failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Silently drains the incoming socket buffer so native PING frames are answered,
    /// preventing the Python server from timing out and dropping the connection.
    /// </summary>
    private async System.Threading.Tasks.Task StartKeepAlivePump()
    {
        byte[] receiveBuffer = new byte[1024];
        
        try
        {
            while (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), _cts.Token);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
                }
            }
        }
        catch (Exception)
        {
            // Silently absorb disconnection exceptions when turning off Play Mode
        }
    }

    private async void DispatchWebSocketCommand(SystemMode targetMode, string payload)
    {
        _currentMode = targetMode;
        UpdateModeUI();
        EvaluateTwinCanopyState();

        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                byte[] byteBuffer = Encoding.UTF8.GetBytes(payload);
                await _webSocket.SendAsync(new ArraySegment<byte>(byteBuffer), WebSocketMessageType.Text, true, _cts.Token);
                Debug.Log($"<color=#FFFF00><b>[WebSocket Sent]:</b></color> Dispatched override payload: {payload}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocket Transmission Failure]: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[WebSocket Warning]: Command '{payload}' dropped. Socket state is currently: {(_webSocket != null ? _webSocket.State.ToString() : "Null")}");
        }
    }

    private void OnDestroy()
    {
        if (_webSocket != null) _webSocket.Dispose();
        _cts?.Cancel();
    }

    #endregion
}