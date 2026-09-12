using System.IO;

namespace ClipPull.Models;

internal sealed class AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Language { get; set; } = "English";
    public string Theme { get; set; } = "Dark";
    public string OutputFolder { get; set; } = DefaultOutputFolder;
    public int FormatIndex { get; set; }
    public int QualityIndex { get; set; }
    public bool UseHistory { get; set; } = true;
    public bool UseBrowserCookies { get; set; }
    public string SelectedBrowser { get; set; } = "Chrome";
    public int PlaylistLimit { get; set; } = 50;
    public bool CheckForUpdates { get; set; } = true;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public WindowSettings Window { get; set; } = new();

    // Added in schema v2. A settings.json from v1 has none of these properties, and
    // System.Text.Json leaves missing properties at their default, which already is
    // the required migration default: subtitles Off, Automatic language, fallback off.
    public int SubtitleModeIndex { get; set; }
    public int SubtitleLanguageIndex { get; set; }
    public bool UseAutomaticSubtitleFallback { get; set; }

    public static string DefaultOutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads", "ClipPull");
}

internal sealed class WindowSettings
{
    public double Width { get; set; } = 920;
    public double Height { get; set; } = 760;
    public bool IsMaximized { get; set; }
}
