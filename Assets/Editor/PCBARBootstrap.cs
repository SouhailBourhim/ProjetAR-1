// =============================================================================
// PCBARBootstrap.cs  —  EDITOR ONLY  (Assets/Editor/)
// Author:      [Your Name]
// Date:        2024-01-01
// Description: One-click scene bootstrapper. Creates the full PCB-AR
//              GameObject hierarchy, adds every custom component, wires all
//              cross-references via SerializedObject, and hooks the Vuforia
//              UnityEvents to HUDController callbacks.
//              Run ONCE after all packages are imported and scripts compile.
//
//              Menu: Tools ▸ PCB-AR ▸ Bootstrap Scene   (Cmd/Ctrl + Shift + B)
//
// Dependencies: AR Foundation 5.x, Apple ARKit XR Plugin, Vuforia Engine,
//               TextMeshPro, Unity.XR.CoreUtils (ships with AR Foundation)
//               All scripts in Assets/Scripts/ must compile cleanly first.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

// Suppress "field never assigned" warnings — we assign via SerializedObject
#pragma warning disable CS0649

public static class PCBARBootstrap
{
    // ------------------------------------------------------------------
    // Menu entry
    // ------------------------------------------------------------------

    [MenuItem("Tools/PCB-AR/Bootstrap Scene %#b")]
    public static void BootstrapScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Bootstrap PCB-AR Scene",
            "This will build the full PCB-AR hierarchy in the active scene.\n\n" +
            "Existing root GameObjects with matching names are skipped (safe to re-run).\n\n" +
            "Proceed?",
            "Bootstrap", "Cancel"))
        {
            return;
        }

        try
        {
            // ---- creation order matters: lower-level objects first ----
            var dbManager      = CreateDatabaseManager();
            var arSession      = CreateARSession();
            var xrOrigin       = CreateXROrigin(out GameObject arCamera);
            var imageTarget    = CreateImageTarget();
            var signalPath     = CreateSignalPath(imageTarget,
                                     out Transform wpA, out Transform wpB, out Transform wpC);
            var hudCanvas      = CreateHUDCanvas(out HUDController hudCtrl,
                                     out PDFExporter pdfExp,
                                     out TextMeshProUGUI tmpStatus,
                                     out TextMeshProUGUI tmpCount,
                                     out TextMeshProUGUI tmpFps,
                                     out Image statusDot,
                                     out Button exportBtn,
                                     out CanvasGroup toastCG,
                                     out TextMeshProUGUI toastLabel);
            var dsCanvas       = CreateDatasheetCanvas(out DatasheetUIController dsCtrl);
            var arManager      = CreateARManager();
            var inputManager   = CreateInputManager();

            // ---- wire all cross-references -------------------------
            WireImageTarget(imageTarget);
            WireSignalPath(signalPath, wpA, wpB, wpC);
            WireHUD(hudCtrl, pdfExp, tmpCount, tmpFps, statusDot, exportBtn,
                    toastCG, toastLabel);
            WireARManager(arManager, imageTarget, tmpStatus);
            WireUnityEvents(imageTarget, hudCtrl);

            // ---- mark scene dirty ----------------------------------
            UnityEditor.SceneManagement.EditorSceneManager
                .MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            // ---- select the root objects for easy inspection -------
            Selection.objects = new UnityEngine.Object[]
            {
                dbManager, arSession, xrOrigin, arManager,
                inputManager, imageTarget, hudCanvas, dsCanvas
            };

            Debug.Log("[PCBARBootstrap] Scene bootstrapped successfully. " +
                      "Assign the ComponentLabel prefab to ComponentOverlayRenderer, " +
                      "then configure Vuforia's ImageTargetBehaviour manually.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PCBARBootstrap] Bootstrap failed: {ex.Message}\n{ex.StackTrace}");
            EditorUtility.DisplayDialog("Bootstrap Error",
                $"An error occurred:\n{ex.Message}\n\nCheck the Console for details.",
                "OK");
        }
    }

    // ------------------------------------------------------------------
    // Validation helper (greyed-out menu when in play mode)
    // ------------------------------------------------------------------

    [MenuItem("Tools/PCB-AR/Bootstrap Scene %#b", true)]
    private static bool ValidateBootstrap() => !Application.isPlaying;

    // ================================================================
    // SECTION 1 — Root GameObjects
    // ================================================================

    private static GameObject CreateDatabaseManager()
    {
        var go = GetOrCreate("DatabaseManager");
        EnsureComponent<ComponentDatabaseLoader>(go);
        RegisterUndo(go, "Create DatabaseManager");
        return go;
    }

    private static GameObject CreateARSession()
    {
        var go = GetOrCreate("AR Session");
        EnsureComponent<ARSession>(go);
        RegisterUndo(go, "Create AR Session");
        return go;
    }

    private static GameObject CreateXROrigin(out GameObject arCamera)
    {
        // XR Origin (replaces ARSessionOrigin in AR Foundation 5.x)
        var originGO = GetOrCreate("XR Origin");
        var xrOrigin = EnsureComponent<XROrigin>(originGO);

        // Camera Offset child
        var offsetGO = GetOrCreateChild(originGO, "Camera Offset");

        // AR Camera child
        var cameraGO = GetOrCreateChild(offsetGO, "Main Camera");
        cameraGO.tag = "MainCamera";

        var cam = EnsureComponent<Camera>(cameraGO);
        cam.clearFlags      = CameraClearFlags.Color;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane   = 0.01f;

        EnsureComponent<ARCameraManager>(cameraGO);
        EnsureComponent<ARCameraBackground>(cameraGO);
        EnsureComponent<ARRaycastManager>(originGO);

        // Wire the XROrigin camera
        var so = new SerializedObject(xrOrigin);
        so.FindProperty("m_Camera").objectReferenceValue = cam;
        so.ApplyModifiedProperties();

        RegisterUndo(originGO, "Create XR Origin");
        arCamera = cameraGO;
        return originGO;
    }

    private static GameObject CreateARManager()
    {
        var go = GetOrCreate("ARManager");
        EnsureComponent<ARSessionManager>(go);
        RegisterUndo(go, "Create ARManager");
        return go;
    }

    private static GameObject CreateInputManager()
    {
        var go = GetOrCreate("InputManager");
        EnsureComponent<ComponentTapHandler>(go);
        RegisterUndo(go, "Create InputManager");
        return go;
    }

    // ================================================================
    // SECTION 2 — ImageTarget hierarchy
    // ================================================================

    private static GameObject CreateImageTarget()
    {
        var go = GetOrCreate("ImageTarget");

        // Vuforia's ImageTargetBehaviour must be added from the Vuforia
        // package once imported. We add our custom scripts here.
        EnsureComponent<VuforiaImageTargetManager>(go);
        EnsureComponent<ComponentOverlayRenderer>(go);

        RegisterUndo(go, "Create ImageTarget");
        return go;
    }

    private static GameObject CreateSignalPath(GameObject imageTarget,
        out Transform wpA, out Transform wpB, out Transform wpC)
    {
        var pathGO = GetOrCreateChild(imageTarget, "SignalPath");
        var flow   = EnsureComponent<ParticleFlowController>(pathGO);

        // Three waypoints spread across a typical PCB trace
        var goA = GetOrCreateChild(pathGO, "Waypoint_A");
        var goB = GetOrCreateChild(pathGO, "Waypoint_B");
        var goC = GetOrCreateChild(pathGO, "Waypoint_C");

        goA.transform.localPosition = new Vector3(-0.02f, 0f,  0.00f);
        goB.transform.localPosition = new Vector3( 0.00f, 0f,  0.02f);
        goC.transform.localPosition = new Vector3( 0.02f, 0f,  0.00f);

        wpA = goA.transform;
        wpB = goB.transform;
        wpC = goC.transform;

        RegisterUndo(pathGO, "Create SignalPath");
        return pathGO;
    }

    // ================================================================
    // SECTION 3 — HUD Canvas
    // ================================================================

    private static GameObject CreateHUDCanvas(
        out HUDController hudCtrl,
        out PDFExporter   pdfExp,
        out TextMeshProUGUI tmpStatus,
        out TextMeshProUGUI tmpCount,
        out TextMeshProUGUI tmpFps,
        out Image           statusDot,
        out Button          exportBtn,
        out CanvasGroup     toastCG,
        out TextMeshProUGUI toastLabel)
    {
        // Root canvas
        var canvasGO = GetOrCreate("HUDCanvas");
        var canvas   = EnsureComponent<Canvas>(canvasGO);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        EnsureComponent<CanvasScaler>(canvasGO);
        EnsureComponent<GraphicRaycaster>(canvasGO);

        hudCtrl = EnsureComponent<HUDController>(canvasGO);
        pdfExp  = EnsureComponent<PDFExporter>(canvasGO);

        // ── Top bar ─────────────────────────────────────────────────
        var topBar    = GetOrCreateChild(canvasGO, "TopBar");
        var topBarImg = EnsureComponent<Image>(topBar);
        topBarImg.color = new Color(0f, 0f, 0f, 0.65f);
        SetRect(topBar, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -80), new Vector2(0, 0));

        // App title
        var titleGO = GetOrCreateChild(topBar, "TxtAppTitle");
        var txtTitle = EnsureComponent<TextMeshProUGUI>(titleGO);
        txtTitle.text      = "PCB-AR Viewer";
        txtTitle.fontSize  = 22;
        txtTitle.fontStyle = FontStyles.Bold;
        txtTitle.color     = Color.white;
        SetRect(titleGO, new Vector2(0, 0), new Vector2(0.4f, 1),
                new Vector2(12, 0), new Vector2(0, 0));

        // Status dot
        var dotGO = GetOrCreateChild(topBar, "StatusDot");
        statusDot = EnsureComponent<Image>(dotGO);
        statusDot.color = new Color(0.9f, 0.15f, 0.15f); // starts red
        SetRect(dotGO, new Vector2(0.4f, 0.5f), new Vector2(0.4f, 0.5f),
                new Vector2(-8, -8), new Vector2(16, 16));

        // Component count
        var countGO = GetOrCreateChild(topBar, "TxtComponentCount");
        tmpCount = EnsureComponent<TextMeshProUGUI>(countGO);
        tmpCount.text     = "Components: 0";
        tmpCount.fontSize = 16;
        tmpCount.color    = Color.white;
        SetRect(countGO, new Vector2(0.43f, 0), new Vector2(0.72f, 1),
                new Vector2(0, 0), new Vector2(0, 0));

        // Export button
        var exportGO = GetOrCreateChild(topBar, "ExportButton");
        exportBtn = EnsureComponent<Button>(exportGO);
        EnsureComponent<Image>(exportGO).color = new Color(0.2f, 0.5f, 1f);
        SetRect(exportGO, new Vector2(0.73f, 0.1f), new Vector2(0.93f, 0.9f),
                new Vector2(0, 0), new Vector2(0, 0));
        var exportLabelGO = GetOrCreateChild(exportGO, "Label");
        var exportLabel   = EnsureComponent<TextMeshProUGUI>(exportLabelGO);
        exportLabel.text      = "Export";
        exportLabel.fontSize  = 15;
        exportLabel.alignment = TextAlignmentOptions.Center;
        exportLabel.color     = Color.white;
        SetRect(exportLabelGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // FPS counter (bottom-right corner, outside top bar)
        var fpsGO = GetOrCreateChild(canvasGO, "TxtFPS");
        tmpFps = EnsureComponent<TextMeshProUGUI>(fpsGO);
        tmpFps.text      = "60 FPS";
        tmpFps.fontSize  = 13;
        tmpFps.color     = new Color(0.8f, 0.8f, 0.8f);
        tmpFps.alignment = TextAlignmentOptions.BottomRight;
        SetRect(fpsGO, new Vector2(0.8f, 0), new Vector2(1, 0),
                new Vector2(-10, 10), new Vector2(80, 30));

        // AR status label (below top bar — driven by ARSessionManager)
        var statusGO = GetOrCreateChild(canvasGO, "TxtARStatus");
        tmpStatus = EnsureComponent<TextMeshProUGUI>(statusGO);
        tmpStatus.text      = "Initializing…";
        tmpStatus.fontSize  = 14;
        tmpStatus.color     = new Color(1f, 0.85f, 0.2f);
        tmpStatus.alignment = TextAlignmentOptions.TopLeft;
        SetRect(statusGO, new Vector2(0, 1), new Vector2(0.5f, 1),
                new Vector2(12, -86), new Vector2(0, 30));

        // ── Toast popup ──────────────────────────────────────────────
        var toastGO = GetOrCreateChild(canvasGO, "Toast");
        toastCG = EnsureComponent<CanvasGroup>(toastGO);
        toastCG.alpha = 0f;
        var toastImg = EnsureComponent<Image>(toastGO);
        toastImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        SetRect(toastGO, new Vector2(0.3f, 0), new Vector2(0.7f, 0),
                new Vector2(0, 20), new Vector2(0, 46));

        var toastTextGO = GetOrCreateChild(toastGO, "TxtToast");
        toastLabel = EnsureComponent<TextMeshProUGUI>(toastTextGO);
        toastLabel.text      = "Report saved!";
        toastLabel.fontSize  = 16;
        toastLabel.color     = Color.white;
        toastLabel.alignment = TextAlignmentOptions.Center;
        SetRect(toastTextGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RegisterUndo(canvasGO, "Create HUDCanvas");
        return canvasGO;
    }

    // ================================================================
    // SECTION 4 — Datasheet Canvas
    // ================================================================

    private static GameObject CreateDatasheetCanvas(out DatasheetUIController dsCtrl)
    {
        var canvasGO = GetOrCreate("DatasheetCanvas");
        var canvas   = EnsureComponent<Canvas>(canvasGO);
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20; // above HUD
        EnsureComponent<CanvasScaler>(canvasGO);
        EnsureComponent<GraphicRaycaster>(canvasGO);

        // ── Panel ─────────────────────────────────────────────────────
        var panelGO  = GetOrCreateChild(canvasGO, "DatasheetPanel");
        var panelImg = EnsureComponent<Image>(panelGO);
        panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        var panelCG = EnsureComponent<CanvasGroup>(panelGO);
        panelCG.alpha          = 0f;
        panelCG.interactable   = false;
        panelCG.blocksRaycasts = false;
        SetRect(panelGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        dsCtrl = EnsureComponent<DatasheetUIController>(panelGO);

        // ── Content labels ────────────────────────────────────────────
        float yTop = -60f;
        float lineH = 44f;

        var nameGO = MakeLabel(panelGO, "TxtComponentName", "—",
                               24, FontStyles.Bold, yTop, lineH);
        yTop -= lineH + 6f;

        var typeGO = MakeLabel(panelGO, "TxtType", "—", 18,
                               FontStyles.Normal, yTop, lineH - 4f);
        yTop -= lineH;

        var valueGO = MakeLabel(panelGO, "TxtValue", "—", 18,
                                FontStyles.Normal, yTop, lineH - 4f);
        yTop -= lineH + 4f;

        var descGO = MakeLabel(panelGO, "TxtDescription", "—", 15,
                               FontStyles.Normal, yTop, lineH * 2f);
        var descTMP = descGO.GetComponent<TextMeshProUGUI>();
        descTMP.enableWordWrapping = true;
        yTop -= lineH * 2f + 4f;

        var detGO = MakeLabel(panelGO, "TxtDetails", "—", 13,
                              FontStyles.Normal, yTop, lineH - 4f);
        detGO.GetComponent<TextMeshProUGUI>().color = new Color(0.7f, 0.7f, 0.7f);

        // ── Datasheet URL button ──────────────────────────────────────
        var dsBtn = MakeButton(panelGO, "DatasheetButton",
                               "Open Datasheet ↗", new Color(0.1f, 0.4f, 0.9f),
                               new Vector2(0.05f, 0), new Vector2(0.55f, 0),
                               new Vector2(0, 60), new Vector2(0, 44));
        var txtUrl = dsBtn.GetComponentInChildren<TextMeshProUGUI>();

        // ── Close button ──────────────────────────────────────────────
        var closeBtn = MakeButton(panelGO, "CloseButton",
                                  "✕ Close", new Color(0.6f, 0.1f, 0.1f),
                                  new Vector2(0.6f, 0), new Vector2(0.95f, 0),
                                  new Vector2(0, 60), new Vector2(0, 44));

        // ── Wire DatasheetUIController ────────────────────────────────
        var so = new SerializedObject(dsCtrl);
        so.FindProperty("panelCanvasGroup").objectReferenceValue = panelCG;
        so.FindProperty("panelRoot").objectReferenceValue        = panelGO;
        so.FindProperty("txtComponentName").objectReferenceValue =
            nameGO.GetComponent<TextMeshProUGUI>();
        so.FindProperty("txtType").objectReferenceValue =
            typeGO.GetComponent<TextMeshProUGUI>();
        so.FindProperty("txtValue").objectReferenceValue =
            valueGO.GetComponent<TextMeshProUGUI>();
        so.FindProperty("txtDescription").objectReferenceValue = descTMP;
        so.FindProperty("txtDetails").objectReferenceValue =
            detGO.GetComponent<TextMeshProUGUI>();
        so.FindProperty("datasheetButton").objectReferenceValue =
            dsBtn.GetComponent<Button>();
        so.FindProperty("txtDatasheetUrl").objectReferenceValue = txtUrl;
        so.FindProperty("closeButton").objectReferenceValue =
            closeBtn.GetComponent<Button>();
        so.ApplyModifiedProperties();

        RegisterUndo(canvasGO, "Create DatasheetCanvas");
        return canvasGO;
    }

    // ================================================================
    // SECTION 5 — Cross-reference wiring
    // ================================================================

    private static void WireImageTarget(GameObject imageTarget)
    {
        // ComponentOverlayRenderer — componentLabelPrefab left unset;
        // user must assign the prefab from the Project window.
        // All other serialized floats use their script defaults.
        Debug.Log("[PCBARBootstrap] Assign the ComponentLabel prefab to " +
                  "ComponentOverlayRenderer on ImageTarget manually.");
    }

    private static void WireSignalPath(GameObject signalPath,
        Transform wpA, Transform wpB, Transform wpC)
    {
        var flow = signalPath.GetComponent<ParticleFlowController>();
        if (flow == null) return;

        var so = new SerializedObject(flow);
        var waysProp = so.FindProperty("waypoints");
        waysProp.arraySize = 3;
        waysProp.GetArrayElementAtIndex(0).objectReferenceValue = wpA;
        waysProp.GetArrayElementAtIndex(1).objectReferenceValue = wpB;
        waysProp.GetArrayElementAtIndex(2).objectReferenceValue = wpC;
        so.ApplyModifiedProperties();
    }

    private static void WireHUD(
        HUDController    hudCtrl,
        PDFExporter      pdfExp,
        TextMeshProUGUI  tmpCount,
        TextMeshProUGUI  tmpFps,
        Image            statusDot,
        Button           exportBtn,
        CanvasGroup      toastCG,
        TextMeshProUGUI  toastLabel)
    {
        // HUDController fields
        var soHud = new SerializedObject(hudCtrl);
        soHud.FindProperty("txtComponentCount").objectReferenceValue = tmpCount;
        soHud.FindProperty("txtFps").objectReferenceValue            = tmpFps;
        soHud.FindProperty("statusDot").objectReferenceValue         = statusDot;
        soHud.FindProperty("exportButton").objectReferenceValue      = exportBtn;
        soHud.FindProperty("pdfExporter").objectReferenceValue       = pdfExp;

        // txtAppTitle — find it from the hierarchy
        var titleTMP = hudCtrl.transform.Find("TopBar/TxtAppTitle")
                              ?.GetComponent<TextMeshProUGUI>();
        if (titleTMP != null)
            soHud.FindProperty("txtAppTitle").objectReferenceValue = titleTMP;

        soHud.ApplyModifiedProperties();

        // PDFExporter fields
        var soPdf = new SerializedObject(pdfExp);
        soPdf.FindProperty("toastCanvasGroup").objectReferenceValue = toastCG;
        soPdf.FindProperty("toastLabel").objectReferenceValue       = toastLabel;
        soPdf.ApplyModifiedProperties();
    }

    private static void WireARManager(
        GameObject       arManager,
        GameObject       imageTarget,
        TextMeshProUGUI  statusLabel)
    {
        var comp = arManager.GetComponent<ARSessionManager>();
        if (comp == null) return;

        var vuforia = imageTarget.GetComponent<VuforiaImageTargetManager>();

        var so = new SerializedObject(comp);
        so.FindProperty("statusLabel").objectReferenceValue      = statusLabel;
        so.FindProperty("imageTargetManager").objectReferenceValue = vuforia;
        so.ApplyModifiedProperties();
    }

    private static void WireUnityEvents(
        GameObject    imageTarget,
        HUDController hudCtrl)
    {
        var manager = imageTarget.GetComponent<VuforiaImageTargetManager>();
        if (manager == null || hudCtrl == null) return;

        // OnTargetFound → HUDController.OnTrackingFound
        UnityAction foundAction = hudCtrl.OnTrackingFound;
        UnityEventTools.AddPersistentListener(manager.OnTargetFound, foundAction);

        // OnTargetLost → HUDController.OnTrackingLost
        UnityAction lostAction = hudCtrl.OnTrackingLost;
        UnityEventTools.AddPersistentListener(manager.OnTargetLost, lostAction);

        EditorUtility.SetDirty(manager);
    }

    // ================================================================
    // SECTION 6 — UI helpers
    // ================================================================

    /// <summary>
    /// Creates a TMP label child anchored to the top of <paramref name="parent"/>.
    /// </summary>
    private static GameObject MakeLabel(
        GameObject parent, string name, string defaultText,
        float fontSize, FontStyles style,
        float yOffset, float height)
    {
        var go  = GetOrCreateChild(parent, name);
        var tmp = EnsureComponent<TextMeshProUGUI>(go);
        tmp.text      = defaultText;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.color     = Color.white;
        tmp.enableWordWrapping = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(24,   yOffset - height);
        rt.offsetMax = new Vector2(-24,  yOffset);

        return go;
    }

    /// <summary>Creates a Button with a TMP label inside it.</summary>
    private static GameObject MakeButton(
        GameObject parent, string name, string labelText,
        Color bgColor,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go  = GetOrCreateChild(parent, name);
        var img = EnsureComponent<Image>(go);
        img.color = bgColor;
        EnsureComponent<Button>(go);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot     = new Vector2(0.5f, 0);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var labelGO = GetOrCreateChild(go, "Label");
        var tmp     = EnsureComponent<TextMeshProUGUI>(labelGO);
        tmp.text      = labelText;
        tmp.fontSize  = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        SetRect(labelGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return go;
    }

    // ================================================================
    // SECTION 7 — Low-level utilities
    // ================================================================

    /// <summary>Returns an existing root GameObject by name or creates a new one.</summary>
    private static GameObject GetOrCreate(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    /// <summary>Returns a named child of <paramref name="parent"/> or creates it.</summary>
    private static GameObject GetOrCreateChild(GameObject parent, string childName)
    {
        var existing = parent.transform.Find(childName);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    /// <summary>
    /// Returns the existing component of type T or adds a new one.
    /// </summary>
    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var existing = go.GetComponent<T>();
        return existing != null ? existing : go.AddComponent<T>();
    }

    /// <summary>
    /// Sets a RectTransform using anchor + pixel offset values.
    /// <paramref name="offsetMin"/> = (left, bottom) offset from anchors in pixels.
    /// <paramref name="offsetMax"/> = (right, top) offset from anchors in pixels.
    /// </summary>
    private static void SetRect(
        GameObject go,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static void RegisterUndo(GameObject go, string label)
    {
        Undo.RegisterFullObjectHierarchyUndo(go, label);
    }
}

#pragma warning restore CS0649
