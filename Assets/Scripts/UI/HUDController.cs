// =============================================================================
// HUDController.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Drives the semi-transparent top HUD bar: app title, tracking
//              status dot, detected component count, FPS counter, and an
//              Export button. Tracking status is updated via UnityEvent
//              callbacks wired in the Inspector from VuforiaImageTargetManager.
// Dependencies: VuforiaImageTargetManager.cs (UnityEvent callbacks),
//               PDFExporter.cs, ComponentDatabaseLoader.cs, TextMeshPro,
//               UnityEngine.UI
// =============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the HUD Canvas root. Wire <see cref="OnTrackingFound"/> and
/// <see cref="OnTrackingLost"/> to the matching UnityEvents on
/// <see cref="VuforiaImageTargetManager"/> in the Inspector.
/// </summary>
public class HUDController : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    private const string AppTitle          = "PCB-AR Viewer";
    private const float  FpsUpdateInterval = 0.5f;

    private static readonly Color ColorTracked = new Color(0.15f, 0.85f, 0.15f); // green dot
    private static readonly Color ColorLost    = new Color(0.9f,  0.15f, 0.15f); // red dot

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("Labels")]
    [Tooltip("TextMeshPro label showing the app title.")]
    [SerializeField] private TextMeshProUGUI txtAppTitle;

    [Tooltip("TextMeshPro label showing number of detected components.")]
    [SerializeField] private TextMeshProUGUI txtComponentCount;

    [Tooltip("TextMeshPro label showing the current FPS.")]
    [SerializeField] private TextMeshProUGUI txtFps;

    [Header("Status indicator")]
    [Tooltip("Image used as the tracking status dot (green = tracked, red = lost).")]
    [SerializeField] private Image statusDot;

    [Header("Buttons")]
    [Tooltip("Button that triggers PDF/TXT report export.")]
    [SerializeField] private Button exportButton;

    [Header("References")]
    [Tooltip("PDFExporter to call when the export button is pressed.")]
    [SerializeField] private PDFExporter pdfExporter;

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private float m_fpsAccumulator;
    private int   m_fpsFrameCount;
    private float m_fpsTimer;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Start()
    {
        if (txtAppTitle != null)
        {
            txtAppTitle.text = AppTitle;
        }

        if (exportButton != null)
        {
            exportButton.onClick.AddListener(OnExportClicked);
        }

        // Initial state: tracking lost
        SetTrackingStatus(false);
        UpdateComponentCount();
    }

    private void OnDestroy()
    {
        if (exportButton != null)
        {
            exportButton.onClick.RemoveListener(OnExportClicked);
        }
    }

    private void Update()
    {
        UpdateFpsCounter();
    }

    // ------------------------------------------------------------------
    // Public callbacks (wired to VuforiaImageTargetManager UnityEvents)
    // ------------------------------------------------------------------

    /// <summary>
    /// Call this from VuforiaImageTargetManager.OnTargetFound (Inspector).
    /// </summary>
    public void OnTrackingFound()
    {
        SetTrackingStatus(true);
        UpdateComponentCount();
    }

    /// <summary>
    /// Call this from VuforiaImageTargetManager.OnTargetLost (Inspector).
    /// </summary>
    public void OnTrackingLost()
    {
        SetTrackingStatus(false);
    }

    // ------------------------------------------------------------------
    // UI updates
    // ------------------------------------------------------------------

    private void SetTrackingStatus(bool tracked)
    {
        if (statusDot != null)
        {
            statusDot.color = tracked ? ColorTracked : ColorLost;
        }
    }

    private void UpdateComponentCount()
    {
        if (txtComponentCount == null) return;

        int count = ComponentDatabaseLoader.IsLoaded
            ? ComponentDatabaseLoader.AllComponents.Count
            : 0;

        txtComponentCount.text = $"Components: {count}";
    }

    private void UpdateFpsCounter()
    {
        m_fpsAccumulator += Time.unscaledDeltaTime;
        m_fpsFrameCount++;
        m_fpsTimer += Time.unscaledDeltaTime;

        if (m_fpsTimer >= FpsUpdateInterval)
        {
            float fps = m_fpsFrameCount / m_fpsAccumulator;

            if (txtFps != null)
            {
                txtFps.text = $"{fps:F0} FPS";
            }

            m_fpsAccumulator = 0f;
            m_fpsFrameCount  = 0;
            m_fpsTimer       = 0f;
        }
    }

    // ------------------------------------------------------------------
    // Button handlers
    // ------------------------------------------------------------------

    private void OnExportClicked()
    {
        if (pdfExporter == null)
        {
            Debug.LogError("[HUDController] PDFExporter reference is not assigned.");
            return;
        }

        pdfExporter.ExportReport();
    }
}
