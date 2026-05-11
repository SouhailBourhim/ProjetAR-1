// =============================================================================
// ComponentData.cs
// Author:      [Your Name]
// Date:        2024-01-01
// Description: Serializable data model for a single PCB component and its
//              database container. Matches the JSON schema in
//              StreamingAssets/components_database.json.
// Dependencies: None (pure C# data model)
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the 2D/3D position of a component on the physical PCB,
/// stored as separate float fields for JsonUtility compatibility.
/// </summary>
[Serializable]
public class PCBPosition
{
    [Tooltip("X offset in metres relative to the ImageTarget centre.")]
    public float x;

    [Tooltip("Y offset in metres (normally 0 – flat PCB surface).")]
    public float y;

    [Tooltip("Z offset in metres relative to the ImageTarget centre.")]
    public float z;

    /// <summary>Converts the stored offsets to a Unity Vector3.</summary>
    public Vector3 ToVector3() => new Vector3(x, y, z);
}

/// <summary>
/// Full data record for one electronic component read from the JSON database.
/// All fields are serializable so JsonUtility can deserialize them directly.
/// </summary>
[Serializable]
public class ComponentData
{
    [Tooltip("Unique identifier on the PCB (e.g. R1, C2, U1).")]
    public string id;

    [Tooltip("Component category: resistor, capacitor, ic, inductor, led, mosfet …")]
    public string type;

    [Tooltip("Primary electrical value (e.g. 10kΩ, 100nF, ATmega328P).")]
    public string value;

    [Tooltip("Manufacturing tolerance (e.g. 5%, 1%, N/A).")]
    public string tolerance;

    [Tooltip("Physical footprint / package (e.g. 0805, TQFP-32, TO-220).")]
    public string package;

    [Tooltip("Human-readable description of the component's role on the PCB.")]
    public string description;

    [Tooltip("URL to the official component datasheet PDF.")]
    public string datasheet_url;

    [Tooltip("Maximum voltage rating (e.g. 150V, 5.5V, N/A).")]
    public string voltage_rating;

    [Tooltip("Hex colour string used to tint the AR overlay label (e.g. #FF6B35).")]
    public string color_hex;

    [Tooltip("3-axis position of the component on the PCB in metres.")]
    public PCBPosition position_on_pcb;

    // ------------------------------------------------------------------
    // Helper methods
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a compact single-line summary suitable for HUD or list views.
    /// Format: "R1 | resistor | 10kΩ | 0805"
    /// </summary>
    public string GetFormattedSummary()
    {
        return $"{id} | {type} | {value} | {package}";
    }
}

/// <summary>
/// Top-level wrapper that matches the JSON root object
/// { "components": [ ... ] } so JsonUtility can deserialize the whole file.
/// </summary>
[Serializable]
public class ComponentDatabase
{
    [Tooltip("List of all components in the database.")]
    public List<ComponentData> components;
}
