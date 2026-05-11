// =============================================================================
// ComponentInfoDisplay.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Drives the UI on a component label prefab. Populates TMP fields
//              from ComponentData and animates the label in with a coroutine-
//              based scale-from-zero animation (no LeanTween dependency).
// Dependencies: ComponentData.cs, TextMeshPro, UnityEngine.UI
// =============================================================================

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the root of each component label prefab.
/// Assign TMP and Image references in the Inspector.
/// </summary>
public class ComponentInfoDisplay : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    private const float AnimDuration = 0.2f;

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("TMP Labels")]
    [Tooltip("Displays the component identifier (e.g. R1).")]
    [SerializeField] private TextMeshProUGUI tmpId;

    [Tooltip("Displays the component type (e.g. resistor).")]
    [SerializeField] private TextMeshProUGUI tmpType;

    [Tooltip("Displays the component value (e.g. 10kΩ).")]
    [SerializeField] private TextMeshProUGUI tmpValue;

    [Tooltip("Displays the component description.")]
    [SerializeField] private TextMeshProUGUI tmpDescription;

    [Header("Visuals")]
    [Tooltip("Background Image whose color is set from component data.color_hex.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("AnimationCurve controlling the scale-in easing (X-axis: 0-1 normalised time).")]
    [SerializeField] private AnimationCurve scaleInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private ComponentData m_data;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        // Start invisible until SetData is called
        transform.localScale = Vector3.zero;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------>

    /// <summary>
    /// Populates all label fields from <paramref name="data"/> and animates
    /// the label into view. Safe to call before the GameObject is active.
    /// </summary>
    /// <param name="data">Component record from the database. Must not be null.</param>
    public void SetData(ComponentData data)
    {
        if (data == null)
        {
            Debug.LogError("[ComponentInfoDisplay] SetData called with null ComponentData.");
            return;
        }

        m_data = data;

        SetTextField(tmpId,          data.id);
        SetTextField(tmpType,        data.type);
        SetTextField(tmpValue,       data.value);
        SetTextField(tmpDescription, data.description);

        ApplyBackgroundColor(data.color_hex);

        // Animate in (restarts if already running)
        StopAllCoroutines();
        StartCoroutine(AnimateScaleIn());
    }

    /// <summary>Returns the component data currently displayed, or null if not set.</summary>
    public ComponentData GetData() => m_data;

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void SetTextField(TextMeshProUGUI field, string text)
    {
        if (field != null)
        {
            field.text = text ?? string.Empty;
        }
    }

    private void ApplyBackgroundColor(string hexColor)
    {
        if (backgroundImage == null) return;

        if (string.IsNullOrEmpty(hexColor))
        {
            backgroundImage.color = Color.white;
            return;
        }

        if (ColorUtility.TryParseHtmlString(hexColor, out Color parsed))
        {
            // Apply a fixed alpha so labels stay semi-transparent over the PCB
            parsed.a = 0.85f;
            backgroundImage.color = parsed;
        }
        else
        {
            Debug.LogWarning(
                $"[ComponentInfoDisplay] Could not parse color '{hexColor}'. Using white.");
            backgroundImage.color = Color.white;
        }
    }

    private IEnumerator AnimateScaleIn()
    {
        float elapsed = 0f;

        while (elapsed < AnimDuration)
        {
            float t = Mathf.Clamp01(elapsed / AnimDuration);
            float scale = scaleInCurve.Evaluate(t);
            transform.localScale = Vector3.one * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = Vector3.one;
    }
}
