// =============================================================================
// DatasheetUIController.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Singleton controller for the full-screen datasheet panel.
//              Fades the panel in/out and populates component info fields
//              including a clickable datasheet URL button.
// Dependencies: ComponentData.cs, TextMeshPro, UnityEngine.UI
// =============================================================================

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the root of the Datasheet Canvas or panel GameObject.
/// Set Canvas renderMode to ScreenSpaceOverlay in the Inspector.
/// </summary>
public class DatasheetUIController : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    private const float FadeInDuration  = 0.3f;
    private const float FadeOutDuration = 0.3f;

    // ------------------------------------------------------------------
    // Singleton
    // ------------------------------------------------------------------

    /// <summary>Global singleton — referenced by ComponentTapHandler.</summary>
    public static DatasheetUIController Instance { get; private set; }

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("Panel")]
    [Tooltip("Root CanvasGroup of the full-screen datasheet panel.")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Tooltip("Root GameObject of the panel (enabled/disabled to block raycasts).")]
    [SerializeField] private GameObject panelRoot;

    [Header("Content fields")]
    [Tooltip("Displays the component id (e.g. U1).")]
    [SerializeField] private TextMeshProUGUI txtComponentName;

    [Tooltip("Displays the component type.")]
    [SerializeField] private TextMeshProUGUI txtType;

    [Tooltip("Displays the component value.")]
    [SerializeField] private TextMeshProUGUI txtValue;

    [Tooltip("Displays the full description.")]
    [SerializeField] private TextMeshProUGUI txtDescription;

    [Tooltip("Displays the package / tolerance / voltage rating.")]
    [SerializeField] private TextMeshProUGUI txtDetails;

    [Header("Buttons")]
    [Tooltip("Button that opens the datasheet URL in the device browser.")]
    [SerializeField] private Button datasheetButton;

    [Tooltip("Label on the datasheet button.")]
    [SerializeField] private TextMeshProUGUI txtDatasheetUrl;

    [Tooltip("Button that dismisses the panel.")]
    [SerializeField] private Button closeButton;

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private ComponentData m_currentData;
    private Coroutine m_fadeCoroutine;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Ensure panel starts invisible and non-interactive
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha          = 0f;
            panelCanvasGroup.interactable   = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseDatasheet);
        }

        if (datasheetButton != null)
        {
            datasheetButton.onClick.AddListener(OpenDatasheetUrl);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)   closeButton.onClick.RemoveListener(CloseDatasheet);
        if (datasheetButton != null) datasheetButton.onClick.RemoveListener(OpenDatasheetUrl);
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Populates the datasheet panel with <paramref name="data"/> and fades it in.
    /// Safe to call while the panel is already open (re-populates with new data).
    /// </summary>
    /// <param name="data">Component data to display. Must not be null.</param>
    public void OpenDatasheet(ComponentData data)
    {
        if (data == null)
        {
            Debug.LogError("[DatasheetUIController] OpenDatasheet called with null data.");
            return;
        }

        m_currentData = data;
        PopulateFields(data);

        if (m_fadeCoroutine != null) StopCoroutine(m_fadeCoroutine);
        m_fadeCoroutine = StartCoroutine(FadePanel(0f, 1f, FadeInDuration, true));
    }

    /// <summary>Fades the datasheet panel out and hides it.</summary>
    public void CloseDatasheet()
    {
        if (m_fadeCoroutine != null) StopCoroutine(m_fadeCoroutine);
        m_fadeCoroutine = StartCoroutine(FadePanel(1f, 0f, FadeOutDuration, false));
    }

    // ------------------------------------------------------------------
    // Field population
    // ------------------------------------------------------------------

    private void PopulateFields(ComponentData data)
    {
        SetText(txtComponentName, data.id);
        SetText(txtType,          data.type);
        SetText(txtValue,         data.value);
        SetText(txtDescription,   data.description);

        string details = $"Package: {data.package}  |  " +
                         $"Tolerance: {data.tolerance}  |  " +
                         $"Vmax: {data.voltage_rating}";
        SetText(txtDetails, details);

        SetText(txtDatasheetUrl, data.datasheet_url ?? "No datasheet URL");

        if (datasheetButton != null)
        {
            datasheetButton.interactable = !string.IsNullOrEmpty(data.datasheet_url);
        }
    }

    private static void SetText(TextMeshProUGUI field, string text)
    {
        if (field != null) field.text = text ?? string.Empty;
    }

    // ------------------------------------------------------------------
    // URL handler
    // ------------------------------------------------------------------

    private void OpenDatasheetUrl()
    {
        if (m_currentData == null) return;

        string url = m_currentData.datasheet_url;

        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("[DatasheetUIController] No datasheet URL for this component.");
            return;
        }

        Application.OpenURL(url);
    }

    // ------------------------------------------------------------------
    // Fade coroutine
    // ------------------------------------------------------------------

    private IEnumerator FadePanel(float from, float to, float duration, bool activateOnStart)
    {
        if (activateOnStart && panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.interactable   = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(from, to, elapsed / duration);

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = alpha;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha          = to;
            panelCanvasGroup.interactable   = (to >= 1f);
            panelCanvasGroup.blocksRaycasts = (to >= 1f);
        }

        if (!activateOnStart && panelRoot != null && to <= 0f)
        {
            panelRoot.SetActive(false);
        }

        m_fadeCoroutine = null;
    }
}
