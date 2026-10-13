using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Glowbo;

internal sealed class GlowboSettings
{
    public string BackgroundColor { get; set; } = "#000000";
    public string FaceColor { get; set; } = "#FFFFFF";
    public string SelectedFace { get; set; } = "Happy";
    public bool AutoChange { get; set; } = true;
    public int ChangeSeconds { get; set; } = 12;
    public int IdleMinutes { get; set; } = 5;
    public bool MultiMonitor { get; set; } = true;
    /// <summary>When true, the PC locks (password/PIN required) as soon as the screensaver is dismissed.</summary>
    public bool SecureLock { get; set; }
    /// <summary>Show the time and date in the bottom-right corner of the screensaver.</summary>
    public bool ShowClock { get; set; }
    /// <summary>HTML color for the clock; empty means "same as the face".</summary>
    public string ClockColor { get; set; } = "";
    /// <summary>The user declined the first-run "Install Glowbo on this PC?" offer.</summary>
    public bool SkipInstallPrompt { get; set; }
    public List<string> Playlist { get; set; } = new() { "Happy", "Blinking", "Shy", "Sleepy", "Angry", "Love", "Surprised" };

    /// <summary>Per-expression face color overrides (expression name → HTML color). Missing entries use <see cref="FaceColor"/>.</summary>
    public Dictionary<string, string> FaceColors { get; set; } = new();

    public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glowbo");
    private static string PathName => Path.Combine(DataDir, "settings.json");
    // Settings file used before the app was renamed to Glowbo.
    private static string LegacyPathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DasaiMochi", "settings.json");

    public static GlowboSettings Load()
    {
        try
        {
            string path = File.Exists(PathName) ? PathName : LegacyPathName;
            if (File.Exists(path)) return JsonSerializer.Deserialize<GlowboSettings>(File.ReadAllText(path)) ?? new();
        }
        catch { }
        return new();
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        File.WriteAllText(PathName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
    [JsonIgnore] public Color Background => Parse(BackgroundColor, Color.Black);
    [JsonIgnore] public Color Face => Parse(FaceColor, Color.White);
    /// <summary>Expression to show at a given animation frame, following the rotation settings.</summary>
    public string ExpressionAt(int frame, int frameMs)
    {
        if (!AutoChange || Playlist.Count == 0) return SelectedFace;
        int framesPerFace = Math.Max(1, ChangeSeconds * 1000 / frameMs);
        return Playlist[frame / framesPerFace % Playlist.Count];
    }
    public Color ClockFor(Color face) => string.IsNullOrEmpty(ClockColor) ? face : Parse(ClockColor, face);
    public Color FaceFor(string expression) => FaceColors.TryGetValue(expression, out var hex) ? Parse(hex, Face) : Face;
    private static Color Parse(string hex, Color fallback) { try { return ColorTranslator.FromHtml(hex); } catch { return fallback; } }
}
