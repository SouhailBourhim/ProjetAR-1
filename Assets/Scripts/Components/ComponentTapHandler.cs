// =============================================================================
// ComponentTapHandler.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Handles single-finger tap input in the AR scene. Uses AR
//              Foundation's ARRaycastManager first, then falls back to a
//              Physics.Raycast via Camera.main for Vuforia overlay colliders.
//              Opens the datasheet panel when a ComponentInfoDisplay is hit.
// Dependencies: AR Foundation 5.x, ComponentInfoDisplay.cs,
//               DatasheetUIController.cs
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Attach to a persistent scene GameObject (e.g. "InputManager").
/// Requires an <see cref="ARRaycastManager"/> in the scene.
/// </summary>
public class ComponentTapHandler : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    private const float MaxRayDistance = 100f;

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Tooltip("Layer mask for Physics.Raycast fallback. " +
             "Set to the layer your label colliders are on.")]
    [SerializeField] private LayerMask labelLayerMask = ~0;

    [Tooltip("Maximum pixel distance movement allowed for a touch to count as a tap.")]
    [SerializeField] private float maxTapMovement = 20f;

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private ARRaycastManager m_raycastManager;
    private static readonly List<ARRaycastHit> s_arHits = new List<ARRaycastHit>();

    // Store touch-start position to distinguish taps from swipes
    private Vector2 m_touchStartPos;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        m_raycastManager = FindObjectOfType<ARRaycastManager>();

        if (m_raycastManager == null)
        {
            Debug.LogWarning(
                "[ComponentTapHandler] ARRaycastManager not found. " +
                "Will use Physics fallback only.");
        }
    }

    private void Update()
    {
        ProcessTouchInput();
    }

    // ------------------------------------------------------------------
    // Input processing
    // ------------------------------------------------------------------

    private void ProcessTouchInput()
    {
        if (Input.touchCount != 1) return;

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                m_touchStartPos = touch.position;
                break;

            case TouchPhase.Ended:
                // Reject if finger moved too far (swipe, not tap)
                if (Vector2.Distance(touch.position, m_touchStartPos) > maxTapMovement)
                    return;

                HandleTap(touch.position);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Hit testing
    // ------------------------------------------------------------------

    private void HandleTap(Vector2 screenPos)
    {
        // 1. Try AR Foundation raycast (hits AR planes / feature points)
        if (TryARRaycast(screenPos, out Vector3 arWorldPos))
        {
            // AR raycast hit the real world – check if a label collider
            // is near that world position
            ComponentInfoDisplay nearby = FindNearbyDisplay(arWorldPos);
            if (nearby != null)
            {
                OpenDatasheet(nearby);
                return;
            }
        }

        // 2. Fallback: Physics raycast via Camera.main (hits label colliders
        //    that Vuforia renders in 3D space)
        if (Camera.main == null)
        {
            Debug.LogWarning("[ComponentTapHandler] Camera.main is null.");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, MaxRayDistance, labelLayerMask))
        {
            ComponentInfoDisplay display =
                hit.collider.GetComponentInParent<ComponentInfoDisplay>();

            if (display != null)
            {
                OpenDatasheet(display);
            }
        }
    }

    private bool TryARRaycast(Vector2 screenPos, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (m_raycastManager == null) return false;

        s_arHits.Clear();
        bool hit = m_raycastManager.Raycast(
            screenPos,
            s_arHits,
            TrackableType.AllTypes);

        if (hit && s_arHits.Count > 0)
        {
            worldPosition = s_arHits[0].pose.position;
            return true;
        }

        return false;
    }

    private static ComponentInfoDisplay FindNearbyDisplay(Vector3 worldPos)
    {
        // Use a small overlap sphere to find any label near the AR hit point
        Collider[] hits = Physics.OverlapSphere(worldPos, 0.05f);

        foreach (Collider col in hits)
        {
            if (col == null) continue;

            ComponentInfoDisplay display =
                col.GetComponentInParent<ComponentInfoDisplay>();

            if (display != null) return display;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Action
    // ------------------------------------------------------------------

    private static void OpenDatasheet(ComponentInfoDisplay display)
    {
        ComponentData data = display.GetData();

        if (data == null)
        {
            Debug.LogWarning("[ComponentTapHandler] Tapped label has no ComponentData.");
            return;
        }

        if (DatasheetUIController.Instance == null)
        {
            Debug.LogError("[ComponentTapHandler] DatasheetUIController.Instance is null.");
            return;
        }

        DatasheetUIController.Instance.OpenDatasheet(data);
    }
}
