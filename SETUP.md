# PCB-AR Viewer — Unity Editor Setup Guide

Follow these steps **in order** after importing all scripts into your Unity 2022 LTS project.

---

## Prerequisites

| Package | Version | How to install |
|---|---|---|
| AR Foundation | 5.x | Package Manager → Unity Registry |
| Apple ARKit XR Plugin | 5.x | Package Manager → Unity Registry |
| Vuforia Engine | 10.x (Community) | Vuforia Developer Portal → `.unitypackage` |
| TextMeshPro | (bundled with Unity) | Window → TextMeshPro → Import TMP Essentials |

---

## Step 1 — Project Settings (iOS)

1. **Edit → Project Settings → Player**
   - Platform: iOS
   - Bundle Identifier: `com.yourcompany.pcbar`
   - Target minimum iOS version: **14.0**
   - Architecture: **ARM64**
   - Camera Usage Description: `"Required for AR tracking"`

2. **Edit → Project Settings → XR Plug-in Management → iOS**
   - Enable **ARKit**

3. **Edit → Project Settings → Player → Other Settings**
   - Scripting Backend: **IL2CPP**
   - Api Compatibility Level: **.NET Standard 2.1**

---

## Step 2 — Scene Hierarchy

Create the following GameObjects in your main scene (right-click Hierarchy → Create Empty):

```
Scene
├── ARManager                   ← ARSessionManager.cs
├── InputManager                ← ComponentTapHandler.cs
├── DatabaseManager             ← ComponentDatabaseLoader.cs
│
├── AR Session Origin           ← (AR Foundation prefab from Package Manager)
│   └── AR Camera               ← (child of Session Origin)
│
├── AR Session                  ← (AR Foundation prefab)
│
└── ImageTarget                 ← (Vuforia ImageTargetBehaviour)
    ├── VuforiaImageTargetManager.cs
    ├── ComponentOverlayRenderer.cs
    └── [Label prefabs spawned at runtime]
```

---

## Step 3 — Component Label Prefab

1. Create a new **empty GameObject** → name it `ComponentLabelPrefab`.
2. Add a **Canvas** component (render mode: **World Space**, scale: ~0.001).
3. Inside the Canvas add:
   - **Image** (background) — assign to `ComponentInfoDisplay.backgroundImage`
   - **TextMeshPro - Text (UI)** × 4 (id, type, value, description)
   - Add a **CanvasGroup** component on the root for fade support.
   - Add a **BoxCollider** (size ~0.05 × 0.02 × 0.001) so raycasts can hit it.
4. Add **ComponentInfoDisplay.cs** to the root.
5. Wire the four TMP fields and the background Image in the Inspector.
6. Save as a prefab in `Assets/Prefabs/`.

---

## Step 4 — ARManager GameObject

Add to the **ARManager** empty GameObject:

| Script | Required fields to assign |
|---|---|
| `ARSessionManager` | `statusLabel` → TMP label in HUD; `imageTargetManager` → ImageTarget's `VuforiaImageTargetManager` |

---

## Step 5 — ImageTarget GameObject

Add to the **ImageTarget** GameObject (which already has `ImageTargetBehaviour`):

| Script | Required fields to assign |
|---|---|
| `VuforiaImageTargetManager` | `overlayRenderer` → `ComponentOverlayRenderer` on same GO |
| `ComponentOverlayRenderer` | `componentLabelPrefab` → the prefab from Step 3 |

---

## Step 6 — InputManager GameObject

| Script | Required fields to assign |
|---|---|
| `ComponentTapHandler` | `labelLayerMask` → set to the layer your label colliders are on |

---

## Step 7 — Datasheet UI Canvas

1. Create a **Canvas** (Screen Space – Overlay) → name it `DatasheetCanvas`.
2. Add a child **Panel** with a **CanvasGroup**.
3. Inside the Panel add:
   - TMP labels: component name, type, value, description, details, URL
   - **Button** → "Open Datasheet" (opens URL)
   - **Button** → "Close"
4. Add **DatasheetUIController.cs** to the Panel root.
5. Wire all TMP fields, buttons, and the `panelRoot` / `panelCanvasGroup` in the Inspector.

---

## Step 8 — HUD Canvas

1. Create a **Canvas** (Screen Space – Overlay) → name it `HUDCanvas`.
2. Add a semi-transparent top bar (Image, alpha ~0.7, black).
3. Inside add:
   - TMP label: app title
   - TMP label: component count
   - TMP label: FPS (bottom corner)
   - **Image** (small circle): tracking status dot
   - **Button**: Export
4. Add **HUDController.cs** to the Canvas root.
5. Wire all fields and `pdfExporter`.
6. In **VuforiaImageTargetManager** → Inspector → `OnTargetFound` → add `HUDController.OnTrackingFound`; `OnTargetLost` → `HUDController.OnTrackingLost`.

---

## Step 9 — Toast for PDFExporter

1. Inside **HUDCanvas** (or a separate overlay Canvas) create a small Panel for the toast.
2. Add a **CanvasGroup** + TMP label inside it.
3. Add **PDFExporter.cs** to any persistent GameObject (e.g. HUDCanvas root).
4. Wire `toastCanvasGroup` and `toastLabel` in the Inspector.
5. Assign the `PDFExporter` reference in **HUDController**.

---

## Step 10 — ParticleFlowController (optional signal path)

1. On the **ImageTarget**, create child empty GameObjects for each waypoint, e.g.:
   `Waypoint_A`, `Waypoint_B`, `Waypoint_C` — position them along the PCB trace.
2. Add a new empty GameObject **SignalPath** as a child of ImageTarget.
3. Attach **ParticleFlowController.cs**.
4. Drag the waypoint Transforms into the `waypoints` list in the Inspector.
5. Toggle `isFault` at runtime to switch between green/red particles.

---

## Step 11 — iOS Native Plugin Stub (PDF share sheet)

To enable the iOS share sheet in `PDFExporter`:

1. In `Assets/Plugins/iOS/` create `SharePlugin.mm`:

```objc
#import <UIKit/UIKit.h>
#import "UnityAppController.h"

extern "C" {
    void ShareFile(const char* path) {
        NSString* filePath = [NSString stringWithUTF8String:path];
        NSURL* fileURL = [NSURL fileURLWithPath:filePath];
        UIActivityViewController* ac =
            [[UIActivityViewController alloc]
                initWithActivityItems:@[fileURL]
                applicationActivities:nil];
        [UnityGetGLViewController() presentViewController:ac
                                                 animated:YES
                                               completion:nil];
    }
}
```

2. Unity will automatically include `.mm` files from `Assets/Plugins/iOS/` in the Xcode project.

---

## Step 12 — Vuforia Database

1. Log in to [developer.vuforia.com](https://developer.vuforia.com).
2. Create a **Target Database** → upload a high-contrast image of your PCB.
3. Download the database as a **Unity Editor** package and import it.
4. On the **ImageTarget** component select your database and target name.

---

## Step 13 — Build Checklist

- [ ] iOS platform selected in Build Settings
- [ ] Scene added to Build Settings scenes list
- [ ] Vuforia licence key entered in **Vuforia Configuration** (Window → Vuforia Configuration)
- [ ] `NSCameraUsageDescription` set in Player Settings
- [ ] `components_database.json` present in `Assets/StreamingAssets/`
- [ ] IL2CPP + ARM64 build settings confirmed
- [ ] Xcode project opens without errors after Unity build
- [ ] Code-signed with a valid Apple Developer provisioning profile targeting an ARKit-capable device (iPhone 6s or later, iOS 14+)

---

## Dependency Order (compile order reference)

```
ComponentData.cs
    └── ComponentDatabaseLoader.cs
            └── ComponentOverlayRenderer.cs
                    └── ComponentInfoDisplay.cs
                            └── ComponentTapHandler.cs
ARSessionManager.cs
    └── VuforiaImageTargetManager.cs
DatasheetUIController.cs
HUDController.cs
    └── PDFExporter.cs
ParticleFlowController.cs  (standalone)
```
