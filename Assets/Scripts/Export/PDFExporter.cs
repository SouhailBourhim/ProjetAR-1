// =============================================================================
// PDFExporter.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Generates a plain-text diagnostic report of all detected PCB
//              components and saves it to Application.persistentDataPath.
//              On Android, shares the report text via ACTION_SEND intent.
//              NOTE: Replace the .txt generation with iTextSharp or
//              Unity PDF Utility to produce real PDF output.
// Dependencies: ComponentDatabaseLoader.cs, ComponentData.cs, TextMeshPro,
//               UnityEngine.UI
// =============================================================================

using System;
using System.Collections;
using System.IO;
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
    /// Generates a diagnostic report .txt file and triggers the Android share
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

        TriggerShareSheet(content);

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
    // Android share intent
    // Shares the report text via ACTION_SEND so the user can save or
    // forward it with any installed app (Gmail, Drive, Files, etc.).
    // File-URI sharing (ACTION_SEND with a Uri) requires a FileProvider
    // entry in AndroidManifest.xml; text sharing used here avoids that
    // dependency while still surfacing all the report content.
    // ------------------------------------------------------------------

    private static void TriggerShareSheet(string content)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass intentClass =
                       new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaObject intent =
                       new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>(
                    "setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>(
                    "putExtra",
                    intentClass.GetStatic<string>("EXTRA_SUBJECT"),
                    "PCB-AR Diagnostic Report");
                intent.Call<AndroidJavaObject>(
                    "putExtra",
                    intentClass.GetStatic<string>("EXTRA_TEXT"),
                    content);

                using (AndroidJavaClass unity =
                           new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                           unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject chooser =
                           intentClass.CallStatic<AndroidJavaObject>(
                               "createChooser", intent, "Share Report via…"))
                {
                    activity.Call("startActivity", chooser);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PDFExporter] Android share intent failed: {ex.Message}");
        }
#else
        Debug.Log("[PDFExporter] Share intent not available in Editor.");
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
