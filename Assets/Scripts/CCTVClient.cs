using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DuoResidence — CCTV Client (UGUI)
///
/// Streams an MJPEG feed from a backend server on a background thread and
/// displays it on a RawImage, with buttons to switch camera view (Overview /
/// Entrance / Exit), start/stop the session, and zoom/pan-control sliders
/// (with auto-recalculated pan bounds) for the Overview camera. Its UGUI canvas
/// is normally hidden behind the UI Toolkit dashboards popup
/// (GarageDashboardsController), which mirrors this UI and forwards interactions
/// through the public methods/sliders below.
/// </summary>
public class CCTVClient : MonoBehaviour
{
    [Header("Network Configuration")]
    public string serverIp = "localhost"; 
    public int serverPort = 8080;
    public bool useHttps = false; // 🚀 NEW: Check this TRUE when using ngrok URLs!

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
    public Button btnResetView; 

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

    /// <summary>
    /// Creates the placeholder video texture, resolves the viewport parent,
    /// resets pan/zoom to defaults, refreshes the UI, and wires up all buttons
    /// and sliders.
    /// </summary>
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
        
        if (btnResetView != null) btnResetView.onClick.AddListener(ResetSlidersAndTransform);

        WireSliderListeners();
        
        UpdateSliderUiHandlesToMatchVariables();
        UpdateSliderInteractivityMatrix();
    }

    // Hooks up the zoom/pan-X/pan-Y sliders so changing them updates the
    // corresponding overview transform variables (zoom changes also recompute
    // the pan bounds).
    private void WireSliderListeners()
    {
        if (sliderOverview_Zoom != null) sliderOverview_Zoom.onValueChanged.AddListener((v) => {
            overviewZoom = v;
            RecalculateDynamicPanBounds();
        });

        if (sliderOverview_X != null) sliderOverview_X.onValueChanged.AddListener((v) => overviewX = v);
        if (sliderOverview_Y != null) sliderOverview_Y.onValueChanged.AddListener((v) => overviewY = v);
    }

    // Recomputes the min/max range of the pan-X/pan-Y sliders so the zoomed-in
    // view can never be panned past the edges of the viewport.
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

    // Resets pan/zoom to their default values, updates the slider handles to
    // match, and re-applies the viewport transform.
    public void ResetSlidersAndTransform()
    {
        ResetVariablesToDefaultSystemValues();
        UpdateSliderUiHandlesToMatchVariables();
        ApplyGeometricViewportView();
        Debug.Log("[CCTV Client] Overview pan/zoom matrix reset to factory center-baseline.");
    }

    /// <summary>
    /// Starts or stops the CCTV session: starting begins the network stream;
    /// stopping halts it, clears the display to black and resets pan/zoom.
    /// Either way, refreshes the status/label UI and slider interactivity.
    /// </summary>
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

    // Restores the overview zoom/pan-X/pan-Y working variables to their
    // hardcoded defaults.
    private void ResetVariablesToDefaultSystemValues()
    {
        overviewZoom = defZoom; 
        overviewX = defX; 
        overviewY = defY;
    }

    // Pushes the current zoom/pan-X/pan-Y variables onto their slider handles
    // (and recalculates pan bounds for the new zoom level).
    private void UpdateSliderUiHandlesToMatchVariables()
    {
        if (sliderOverview_Zoom != null) sliderOverview_Zoom.value = overviewZoom;
        RecalculateDynamicPanBounds();
        if (sliderOverview_X != null) sliderOverview_X.value = overviewX;
        if (sliderOverview_Y != null) sliderOverview_Y.value = overviewY;
    }

    // Enables the zoom/pan sliders and reset-view button only while a session
    // is active and the active camera is the Overview camera.
    private void UpdateSliderInteractivityMatrix()
    {
        bool allowOverviewControls = _isSessionActive && _activeMode == "OVERVIEW";

        if (sliderOverview_Zoom != null) sliderOverview_Zoom.interactable = allowOverviewControls;
        if (sliderOverview_X != null) sliderOverview_X.interactable = allowOverviewControls;
        if (sliderOverview_Y != null) sliderOverview_Y.interactable = allowOverviewControls;
        
        if (btnResetView != null) btnResetView.interactable = allowOverviewControls;
    }

    /// <summary>
    /// Switches the active camera view (OVERVIEW / ENTRANCE / EXIT). If the
    /// underlying stream path changes, aborts any in-flight request so the
    /// background thread reconnects to the new path, then refreshes the
    /// viewport transform, status UI and slider interactivity.
    /// </summary>
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

    // Applies the current zoom/pan as a scale + anchored-position on the
    // display RectTransform for Overview mode, or resets it for other modes.
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

    /// <summary>
    /// On the main thread: loads the latest JPEG frame (if any) produced by
    /// the background stream thread into the video texture, and continuously
    /// re-applies the viewport transform while a session is active.
    /// </summary>
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

    // Stops any existing stream, then starts a new background thread running
    // MjpegStreamListener().
    private void StartNetworkStream()
    {
        StopNetworkStream(); 
        _isSessionActive = true;
        _networkThread = new Thread(MjpegStreamListener);
        _networkThread.IsBackground = true;
        _networkThread.Start();
    }

    // Marks the session inactive and aborts any in-flight HTTP request so the
    // background thread's loop exits.
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

    /// <summary>
    /// Background-thread loop: connects to the MJPEG endpoint for the current
    /// camera mode, parses the multipart stream's Content-Length headers and
    /// reads each JPEG frame into <see cref="_latestFrameBytes"/> for Update()
    /// to display. Reconnects on error/timeout, and exits early if the
    /// requested stream path changes mid-read.
    /// </summary>
    private void MjpegStreamListener()
    {
        while (_isSessionActive)
        {
            // 🚀 STRATEGIC REVISION: Handles secure protocol scaling automatically
            string protocol = useHttps ? "https" : "http";
            string portSuffix = (useHttps && serverPort == 443) || (!useHttps && serverPort == 80) ? "" : $":{serverPort}";
            string targetUrl = $"{protocol}://{serverIp}{portSuffix}{_currentStreamPath}";
            string cachedPath = _currentStreamPath;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(targetUrl);
                request.Timeout = 5000;
                
                // 🚀 CRITICAL CLOUD TUNNEL FIX: Tells ngrok to instantly pass the video bytes 
                // and skip serving the raw HTML browser warning page!
                request.Headers.Add("ngrok-skip-browser-warning", "true");
                
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

    // Reads a single CRLF-terminated ASCII line from the MJPEG stream
    // (used for parsing multipart headers); returns null on read failure.
    private string ReadAsciiLine(BinaryReader reader)
    {
        string line = ""; char c;
        try {
            while ((c = reader.ReadChar()) != '\r') line += c;
            reader.ReadChar(); return line;
        } catch { return null; }
    }

    // Resets the display RectTransform to scale 1 / centered position
    // (the non-Overview, unzoomed view).
    private void ResetViewportTransform()
    {
        if (displayRect == null) return;
        displayRect.localScale = Vector3.one;
        displayRect.anchoredPosition = Vector2.zero;
    }

    // Updates the camera label, session button text and session button colour
    // to reflect whether a session is active and which camera mode is selected.
    private void UpdateUIElements()
    {
        if (camLabelText != null) camLabelText.text = _isSessionActive ? $"LIVE: {_activeMode}" : "SYSTEM OFFLINE";
        if (sessionButtonText != null) sessionButtonText.text = _isSessionActive ? "TERMINATE SESSION" : "START WATCHING";
        if (sessionButton != null) sessionButton.GetComponent<Image>().color = _isSessionActive ? Color.red : Color.green;
    }

    // Replaces the video texture with a blank black 2x2 texture, shown when a
    // session ends.
    private void ClearDisplayToBlack()
    {
        _videoTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        Color[] blackPixels = new Color[4] { Color.black, Color.black, Color.black, Color.black };
        _videoTexture.SetPixels(blackPixels); _videoTexture.Apply();
        if (streamDisplay != null) streamDisplay.texture = _videoTexture;
    }

    // Ensures the background streaming thread is stopped when this object is destroyed.
    private void OnDestroy()
    {
        StopNetworkStream();
    }
}