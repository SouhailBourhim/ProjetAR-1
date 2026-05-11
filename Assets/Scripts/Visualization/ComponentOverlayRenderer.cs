// =============================================================================
// ComponentOverlayRenderer.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Spawns one label prefab per component from the database and
//              positions it at the component's PCB offset relative to the
//              ImageTarget transform. Exposes ShowAll / HideAll for the
//              tracking manager to call.
// Dependencies: ComponentDatabaseLoader.cs, ComponentInfoDisplay.cs,
//               ComponentData.cs
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to the Vuforia ImageTarget GameObject (alongside
/// <see cref="VuforiaImageTargetManager"/>). Assign
/// <see cref="componentLabelPrefab"/> in the Inspector.
/// </summary>
public class ComponentOverlayRenderer : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Tooltip("Prefab to instantiate for each component. Must have a " +
             "ComponentInfoDisplay component at its root.")]
    [SerializeField] private GameObject componentLabelPrefab;

    [Tooltip("Seconds to wait between database-ready polls before spawning.")]
    [SerializeField] private float databasePollInterval = 0.2f;

    [Tooltip("Maximum seconds to wait for the database before giving up.")]
    [SerializeField] private float databasePollTimeout = 15f;

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private readonly List<GameObject> m_activeOverlays = new List<GameObject>();

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Start()
    {
        if (componentLabelPrefab == null)
        {
            Debug.LogError(
                "[ComponentOverlayRenderer] componentLabelPrefab is not assigned.");
            return;
        }

        StartCoroutine(WaitForDatabaseThenSpawn());
    }

    private void OnDestroy()
    {
        DestroyAllOverlays();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Makes all spawned overlay GameObjects visible.</summary>
    public void ShowAll()
    {
        foreach (GameObject overlay in m_activeOverlays)
        {
            if (overlay != null)
            {
                overlay.SetActive(true);
            }
        }
    }

    /// <summary>Hides all spawned overlay GameObjects.</summary>
    public void HideAll()
    {
        foreach (GameObject overlay in m_activeOverlays)
        {
            if (overlay != null)
            {
                overlay.SetActive(false);
            }
        }
    }

    /// <summary>Returns the number of component overlays currently spawned.</summary>
    public int OverlayCount => m_activeOverlays.Count;

    // ------------------------------------------------------------------
    // Spawning
    // ------------------------------------------------------------------

    private IEnumerator WaitForDatabaseThenSpawn()
    {
        float elapsed = 0f;

        while (!ComponentDatabaseLoader.IsLoaded && elapsed < databasePollTimeout)
        {
            elapsed += databasePollInterval;
            yield return new WaitForSeconds(databasePollInterval);
        }

        if (!ComponentDatabaseLoader.IsLoaded)
        {
            Debug.LogError(
                "[ComponentOverlayRenderer] Database did not load within timeout. " +
                "No overlays will be spawned.");
            yield break;
        }

        SpawnOverlays();
    }

    private void SpawnOverlays()
    {
        IReadOnlyList<ComponentData> components = ComponentDatabaseLoader.AllComponents;

        if (components == null || components.Count == 0)
        {
            Debug.LogWarning("[ComponentOverlayRenderer] Database is empty – nothing to spawn.");
            return;
        }

        foreach (ComponentData data in components)
        {
            if (data == null) continue;

            SpawnOverlayForComponent(data);
        }

        Debug.Log(
            $"[ComponentOverlayRenderer] Spawned {m_activeOverlays.Count} component overlays.");

        // Start hidden; VuforiaImageTargetManager will call ShowAll on track
        HideAll();
    }

    private void SpawnOverlayForComponent(ComponentData data)
    {
        Vector3 localOffset = data.position_on_pcb != null
            ? data.position_on_pcb.ToVector3()
            : Vector3.zero;

        // Instantiate as a child of the ImageTarget so it moves with the PCB
        GameObject label = Instantiate(
            componentLabelPrefab,
            transform.TransformPoint(localOffset),
            Quaternion.identity,
            transform);

        label.name = $"Label_{data.id}";

        ComponentInfoDisplay display = label.GetComponent<ComponentInfoDisplay>();

        if (display == null)
        {
            Debug.LogWarning(
                $"[ComponentOverlayRenderer] Prefab for '{data.id}' has no " +
                "ComponentInfoDisplay component. Label will show no data.");
        }
        else
        {
            display.SetData(data);
        }

        m_activeOverlays.Add(label);
    }

    private void DestroyAllOverlays()
    {
        foreach (GameObject overlay in m_activeOverlays)
        {
            if (overlay != null)
            {
                Destroy(overlay);
            }
        }

        m_activeOverlays.Clear();
    }
}
