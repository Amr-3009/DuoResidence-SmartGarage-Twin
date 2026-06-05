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
    public string serverIp = "localhost"; 
    public int serverPort = 8080;

    [Header("UI Component Links")]
    public RawImage streamDisplay;       
    public RectTransform displayRect;    
    public RectTransform viewportParent; 
    public TextMeshProUGUI camLabelText;      
    public Button sessionButton;         
    public TextMeshProUGUI sessionButtonText;  

    [Header("Camera Control Buttons (Auto-Wired)")]
    public Button btnOverview;
    public Button btnEntrance;
    public Button btnExit;
    public Button btnResetView; // 🚀 NEW: Reference slot for the reset view action

    [Header("🎚️ UNIFIED OVERVIEW CONTROL DECK")]
    public Slider sliderOverview_Zoom;
    public Slider sliderOverview_X;
    public Slider sliderOverview_Y;

    // Active operating coordinates for overview pan and zoom
    private float overviewZoom = 1.0f;
    private float overviewX = 0f;
    private float overviewY = 0f;

    // Hardcoded baseline system defaults
    private float defZoom = 1.0f;
    private float defX = 0f;
    private float defY = 0f;

    private bool _isSessionActive = false;
    private string _currentStreamPath = "/lanes";
    private string _activeMode = "OVERVIEW";

    private Thread _networkThread;
    private HttpWebRequest _activeRequest; 
    private Texture2D _videoTexture;
    private byte[] _latestFrameBytes;
    private readonly object _lockObject = new object();
    private bool _hasNewFrameReady = false;

    private void Start()
    {
        _videoTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (streamDisplay != null) streamDisplay.texture = _videoTexture;

        if (viewportParent == null && displayRect != null)
        {
            viewportParent = displayRect.parent as RectTransform;
        }

        ResetVariablesToDefaultSystemValues();
        ResetViewportTransform();
        UpdateUIElements();

        if (sessionButton != null) sessionButton.onClick.AddListener(ToggleMasterSession);
        if (btnOverview != null) btnOverview.onClick.AddListener(() => SelectCameraView("OVERVIEW"));
        if (btnEntrance != null) btnEntrance.onClick.AddListener(() => SelectCameraView("ENTRANCE"));
        if (btnExit != null) btnExit.onClick.AddListener(() => SelectCameraView("EXIT"));
        
        // 🚀 NEW: Wire up the click event for the reset function programmatically
        if (btnResetView != null) btnResetView.onClick.AddListener(ResetSlidersAndTransform);

        WireSliderListeners();
        
        UpdateSliderUiHandlesToMatchVariables();
        UpdateSliderInteractivityMatrix();
    }

    private void WireSliderListeners()
    {
        if (sliderOverview_Zoom != null) sliderOverview_Zoom.onValueChanged.AddListener((v) => {
            overviewZoom = v;
            RecalculateDynamicPanBounds();
        });

        if (sliderOverview_X != null) sliderOverview_X.onValueChanged.AddListener((v) => overviewX = v);
        if (sliderOverview_Y != null) sliderOverview_Y.onValueChanged.AddListener((v) => overviewY = v);
    }

    private void RecalculateDynamicPanBounds()
    {
        if (displayRect == null || viewportParent == null || sliderOverview_X == null || sliderOverview_Y == null) return;

        float viewWidth = viewportParent.rect.width;
        float viewHeight = viewportParent.rect.height;

        float maxDeltaX = (viewWidth * (overviewZoom - 1f)) / 2f;
        float maxDeltaY = (viewHeight * (overviewZoom - 1f)) / 2f;

        sliderOverview_X.minValue = -maxDeltaX;
        sliderOverview_X.maxValue = maxDeltaX;

        sliderOverview_Y.minValue = -maxDeltaY;
        sliderOverview_Y.maxValue = maxDeltaY;
    }

    /// <summary>
    /// 🚀 NEW: Resets all active position variables and snaps slider elements back to zero bounds cleanly
    /// </summary>
    public void ResetSlidersAndTransform()
    {
        ResetVariablesToDefaultSystemValues();
        UpdateSliderUiHandlesToMatchVariables();
        ApplyGeometricViewportView();
        Debug.Log("[CCTV Client] Overview pan/zoom matrix reset to factory center-baseline.");
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
            
            ResetVariablesToDefaultSystemValues();
            UpdateSliderUiHandlesToMatchVariables();
        }

        UpdateUIElements();
        UpdateSliderInteractivityMatrix();
    }

    private void ResetVariablesToDefaultSystemValues()
    {
        overviewZoom = defZoom; 
        overviewX = defX; 
        overviewY = defY;
    }

    private void UpdateSliderUiHandlesToMatchVariables()
    {
        if (sliderOverview_Zoom != null) sliderOverview_Zoom.value = overviewZoom;
        RecalculateDynamicPanBounds();
        if (sliderOverview_X != null) sliderOverview_X.value = overviewX;
        if (sliderOverview_Y != null) sliderOverview_Y.value = overviewY;
    }

    private void UpdateSliderInteractivityMatrix()
    {
        bool allowOverviewControls = _isSessionActive && _activeMode == "OVERVIEW";

        if (sliderOverview_Zoom != null) sliderOverview_Zoom.interactable = allowOverviewControls;
        if (sliderOverview_X != null) sliderOverview_X.interactable = allowOverviewControls;
        if (sliderOverview_Y != null) sliderOverview_Y.interactable = allowOverviewControls;
        
        // 🚀 NEW: The reset button behaves logically—it locks out unless you are on the interactive lanes view
        if (btnResetView != null) btnResetView.interactable = allowOverviewControls;
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
            
            lock (_lockObject)
            {
                if (_activeRequest != null)
                {
                    try { _activeRequest.Abort(); } catch {}
                }
            }
        }

        ApplyGeometricViewportView();
        UpdateUIElements();
        UpdateSliderInteractivityMatrix(); 
    }

    private void ApplyGeometricViewportView()
    {
        if (displayRect == null) return;

        switch (_activeMode)
        {
            case "OVERVIEW":
                displayRect.localScale = new Vector3(overviewZoom, overviewZoom, 1f);
                displayRect.anchoredPosition = new Vector2(overviewX, overviewY);
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
            ApplyGeometricViewportView(); 
        }
    }

    private void StartNetworkStream()
    {
        StopNetworkStream(); 
        _isSessionActive = true;
        _networkThread = new Thread(MjpegStreamListener);
        _networkThread.IsBackground = true;
        _networkThread.Start();
    }

    private void StopNetworkStream()
    {
        _isSessionActive = false;
        lock (_lockObject)
        {
            if (_activeRequest != null)
            {
                try { _activeRequest.Abort(); } catch {}
                _activeRequest = null;
            }
        }
        _hasNewFrameReady = false;
    }

    private void MjpegStreamListener()
    {
        while (_isSessionActive)
        {
            string targetUrl = $"http://{serverIp}:{serverPort}{_currentStreamPath}";
            string cachedPath = _currentStreamPath;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(targetUrl);
                request.Timeout = 5000;
                
                lock (_lockObject) { _activeRequest = request; }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    while (_isSessionActive && _currentStreamPath == cachedPath)
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
            catch (Exception) 
            { 
                Thread.Sleep(250); 
            }
            finally
            {
                lock (_lockObject) { _activeRequest = null; }
            }
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
        StopNetworkStream();
    }
}