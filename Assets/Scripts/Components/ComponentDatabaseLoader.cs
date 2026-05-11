// =============================================================================
// ComponentDatabaseLoader.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Singleton MonoBehaviour that loads components_database.json
//              from StreamingAssets at runtime, deserialises it, and exposes
//              a static lookup dictionary for the rest of the app.
//              Uses UnityWebRequest for iOS StreamingAssets compatibility.
// Dependencies: ComponentData.cs, UnityEngine.Networking
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Loads and owns the component database. Access component records via
/// <see cref="GetComponent(string)"/> or iterate <see cref="AllComponents"/>.
/// </summary>
public class ComponentDatabaseLoader : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Singleton
    // ------------------------------------------------------------------

    /// <summary>Global singleton instance.</summary>
    public static ComponentDatabaseLoader Instance { get; private set; }

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Tooltip("File name inside StreamingAssets (no path prefix).")]
    [SerializeField] private string databaseFileName = "components_database.json";

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------

    private static readonly Dictionary<string, ComponentData> s_lookup =
        new Dictionary<string, ComponentData>();

    private static List<ComponentData> s_allComponents = new List<ComponentData>();

    /// <summary>True once the database has been loaded successfully.</summary>
    public static bool IsLoaded { get; private set; }

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
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(LoadDatabaseCoroutine());
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a read-only view of every loaded component.
    /// Returns an empty list if the database has not finished loading.
    /// </summary>
    public static IReadOnlyList<ComponentData> AllComponents => s_allComponents;

    /// <summary>
    /// Looks up a component by its PCB identifier (e.g. "R1", "U2").
    /// Returns <c>null</c> if the id is not found or the database is not loaded.
    /// </summary>
    /// <param name="id">Case-sensitive component id.</param>
    public static ComponentData GetComponent(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[ComponentDatabaseLoader] GetComponent called with null/empty id.");
            return null;
        }

        s_lookup.TryGetValue(id, out ComponentData result);
        return result;
    }

    // ------------------------------------------------------------------
    // Internal loading
    // ------------------------------------------------------------------

    private IEnumerator LoadDatabaseCoroutine()
    {
        string filePath = BuildStreamingAssetsPath(databaseFileName);

        using (UnityWebRequest request = UnityWebRequest.Get(filePath))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[ComponentDatabaseLoader] Failed to load database from '{filePath}'. " +
                    $"Error: {request.error}");
                yield break;
            }

            string json = request.downloadHandler.text;
            ParseDatabase(json);
        }
    }

    private void ParseDatabase(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError("[ComponentDatabaseLoader] Database JSON is empty.");
            return;
        }

        ComponentDatabase database = null;

        try
        {
            database = JsonUtility.FromJson<ComponentDatabase>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[ComponentDatabaseLoader] Failed to deserialise JSON. Exception: {ex.Message}");
            return;
        }

        if (database == null || database.components == null)
        {
            Debug.LogError(
                "[ComponentDatabaseLoader] Deserialised database is null or has no components list.");
            return;
        }

        s_lookup.Clear();
        s_allComponents.Clear();

        foreach (ComponentData comp in database.components)
        {
            if (comp == null)
            {
                Debug.LogWarning("[ComponentDatabaseLoader] Skipping null component entry.");
                continue;
            }

            if (string.IsNullOrEmpty(comp.id))
            {
                Debug.LogWarning("[ComponentDatabaseLoader] Skipping component with empty id.");
                continue;
            }

            if (s_lookup.ContainsKey(comp.id))
            {
                Debug.LogWarning(
                    $"[ComponentDatabaseLoader] Duplicate component id '{comp.id}' – skipping.");
                continue;
            }

            s_lookup[comp.id] = comp;
            s_allComponents.Add(comp);
        }

        IsLoaded = true;
        Debug.Log(
            $"[ComponentDatabaseLoader] Loaded {s_allComponents.Count} components successfully.");
    }

    private static string BuildStreamingAssetsPath(string fileName)
    {
        // On iOS, StreamingAssets lives inside the app bundle and must be
        // accessed via the "jar:file://" or plain "file://" URI schemes
        // depending on platform. UnityWebRequest handles this transparently
        // when we use Application.streamingAssetsPath.
#if UNITY_ANDROID && !UNITY_EDITOR
        return $"{Application.streamingAssetsPath}/{fileName}";
#else
        return $"file://{Application.streamingAssetsPath}/{fileName}";
#endif
    }
}
