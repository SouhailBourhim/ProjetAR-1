// =============================================================================
// ParticleFlowController.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Flows particles along a series of waypoints representing a
//              signal path on the PCB. Colour switches between normal (green)
//              and fault (red) via the IsFault property. Implemented with a
//              custom per-particle Lerp update — no job system dependency.
// Dependencies: None (pure UnityEngine)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any GameObject on the ImageTarget hierarchy.
/// Assign <see cref="waypoints"/> in the Inspector to define the signal path.
/// </summary>
public class ParticleFlowController : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    private const int ParticlesPerSegment = 20;
    private const float ParticleSpeed     = 0.05f; // units per second

    private static readonly Color ColorNormal = new Color(0f, 1f, 0.255f, 1f); // #00FF41
    private static readonly Color ColorFault  = Color.red;                       // #FF0000

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Tooltip("Ordered list of world-space (or local-space) waypoints that " +
             "define the signal path on the PCB.")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();

    [Tooltip("When true, particles turn red to indicate a fault condition.")]
    [SerializeField] private bool isFault;

    [Tooltip("Size of each particle in world units.")]
    [SerializeField] private float particleSize = 0.003f;

    // ------------------------------------------------------------------
    // Public property
    // ------------------------------------------------------------------

    /// <summary>
    /// Toggle between normal (green) and fault (red) particle colour at runtime.
    /// </summary>
    public bool IsFault
    {
        get => isFault;
        set
        {
            isFault = value;
            ApplyColour();
        }
    }

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private ParticleSystem m_ps;
    private ParticleSystem.Particle[] m_particles;

    // Each particle is tracked as a normalised distance [0, totalPath]
    private float[] m_particleDistances;
    private float m_totalPathLength;
    private List<float> m_segmentLengths = new List<float>();

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Start()
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            Debug.LogWarning(
                "[ParticleFlowController] Need at least 2 waypoints to define a path.");
            return;
        }

        BuildPath();
        CreateParticleSystem();
        InitialiseParticles();
    }

    private void Update()
    {
        if (m_ps == null || m_particleDistances == null) return;

        UpdateParticlePositions();
    }

    private void OnDestroy()
    {
        if (m_ps != null)
        {
            Destroy(m_ps.gameObject);
        }
    }

    // ------------------------------------------------------------------
    // Path construction
    // ------------------------------------------------------------------

    private void BuildPath()
    {
        m_segmentLengths.Clear();
        m_totalPathLength = 0f;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null)
            {
                m_segmentLengths.Add(0f);
                continue;
            }

            float len = Vector3.Distance(
                waypoints[i].position, waypoints[i + 1].position);

            m_segmentLengths.Add(len);
            m_totalPathLength += len;
        }

        if (m_totalPathLength <= 0f)
        {
            Debug.LogError("[ParticleFlowController] Path has zero length.");
        }
    }

    // ------------------------------------------------------------------
    // Particle system setup
    // ------------------------------------------------------------------

    private void CreateParticleSystem()
    {
        GameObject psGO = new GameObject("SignalParticles");
        psGO.transform.SetParent(transform, false);

        m_ps = psGO.AddComponent<ParticleSystem>();

        // Stop the built-in emission – we drive particles manually
        ParticleSystem.EmissionModule emission = m_ps.emission;
        emission.enabled = false;

        ParticleSystem.MainModule main = m_ps.main;
        main.loop              = false;
        main.playOnAwake       = false;
        main.maxParticles      = ParticlesPerSegment * (waypoints.Count - 1);
        main.startLifetime     = float.MaxValue; // we manage lifetime ourselves
        main.startSpeed        = 0f;
        main.startSize         = particleSize;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;

        ApplyColour();

        // Disable the renderer's built-in sorting – position is set manually
        ParticleSystem.ShapeModule shape = m_ps.shape;
        shape.enabled = false;
    }

    private void ApplyColour()
    {
        if (m_ps == null) return;

        ParticleSystem.MainModule main = m_ps.main;
        main.startColor = isFault ? ColorFault : ColorNormal;

        // Update live particles
        if (m_particles == null) return;

        int count = m_ps.GetParticles(m_particles);
        Color c   = isFault ? ColorFault : ColorNormal;

        for (int i = 0; i < count; i++)
        {
            m_particles[i].startColor = c;
        }

        m_ps.SetParticles(m_particles, count);
    }

    // ------------------------------------------------------------------
    // Particle initialisation
    // ------------------------------------------------------------------

    private void InitialiseParticles()
    {
        int totalParticles = ParticlesPerSegment * (waypoints.Count - 1);
        m_particles          = new ParticleSystem.Particle[totalParticles];
        m_particleDistances  = new float[totalParticles];

        Color startColor = isFault ? ColorFault : ColorNormal;

        for (int i = 0; i < totalParticles; i++)
        {
            // Distribute evenly along the path
            m_particleDistances[i] =
                (float)i / totalParticles * m_totalPathLength;

            m_particles[i] = new ParticleSystem.Particle
            {
                position   = GetPositionAtDistance(m_particleDistances[i]),
                startColor = startColor,
                startSize  = particleSize,
                startLifetime = float.MaxValue,
                remainingLifetime = float.MaxValue,
            };
        }

        m_ps.SetParticles(m_particles, totalParticles);
        m_ps.Play();
    }

    // ------------------------------------------------------------------
    // Per-frame update
    // ------------------------------------------------------------------

    private void UpdateParticlePositions()
    {
        if (m_totalPathLength <= 0f) return;

        float delta = ParticleSpeed * Time.deltaTime;

        for (int i = 0; i < m_particleDistances.Length; i++)
        {
            m_particleDistances[i] =
                (m_particleDistances[i] + delta) % m_totalPathLength;

            m_particles[i].position =
                GetPositionAtDistance(m_particleDistances[i]);
        }

        m_ps.SetParticles(m_particles, m_particles.Length);
    }

    // ------------------------------------------------------------------
    // Path sampling
    // ------------------------------------------------------------------

    private Vector3 GetPositionAtDistance(float distance)
    {
        float remaining = distance;

        for (int i = 0; i < m_segmentLengths.Count; i++)
        {
            float segLen = m_segmentLengths[i];

            if (segLen <= 0f) continue;

            if (remaining <= segLen)
            {
                float t = remaining / segLen;
                return Vector3.Lerp(
                    waypoints[i].position,
                    waypoints[i + 1].position,
                    t);
            }

            remaining -= segLen;
        }

        // Clamp to last waypoint
        return waypoints[waypoints.Count - 1].position;
    }
}
