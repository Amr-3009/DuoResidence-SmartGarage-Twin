using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// DuoResidence — Garage Scene Live Preview Loader
///
/// Additively loads the Smart Garage scene in the background, redirects
/// its "PreviewCamera" to render into a RenderTexture (instead of the screen),
/// and feeds that texture to the MainMenu viewport.
///
/// Also strips out anything in the preview scene that would steal input
/// from the MainMenu UI (duplicate EventSystem, screen-space Canvas
/// raycasters, the Garage HUD, etc.) so the sidebar stays clickable.
///
/// Attach to any persistent GameObject in the MainMenu scene (e.g. MainMenu_UI).
/// </summary>
public class GarageScenePreviewLoader : MonoBehaviour
{
    [Header("Scene to preview")]
    [Tooltip("Name must match Build Settings exactly.")]
    public string previewSceneName = "GarageTwin_Phase1";

    [Header("Render Target")]
    [Tooltip("Same RenderTexture assigned to MainMenuController's cameraPreviewTexture.")]
    public RenderTexture previewTexture;

    [Header("Preview Camera")]
    [Tooltip("Exact GameObject name of the camera to use for the live preview.")]
    public string previewCameraName = "PreviewCamera";

    [Header("Other Cameras")]
    [Tooltip("Disable any other cameras in the preview scene (e.g. Main Camera) so only PreviewCamera renders.")]
    public bool disableOtherCameras = true;

    [Header("Preview Camera Settings")]
    [Tooltip("Disable the preview scene's HUD/UIDocument so it doesn't render full-screen.")]
    public bool disablePreviewHUD = true;

    [Tooltip("Disable the preview scene's AudioListener to avoid 'multiple listeners' warnings.")]
    public bool disablePreviewAudioListener = true;

    [Tooltip("Disable the preview scene's EventSystem so it doesn't conflict with MainMenu's input.")]
    public bool disablePreviewEventSystem = true;

    [Tooltip("Disable GraphicRaycaster on Canvases in the preview scene so they can't block clicks on MainMenu UI.")]
    public bool disablePreviewCanvasRaycasters = true;

    private Scene _previewScene;
    private bool  _loaded;

    private void Start()
    {
        StartCoroutine(LoadPreviewScene());
    }

    private IEnumerator LoadPreviewScene()
    {
        if (string.IsNullOrEmpty(previewSceneName) || previewTexture == null)
        {
            Debug.LogWarning("[DuoResidence] GarageScenePreviewLoader: " +
                             "previewSceneName or previewTexture not set.");
            yield break;
        }

        var op = SceneManager.LoadSceneAsync(previewSceneName, LoadSceneMode.Additive);
        yield return op;

        _previewScene = SceneManager.GetSceneByName(previewSceneName);
        _loaded = true;

        SetupPreviewCamera();

        if (disablePreviewHUD)             DisableHUD();
        if (disablePreviewAudioListener)   DisableAudioListener();
        if (disablePreviewEventSystem)     DisableEventSystems();
        if (disablePreviewCanvasRaycasters) DisableCanvasRaycasters();
    }

    private void SetupPreviewCamera()
    {
        Camera previewCam = null;
        var allCams = new List<Camera>();

        foreach (var go in _previewScene.GetRootGameObjects())
            allCams.AddRange(go.GetComponentsInChildren<Camera>(true));

        foreach (var cam in allCams)
        {
            if (cam.gameObject.name == previewCameraName)
                previewCam = cam;
        }

        if (previewCam == null)
        {
            Debug.LogWarning($"[DuoResidence] No camera named '{previewCameraName}' found in '{previewSceneName}'.");
            return;
        }

        previewCam.gameObject.SetActive(true);
        previewCam.enabled = true;
        previewCam.targetTexture = previewTexture;
        previewCam.depth = -10; // avoid fighting with the UI Toolkit overlay camera

        if (disableOtherCameras)
        {
            foreach (var cam in allCams)
            {
                if (cam != previewCam)
                {
                    cam.targetTexture = null;
                    cam.enabled = false;
                }
            }
        }
    }

    private void DisableHUD()
    {
        foreach (var go in _previewScene.GetRootGameObjects())
        {
            var doc = go.GetComponentInChildren<UnityEngine.UIElements.UIDocument>(true);
            if (doc != null)
                doc.gameObject.SetActive(false);
        }
    }

    private void DisableAudioListener()
    {
        foreach (var go in _previewScene.GetRootGameObjects())
        {
            var listeners = go.GetComponentsInChildren<AudioListener>(true);
            foreach (var l in listeners)
                l.enabled = false;
        }
    }

    /// <summary>
    /// Disables any EventSystem GameObjects in the preview scene.
    /// Two active EventSystems (one per scene) conflict and can break
    /// input routing for the MainMenu UI Toolkit panel.
    /// </summary>
    private void DisableEventSystems()
    {
        foreach (var go in _previewScene.GetRootGameObjects())
        {
            var systems = go.GetComponentsInChildren<EventSystem>(true);
            foreach (var es in systems)
                es.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Disables GraphicRaycaster on every Canvas in the preview scene.
    /// The garage scene has several screen-space dashboard canvases
    /// (wall/HVAC/CCTV displays) that, once loaded additively, can sit
    /// on top of the MainMenu UI and swallow pointer clicks.
    /// </summary>
    private void DisableCanvasRaycasters()
    {
        foreach (var go in _previewScene.GetRootGameObjects())
        {
            var raycasters = go.GetComponentsInChildren<GraphicRaycaster>(true);
            foreach (var rc in raycasters)
                rc.enabled = false;

            // Belt-and-braces: if any of these canvases are Screen Space - Overlay,
            // switch them to World Space so they can never cover the MainMenu UI.
            var canvases = go.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay ||
                    c.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    c.renderMode = RenderMode.WorldSpace;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (_loaded && _previewScene.IsValid())
            SceneManager.UnloadSceneAsync(_previewScene);
    }
}
