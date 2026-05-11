// =============================================================================
// PDFExporter.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Generates a plain-text diagnostic report of all detected PCB
//              components and saves it to Application.persistentDataPath.
//              On iOS, triggers the native share sheet via a DllImport stub.
//              NOTE: Replace the .txt generation with iTextSharp or
//              Unity PDF Utility to produce real PDF output.
// Dependencies: ComponentDatabaseLoader.cs, ComponentData.cs, TextMeshPro,
//               UnityEngine.UI
// =============================================================================

using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any persistent scene GameObject. Call <see cref="ExportReport"/>
/// from UI buttons or <see cref="HUDController"/>.
/// </summary>
public class PDFExporter : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    private const string AppName         = "PCB-AR Viewer";
    private const string DiagnosticStatus = "diagnostic status: OK";
    private const float  ToastDuration    = 2f;

    // ------------------------------------------------------------------
    // iOS native stub
    // iOS native side (Swift/ObjC) must implement:
    //   @objc func ShareFile(_ path: UnsafePointer<CChar>) {
    //     let url = URL(fileURLWithPath: String(cString: path))
    //     let ac  = UIActivityViewController(activityItems: [url],
    //                                        applicationActivities: nil)
    //     UnityGetGLViewController()?.present(ac, animated: true)
    //   }
    // Register it as a Unity plugin in a .mm file and export as "ShareFile".
    // ------------------------------------------------------------------

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ShareFile(string filePath);
#endif

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Tooltip("Root CanvasGroup of the 'Report saved!' toast popup.")]
    [SerializeField] private CanvasGroup toastCanvasGroup;

    [Tooltip("TextMeshPro label inside the toast.")]
    [SerializeField] private TextMeshProUGUI toastLabel;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Start()
    {
        HideToastImmediate();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Generates a diagnostic report .txt file and triggers the iOS share
    /// sheet. Shows a 2-second toast message on completion.
    /// </summary>
    public void ExportReport()
    {
        if (!ComponentDatabaseLoader.IsLoaded)
        {
            Debug.LogWarning("[PDFExporter] Database not loaded yet — export aborted.");
            return;
        }

        string filePath = BuildFilePath();
        string content  = BuildReportContent();

        if (!WriteReport(filePath, content)) return;

        Debug.Log($"[PDFExporter] Report saved to: {filePath}");

        TriggerShareSheet(filePath);

        StartCoroutine(ShowToast("Report saved!"));
    }

    // ------------------------------------------------------------------
    // Report building
    // ------------------------------------------------------------------

    private static string BuildFilePath()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName  = $"report_{timestamp}.txt";
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    private static string BuildReportContent()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("=================================================");
        sb.AppendLine($"  {AppName}  —  Diagnostic Report");
        sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("=================================================");
        sb.AppendLine();
        sb.AppendLine("DETECTED COMPONENTS");
        sb.AppendLine("-------------------");

        foreach (ComponentData comp in ComponentDatabaseLoader.AllComponents)
        {
            if (comp == null) continue;

            sb.AppendLine(comp.GetFormattedSummary());
            sb.AppendLine($"  Description : {comp.description}");
            sb.AppendLine($"  Voltage     : {comp.voltage_rating}");
            sb.AppendLine($"  Datasheet   : {comp.datasheet_url}");
            sb.AppendLine();
        }

        sb.AppendLine("=================================================");
        sb.AppendLine(DiagnosticStatus);
        sb.AppendLine("=================================================");

        return sb.ToString();
    }

    private static bool WriteReport(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PDFExporter] Failed to write report. Exception: {ex.Message}");
            return false;
        }
    }

    // ------------------------------------------------------------------
    // Native share sheet
    // ------------------------------------------------------------------

    private static void TriggerShareSheet(string filePath)
    {
#if UNITY_IOS && !UNITY_EDITOR
        ShareFile(filePath);
#else
        Debug.Log($"[PDFExporter] Share sheet not available in Editor. Path: {filePath}");
#endif
    }

    // ------------------------------------------------------------------
    // Toast coroutine
    // ------------------------------------------------------------------

    private IEnumerator ShowToast(string message)
    {
        if (toastLabel != null) toastLabel.text = message;

        if (toastCanvasGroup != null)
        {
            toastCanvasGroup.alpha          = 1f;
            toastCanvasGroup.interactable   = false;
            toastCanvasGroup.blocksRaycasts = false;
        }

        yield return new WaitForSeconds(ToastDuration);

        HideToastImmediate();
    }

    private void HideToastImmediate()
    {
        if (toastCanvasGroup != null)
        {
            toastCanvasGroup.alpha = 0f;
        }
    }
}
