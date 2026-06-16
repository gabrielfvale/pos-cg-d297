using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

public static class FigureLoader
{
    // Finds FIG1, FIG_1, FIG-1, FIG 1, fig1, etc. in Assets/PaperFigures/
    public static Sprite Load(string assetRef)
    {
        // Extract the number from the ref (e.g. "FIG1" → "1", "FIG_3" → "3")
        var match = Regex.Match(assetRef, @"\d+");
        if (!match.Success) return null;
        string num = match.Value;

        string folder = "Assets/PaperFigures";
        if (!Directory.Exists(folder)) return null;

        var files = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
        // Also check jpg
        var filesJpg = Directory.GetFiles(folder, "*.jpg", SearchOption.TopDirectoryOnly);

        var allFiles = new System.Collections.Generic.List<string>(files);
        allFiles.AddRange(filesJpg);

        // Pattern: FIG + any non-digit chars + num (case insensitive)
        var pattern = new Regex(@"fig\D*" + Regex.Escape(num) + @"\b", RegexOptions.IgnoreCase);

        foreach (var f in allFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(f);
            if (pattern.IsMatch(fileName))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(f.Replace("\\", "/"));
                if (sprite != null) return sprite;

                // Try loading as Texture2D and converting
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace("\\", "/"));
                if (tex != null)
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        return null;
    }
}
