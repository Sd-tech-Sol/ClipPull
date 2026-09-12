using System.IO;
using System.Text.Json;
using ClipPull.Models;

namespace ClipPull.Services;

internal sealed class SettingsService
{
    private static readonly HashSet<string> Browsers =
        ["Chrome", "Edge", "Firefox", "Brave", "Chromium", "Opera", "Vivaldi"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _writeLock = new();
    private readonly string _directory;

    public SettingsService(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipPull");
    }

    public string SettingsPath => Path.Combine(_directory, "settings.json");

    public AppSettings Load()
    {
        var settings = new AppSettings();
        var hasLanguage = false;
        var hasTheme = false;

        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    hasLanguage = HasProperty(document.RootElement, "language");
                    hasTheme = HasProperty(document.RootElement, "theme");
                    settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? settings;
                }
            }
        }
        catch
        {
            // Corrupt, locked, or unreadable settings must never prevent startup.
            settings = new AppSettings();
        }

        if (!hasLanguage)
            settings.Language = ReadLegacyPreference("language-preference.txt") ?? settings.Language;
        if (!hasTheme)
            settings.Theme = ReadLegacyPreference("theme-preference.txt") ?? settings.Theme;

        Validate(settings);
        Save(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        string? temporaryPath = null;
        try
        {
            lock (_writeLock)
            {
                Validate(settings);
                Directory.CreateDirectory(_directory);
                temporaryPath = Path.Combine(_directory, $"settings.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
                File.Move(temporaryPath, SettingsPath, overwrite: true);
                temporaryPath = null;
            }
        }
        catch
        {
            // Settings are best effort; an unavailable profile must not break the app.
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); } catch { }
            }
        }
    }

    private string? ReadLegacyPreference(string fileName)
    {
        try
        {
            var path = Path.Combine(_directory, fileName);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasProperty(JsonElement element, string name) =>
        element.EnumerateObject().Any(property =>
            string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

    private static void Validate(AppSettings settings)
    {
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        settings.Language = string.Equals(settings.Language, "French", StringComparison.OrdinalIgnoreCase)
            ? "French"
            : "English";
        settings.Theme = settings.Theme.ToLowerInvariant() switch
        {
            "light" => "Light",
            "system" => "System",
            _ => "Dark"
        };

        if (string.IsNullOrWhiteSpace(settings.OutputFolder))
            settings.OutputFolder = AppSettings.DefaultOutputFolder;
        else
        {
            try { settings.OutputFolder = Path.GetFullPath(settings.OutputFolder.Trim()); }
            catch { settings.OutputFolder = AppSettings.DefaultOutputFolder; }
        }

        settings.FormatIndex = Math.Clamp(settings.FormatIndex, 0, 2);
        settings.QualityIndex = Math.Clamp(settings.QualityIndex, 0, 5);
        settings.PlaylistLimit = Math.Clamp(settings.PlaylistLimit, 1, 500);
        settings.SubtitleModeIndex = Math.Clamp(settings.SubtitleModeIndex, 0, 2);
        settings.SubtitleLanguageIndex = Math.Clamp(settings.SubtitleLanguageIndex, 0, 3);
        if (!Browsers.Contains(settings.SelectedBrowser))
            settings.SelectedBrowser = "Chrome";
        if (settings.LastUpdateCheckUtc > DateTimeOffset.UtcNow.AddDays(1))
            settings.LastUpdateCheckUtc = null;

        settings.Window ??= new WindowSettings();
        settings.Window.Width = ClampFinite(settings.Window.Width, 760, 3840, 920);
        settings.Window.Height = ClampFinite(settings.Window.Height, 620, 2160, 760);
    }

    private static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
