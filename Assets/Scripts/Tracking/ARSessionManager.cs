// =============================================================================
// ARSessionManager.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Monitors ARSession state, requests camera permission on iOS,
//              drives a TextMeshPro status label, and gates Vuforia tracking
//              until ARKit reports it is ready.
// Dependencies: AR Foundation 5.x, Apple ARKit XR Plugin, TextMeshPro,
//               VuforiaImageTargetManager.cs
// =============================================================================

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Place on a persistent GameObject (e.g. "ARManager") in the scene.
/// Assign the <see cref="statusLabel"/> and <see cref="imageTargetManager"/>
/// references in the Inspector.
/// </summary>
public class ARSessionManager : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    private const string StatusReady         = "AR Ready";
    private const string StatusInitializing  = "Initializing…";
    private const string StatusNotSupported  = "AR Not Supported";
    private const string StatusCheckingAvail = "Checking availability…";
    private const string StatusNone          = "Session inactive";

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Tooltip("TextMeshPro label that shows the current AR session status.")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    [Tooltip("Reference to the Vuforia tracker that should only run once AR is ready.")]
    [SerializeField] private VuforiaImageTargetManager imageTargetManager;

    [Tooltip("Seconds to wait for camera permission before timing out.")]
    [SerializeField] private float cameraPermissionTimeout = 10f;

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private ARSession m_arSession;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        m_arSession = FindObjectOfType<ARSession>();

        if (m_arSession == null)
        {
            Debug.LogError("[ARSessionManager] No ARSession found in scene.");
        }

        // Disable Vuforia until AR is confirmed ready
        if (imageTargetManager != null)
        {
            imageTargetManager.enabled = false;
        }
    }

    private void OnEnable()
    {
        ARSession.stateChanged += OnARSessionStateChanged;
    }

    private void OnDisable()
    {
        ARSession.stateChanged -= OnARSessionStateChanged;
    }

    private void Start()
    {
        SetStatusText(StatusCheckingAvail);
        StartCoroutine(InitialiseARSession());
    }

    // ------------------------------------------------------------------
    // Initialisation coroutine
    // ------------------------------------------------------------------

    private IEnumerator InitialiseARSession()
    {
#if UNITY_IOS && !UNITY_EDITOR
        yield return RequestCameraPermission();
#else
        yield return null; // Skip permission check in Editor / Android
#endif
        SetStatusText(StatusInitializing);
        LogCurrentARState();
    }

#if UNITY_IOS && !UNITY_EDITOR
    private IEnumerator RequestCameraPermission()
    {
        AsyncOperation permissionOp =
            Application.RequestUserAuthorization(UserAuthorization.WebCam);

        float elapsed = 0f;
        while (!permissionOp.isDone && elapsed < cameraPermissionTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogError("[ARSessionManager] Camera permission denied by user.");
            SetStatusText("Camera Permission Denied");
        }
        else
        {
            Debug.Log("[ARSessionManager] Camera permission granted.");
        }
    }
#endif

    // ------------------------------------------------------------------
    // ARSession event handler
    // ------------------------------------------------------------------

    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        switch (args.state)
        {
            case ARSessionState.None:
                SetStatusText(StatusNone);
                SetVuforiaEnabled(false);
                break;

            case ARSessionState.CheckingAvailability:
                SetStatusText(StatusCheckingAvail);
                SetVuforiaEnabled(false);
                break;

            case ARSessionState.NeedsInstall:
            case ARSessionState.Installing:
                SetStatusText("Installing AR support…");
                SetVuforiaEnabled(false);
                break;

            case ARSessionState.Unsupported:
                SetStatusText(StatusNotSupported);
                SetVuforiaEnabled(false);
                Debug.LogWarning("[ARSessionManager] ARKit is not supported on this device.");
                break;

            case ARSessionState.Ready:
                SetStatusText(StatusReady);
                SetVuforiaEnabled(true);
                Debug.Log("[ARSessionManager] AR session is ready.");
                break;

            case ARSessionState.SessionInitializing:
                SetStatusText(StatusInitializing);
                SetVuforiaEnabled(false);
                break;

            case ARSessionState.SessionTracking:
                SetStatusText(StatusReady);
                SetVuforiaEnabled(true);
                break;
        }

        Debug.Log($"[ARSessionManager] State changed → {args.state}");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void SetStatusText(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
    }

    private void SetVuforiaEnabled(bool enabled)
    {
        if (imageTargetManager != null)
        {
            imageTargetManager.enabled = enabled;
        }
    }

    private void LogCurrentARState()
    {
        Debug.Log($"[ARSessionManager] Current AR state on Start: {ARSession.state}");
    }
}
