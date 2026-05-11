// =============================================================================
// VuforiaImageTargetManager.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Attaches to a Vuforia ImageTarget GameObject and handles
//              tracking-found / tracking-lost events. Drives the
//              ComponentOverlayRenderer and exposes UnityEvents for
//              inspector wiring (e.g. HUDController status icon).
// Dependencies: Vuforia Engine (Community), ComponentOverlayRenderer.cs
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Vuforia;

/// <summary>
/// Attach directly to the Vuforia ImageTarget GameObject (the one with the
/// <c>ImageTargetBehaviour</c> component). Wire <see cref="OnTargetFound"/>
/// and <see cref="OnTargetLost"/> in the Inspector as needed.
/// </summary>
public class VuforiaImageTargetManager : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    private const float FadeDuration = 0.5f;

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Tooltip("Overlay renderer to show/hide when tracking changes.")]
    [SerializeField] private ComponentOverlayRenderer overlayRenderer;

    [Tooltip("Fired when the image target is found/tracked.")]
    public UnityEvent OnTargetFound;

    [Tooltip("Fired when the image target tracking is lost.")]
    public UnityEvent OnTargetLost;

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private ImageTargetBehaviour m_imageTarget;
    private bool m_isTracked;
    private Coroutine m_fadeCoroutine;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Start()
    {
        m_imageTarget = GetComponent<ImageTargetBehaviour>();

        if (m_imageTarget == null)
        {
            Debug.LogError(
                "[VuforiaImageTargetManager] No ImageTargetBehaviour found on this GameObject.");
            return;
        }

        m_imageTarget.OnTargetStatusChanged += HandleTargetStatusChanged;

        // Start with overlays hidden
        if (overlayRenderer != null)
        {
            overlayRenderer.HideAll();
        }
    }

    private void OnDestroy()
    {
        if (m_imageTarget != null)
        {
            m_imageTarget.OnTargetStatusChanged -= HandleTargetStatusChanged;
        }
    }

    // ------------------------------------------------------------------
    // Vuforia event handler
    // ------------------------------------------------------------------

    private void HandleTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool nowTracked = status.Status == Status.TRACKED ||
                          status.Status == Status.EXTENDED_TRACKED;

        if (nowTracked && !m_isTracked)
        {
            m_isTracked = true;
            HandleFound();
        }
        else if (!nowTracked && m_isTracked)
        {
            m_isTracked = false;
            HandleLost();
        }
    }

    // ------------------------------------------------------------------
    // Found / lost logic
    // ------------------------------------------------------------------

    private void HandleFound()
    {
        Debug.Log("[VuforiaImageTargetManager] Target FOUND.");

        // Cancel any in-progress fade-out before showing overlays
        if (m_fadeCoroutine != null)
        {
            StopCoroutine(m_fadeCoroutine);
            m_fadeCoroutine = null;
        }

        SetChildrenActive(true);

        if (overlayRenderer != null)
        {
            overlayRenderer.ShowAll();
        }

        OnTargetFound?.Invoke();
    }

    private void HandleLost()
    {
        Debug.Log("[VuforiaImageTargetManager] Target LOST.");

        if (m_fadeCoroutine != null)
        {
            StopCoroutine(m_fadeCoroutine);
        }

        m_fadeCoroutine = StartCoroutine(FadeOutAndHide());
        OnTargetLost?.Invoke();
    }

    // ------------------------------------------------------------------
    // Fade coroutine
    // ------------------------------------------------------------------

    private IEnumerator FadeOutAndHide()
    {
        // Collect all CanvasGroup components on direct children so we can
        // fade them. Components without a CanvasGroup are hidden immediately.
        CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);

        float elapsed = 0f;

        while (elapsed < FadeDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / FadeDuration);

            foreach (CanvasGroup cg in groups)
            {
                if (cg != null)
                {
                    cg.alpha = alpha;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure fully invisible then deactivate
        foreach (CanvasGroup cg in groups)
        {
            if (cg != null)
            {
                cg.alpha = 0f;
            }
        }

        if (overlayRenderer != null)
        {
            overlayRenderer.HideAll();
        }

        SetChildrenActive(false);

        // Restore alpha for next tracking hit
        foreach (CanvasGroup cg in groups)
        {
            if (cg != null)
            {
                cg.alpha = 1f;
            }
        }

        m_fadeCoroutine = null;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void SetChildrenActive(bool active)
    {
        foreach (Transform child in transform)
        {
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }
    }
}
