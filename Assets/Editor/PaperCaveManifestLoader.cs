using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reads a manifest.json produced by the Paper Cave Python pipeline
/// (Assets/PaperCaveData/{paper_id}/manifest.json) and builds the cards
/// deterministically in the current scene using PaperCaveSceneBuilder3D.
///
/// Run via menu: Tools > PaperCave > Build Cards From Manifest...
/// </summary>
public static class PaperCaveManifestLoader
{
    // Default folder relative to project root — created by unity_export.py
    const string DefaultDataFolder = "Assets/PaperCaveData";

    [MenuItem("Tools/PaperCave/Build Cards From Manifest...")]
    public static void BuildFromManifest()
    {
        string manifestPath = EditorUtility.OpenFilePanel(
            "Select Paper Cave manifest.json",
            Path.Combine(Application.dataPath, "../" + DefaultDataFolder),
            "json"
        );

        if (string.IsNullOrEmpty(manifestPath)) return;

        PaperManifestData manifest;
        try
        {
            string json = File.ReadAllText(manifestPath);
            manifest = JsonConvert.DeserializeObject<PaperManifestData>(json);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog(
                "PaperCave Manifest Error",
                $"Could not parse manifest.json:\n{e.Message}",
                "OK"
            );
            return;
        }

        if (manifest == null || manifest.units == null || manifest.units.Count == 0)
        {
            EditorUtility.DisplayDialog("PaperCave", "Manifest has no units.", "OK");
            return;
        }

        // Derive the paper_id folder so image lookups work.
        // manifest.json lives at  .../PaperCaveData/{paper_id}/manifest.json
        string paperId = Path.GetFileName(Path.GetDirectoryName(manifestPath));

        BuildUnitsFromManifest(manifest, paperId);

        EditorUtility.DisplayDialog(
            "PaperCave",
            $"Built {manifest.units.Count} unit(s) from '{manifest.paperTitle}'.",
            "OK"
        );
    }

    /// <summary>
    /// Programmatic entry point — builds all units from a loaded manifest.
    /// </summary>
    public static void BuildUnitsFromManifest(PaperManifestData manifest, string paperId)
    {
        // Arc positions for up to 8 units (primary always centre).
        Vector3[] positions =
        {
            new Vector3( 0.0f,  0.0f,  0.0f),
            new Vector3(-2.0f, -0.3f,  0.2f),
            new Vector3( 2.0f, -0.3f,  0.2f),
            new Vector3(-1.0f, -1.8f,  0.3f),
            new Vector3( 1.0f, -1.8f,  0.3f),
            new Vector3(-3.0f, -0.6f,  0.4f),
            new Vector3( 3.0f, -0.6f,  0.4f),
            new Vector3( 0.0f, -2.4f,  0.5f),
        };
        float[] rotations = { 0f, 12f, -12f, 7f, -7f, 20f, -20f, 0f };

        // Primary unit comes first regardless of JSON order.
        var ordered = new List<PaperUnitData>();
        var primary = manifest.units.Find(u => u.priority == "primary");
        if (primary != null) ordered.Add(primary);
        foreach (var u in manifest.units)
            if (u.priority != "primary") ordered.Add(u);

        for (int i = 0; i < ordered.Count && i < positions.Length; i++)
        {
            float w = (i == 0) ? 1.6f : 1.3f;
            float h = (i == 0) ? 2.2f : 1.8f;
            PaperCaveSceneBuilder3D.BuildCardFromData(ordered[i], paperId, positions[i], rotations[i], w, h);
        }
    }
}

// ── JSON data model (mirrors Python UnitManifest / flat card list) ─────────────

[Serializable]
public class PaperManifestData
{
    public string paperTitle;
    public string centralContribution;
    public int unitCount;
    public List<PaperUnitData> units;
}

[Serializable]
public class PaperUnitData
{
    public string id;
    public string type;             // "card" (stacks already flattened by unity_export.py)
    public string priority;         // "primary" | "secondary"
    public string title;
    public string category;
    public string summary;
    public string contentType;      // "figure" | "chart" | "table" | "animation" | "text_panel"
    public PaperCardContent content;
    public string conceptualOrigin;
    public string whyThisUnit;
    public PaperStyleHint styleHint;
}

[Serializable]
public class PaperCardContent
{
    public string description;
    public string assetReference;
    public string caption;
    public string chartType;
    public string title;
    public object data;             // chart/table data — used as raw JSON object
    public int frameCount;
    public List<PaperAnimFrame> frames;
    public string transitionType;
    public bool looping;
}

[Serializable]
public class PaperAnimFrame
{
    public int index;
    public string label;
    public string description;
    public string assetReference;
}

[Serializable]
public class PaperStyleHint
{
    public string categoryColor;
    public string colorName;
}
