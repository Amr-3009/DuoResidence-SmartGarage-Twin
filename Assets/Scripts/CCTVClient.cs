using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CCTVClient : MonoBehaviour
{
    [Header("Network Configuration")]
    public string serverIp = "127.0.0.1"; 
    public int serverPort = 8080;

    [Header("UI Component Links")]
    public RawImage streamDisplay;       
    public RectTransform displayRect;    
    public TextMeshProUGUI camLabelText;      
    public Button sessionButton;         
    public TextMeshProUGUI sessionButtonText;  

    [Header("Camera Control Buttons (Auto-Wired)")]
    public Button btnOverview;
    public Button btnLaneA;
    public Button btnLaneB;
    public Button btnLaneC;
    public Button btnEntrance;
    public Button btnExit;

    [Header("🎚️ CANVAS CALIBRATION DECK: LANE A")]
    public Slider sliderA_Zoom;
    public Slider sliderA_X;
    public Slider sliderA_Y;

    [Header("🎚️ CANVAS CALIBRATION DECK: LANE B")]
    public Slider sliderB_Zoom;
    public Slider sliderB_X;
    public Slider sliderB_Y;

    [Header("🎚️ CANVAS CALIBRATION DECK: LANE C")]
    public Slider sliderC_Zoom;
    public Slider sliderC_X;
    public Slider sliderC_Y;

    // Active operating coordinates
    private float laneA_Zoom, laneA_X, laneA_Y;
    private float laneB_Zoom, laneB_X, laneB_Y;
    private float laneC_Zoom, laneC_X, laneC_Y;

    // Hardcoded baseline default rulesets (Wiped and restored on session cycles)
    private float defA_Zoom = 2.5f, defA_X = 0f, defA_Y = 0f;
    private float defB_Zoom = 2.5f, defB_X = 0f, defB_Y = 0f;
    private float defC_Zoom = 2.5f, defC_X = 0f, defC_Y = 0f;

    private bool _isSessionActive = false;
    private string _currentStreamPath = "/lanes";
    private string _activeMode = "OVERVIEW";

    private Thread _networkThread;
    private Texture2D _videoTexture;
    private byte[] _latestFrameBytes;
    private readonly object _lockObject = new object();
    private bool _hasNewFrameReady = false;

    private void Start()
    {
        _videoTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (streamDisplay != null) streamDisplay.texture = _videoTexture;

        // Force variables back to system defaults on startup instance
        ResetVariablesToDefaultSystemValues();
        ResetViewportTransform();
        UpdateUIElements();

        // Bind interactive UI button click listeners programmatically
        if (sessionButton != null) sessionButton.onClick.AddListener(ToggleMasterSession);
        if (btnOverview != null) btnOverview.onClick.AddListener(() => SelectCameraView("OVERVIEW"));
        if (btnLaneA != null) btnLaneA.onClick.AddListener(() => SelectCameraView("LANE_A"));
        if (btnLaneB != null) btnLaneB.onClick.AddListener(() => SelectCameraView("LANE_B"));
        if (btnLaneC != null) btnLaneC.onClick.AddListener(() => SelectCameraView("LANE_C"));
        if (btnEntrance != null) btnEntrance.onClick.AddListener(() => SelectCameraView("ENTRANCE"));
        if (btnExit != null) btnExit.onClick.AddListener(() => SelectCameraView("EXIT"));

        // Bind UI Slider value capture actions programmatically
        WireSliderListeners();
        
        // Push starting defaults onto the slider handles visually
        UpdateSliderUiHandlesToMatchVariables();
        UpdateSliderInteractivityMatrix();
    }

    private void WireSliderListeners()
    {
        if (sliderA_Zoom != null) sliderA_Zoom.onValueChanged.AddListener((v) => laneA_Zoom = v);
        if (sliderA_X != null) sliderA_X.onValueChanged.AddListener((v) => laneA_X = v);
        if (sliderA_Y != null) sliderA_Y.onValueChanged.AddListener((v) => laneA_Y = v);

        if (sliderB_Zoom != null) sliderB_Zoom.onValueChanged.AddListener((v) => laneB_Zoom = v);
        if (sliderB_X != null) sliderB_X.onValueChanged.AddListener((v) => laneB_X = v);
        if (sliderB_Y != null) sliderB_Y.onValueChanged.AddListener((v) => laneB_Y = v);

        if (sliderC_Zoom != null) sliderC_Zoom.onValueChanged.AddListener((v) => laneC_Zoom = v);
        if (sliderC_X != null) sliderC_X.onValueChanged.AddListener((v) => laneC_X = v);
        if (sliderC_Y != null) sliderC_Y.onValueChanged.AddListener((v) => laneC_Y = v);
    }

    public void ToggleMasterSession()
    {
        if (!_isSessionActive)
        {
            _isSessionActive = true;
            StartNetworkStream();
        }
        else
        {
            _isSessionActive = false;
            StopNetworkStream();
            ClearDisplayToBlack();
            
            // 🔄 Wipe all user slider modifications and restore defaults when session is terminated!
            ResetVariablesToDefaultSystemValues();
            UpdateSliderUiHandlesToMatchVariables();
        }

        UpdateUIElements();
        UpdateSliderInteractivityMatrix();
    }

    private void ResetVariablesToDefaultSystemValues()
    {
        laneA_Zoom = defA_Zoom; laneA_X = defA_X; laneA_Y = defA_Y;
        laneB_Zoom = defB_Zoom; laneB_X = defB_X; laneB_Y = defB_Y;
        laneC_Zoom = defC_Zoom; laneC_X = defC_X; laneC_Y = defC_Y;
    }

    private void UpdateSliderUiHandlesToMatchVariables()
    {
        if (sliderA_Zoom != null) sliderA_Zoom.value = laneA_Zoom;
        if (sliderA_X != null) sliderA_X.value = laneA_X;
        if (sliderA_Y != null) sliderA_Y.value = laneA_Y;

        if (sliderB_Zoom != null) sliderB_Zoom.value = laneB_Zoom;
        if (sliderB_X != null) sliderB_X.value = laneB_X;
        if (sliderB_Y != null) sliderB_Y.value = laneB_Y;

        if (sliderC_Zoom != null) sliderC_Zoom.value = laneC_Zoom;
        if (sliderC_X != null) sliderC_X.value = laneC_X;
        if (sliderC_Y != null) sliderC_Y.value = laneC_Y;
    }

    private void UpdateSliderInteractivityMatrix()
    {
        // Controls are completely dead unless the security session loop is turned on
        bool laneAActive = _isSessionActive && _activeMode == "LANE_A";
        bool laneBActive = _isSessionActive && _activeMode == "LANE_B";
        bool laneCActive = _isSessionActive && _activeMode == "LANE_C";

        if (sliderA_Zoom != null) sliderA_Zoom.interactable = laneAActive;
        if (sliderA_X != null) sliderA_X.interactable = laneAActive;
        if (sliderA_Y != null) sliderA_Y.interactable = laneAActive;

        if (sliderB_Zoom != null) sliderB_Zoom.interactable = laneBActive;
        if (sliderB_X != null) sliderB_X.interactable = laneBActive;
        if (sliderB_Y != null) sliderB_Y.interactable = laneBActive;

        if (sliderC_Zoom != null) sliderC_Zoom.interactable = laneCActive;
        if (sliderC_X != null) sliderC_X.interactable = laneCActive;
        if (sliderC_Y != null) sliderC_Y.interactable = laneCActive;
    }

    public void SelectCameraView(string mode)
    {
        _activeMode = mode.ToUpper();
        string targetPath = "/lanes";

        if (_activeMode == "ENTRANCE") targetPath = "/entrance";
        else if (_activeMode == "EXIT") targetPath = "/exit";

        if (targetPath != _currentStreamPath)
        {
            _currentStreamPath = targetPath;
            if (_isSessionActive) StartNetworkStream();
        }

        ApplyGeometricViewportView();
        UpdateUIElements();
        UpdateSliderInteractivityMatrix(); // 🔒 Dynamically lock/unlock matching rows instantly
    }

    private void ApplyGeometricViewportView()
    {
        if (displayRect == null) return;

        switch (_activeMode)
        {
            case "LANE_A":
                displayRect.localScale = new Vector3(laneA_Zoom, laneA_Zoom, 1f);
                displayRect.anchoredPosition = new Vector2(laneA_X, laneA_Y);
                break;
            case "LANE_B":
                displayRect.localScale = new Vector3(laneB_Zoom, laneB_Zoom, 1f);
                displayRect.anchoredPosition = new Vector2(laneB_X, laneB_Y);
                break;
            case "LANE_C":
                displayRect.localScale = new Vector3(laneC_Zoom, laneC_Zoom, 1f);
                displayRect.anchoredPosition = new Vector2(laneC_X, laneC_Y);
                break;
            default:
                ResetViewportTransform();
                break;
        }
    }

    private void Update()
    {
        if (_hasNewFrameReady)
        {
            byte[] bytesToLoad = null;
            lock (_lockObject) { bytesToLoad = _latestFrameBytes; _hasNewFrameReady = false; }
            if (bytesToLoad != null && _isSessionActive) _videoTexture.LoadImage(bytesToLoad);
        }

        if (_isSessionActive)
        {
            ApplyGeometricViewportView(); // Keep rendering tracking modifications fluidly
        }
    }

    private void StartNetworkStream()
    {
        StopNetworkStream(); 
        _networkThread = new Thread(MjpegStreamListener);
        _networkThread.IsBackground = true;
        _networkThread.Start();
    }

    private void StopNetworkStream()
    {
        if (_networkThread != null && _networkThread.IsAlive) _networkThread.Abort();
        _hasNewFrameReady = false;
    }

    private void MjpegStreamListener()
    {
        string targetUrl = $"http://{serverIp}:{serverPort}{_currentStreamPath}";
        while (_isSessionActive)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(targetUrl);
                request.Timeout = 5000;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    while (_isSessionActive)
                    {
                        string line = ""; int contentLength = 0;
                        while ((line = ReadAsciiLine(reader)) != null)
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            {
                                contentLength = int.Parse(line.Split(':')[1].Trim());
                                break;
                            }
                        }
                        ReadAsciiLine(reader);
                        if (contentLength > 0)
                        {
                            byte[] frameData = reader.ReadBytes(contentLength);
                            lock (_lockObject) { _latestFrameBytes = frameData; _hasNewFrameReady = true; }
                        }
                    }
                }
            }
            catch (Exception) { Thread.Sleep(1000); }
        }
    }

    private string ReadAsciiLine(BinaryReader reader)
    {
        string line = ""; char c;
        try {
            while ((c = reader.ReadChar()) != '\r') line += c;
            reader.ReadChar(); return line;
        } catch { return null; }
    }

    private void ResetViewportTransform()
    {
        if (displayRect == null) return;
        displayRect.localScale = Vector3.one;
        displayRect.anchoredPosition = Vector2.zero;
    }

    private void UpdateUIElements()
    {
        if (camLabelText != null) camLabelText.text = _isSessionActive ? $"LIVE: {_activeMode}" : "SYSTEM OFFLINE";
        if (sessionButtonText != null) sessionButtonText.text = _isSessionActive ? "TERMINATE SESSION" : "START WATCHING";
        if (sessionButton != null) sessionButton.GetComponent<Image>().color = _isSessionActive ? Color.red : Color.green;
    }

    private void ClearDisplayToBlack()
    {
        _videoTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        Color[] blackPixels = new Color[4] { Color.black, Color.black, Color.black, Color.black };
        _videoTexture.SetPixels(blackPixels); _videoTexture.Apply();
        if (streamDisplay != null) streamDisplay.texture = _videoTexture;
    }

    private void OnDestroy()
    {
        _isSessionActive = false;
        StopNetworkStream();
    }
}