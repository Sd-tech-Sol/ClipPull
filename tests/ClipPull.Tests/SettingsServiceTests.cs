using ClipPull.Services;

namespace ClipPull.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"ClipPull-tests-{Guid.NewGuid():N}");

    [Fact]
    public void MigratesLegacyLanguageAndThemeAndPersistsGeneralSettings()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "language-preference.txt"), "French");
        File.WriteAllText(Path.Combine(_directory, "theme-preference.txt"), "Light");
        var service = new SettingsService(_directory);

        var settings = service.Load();

        Assert.Equal("French", settings.Language);
        Assert.Equal("Light", settings.Theme);
        settings.FormatIndex = 2;
        settings.QualityIndex = 4;
        settings.OutputFolder = Path.Combine(_directory, "media");
        settings.UseHistory = false;
        settings.UseBrowserCookies = true;
        settings.SelectedBrowser = "Firefox";
        settings.PlaylistLimit = 27;
        settings.CheckForUpdates = false;
        settings.Window.Width = 1100;
        settings.Window.Height = 700;
        settings.Window.IsMaximized = true;
        service.Save(settings);

        var restored = service.Load();
        Assert.Equal(2, restored.FormatIndex);
        Assert.Equal(4, restored.QualityIndex);
        Assert.False(restored.UseHistory);
        Assert.True(restored.UseBrowserCookies);
        Assert.Equal("Firefox", restored.SelectedBrowser);
        Assert.Equal(27, restored.PlaylistLimit);
        Assert.False(restored.CheckForUpdates);
        Assert.Equal(1100, restored.Window.Width);
        Assert.Equal(700, restored.Window.Height);
        Assert.True(restored.Window.IsMaximized);
    }

    [Fact]
    public void CorruptJsonFallsBackSafelyAndRewritesValidSettings()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ definitely not json");
        var service = new SettingsService(_directory);

        var settings = service.Load();

        Assert.Equal("English", settings.Language);
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(1, settings.SchemaVersion);
        Assert.NotNull(service.Load());
    }

    [Fact]
    public void InvalidValuesAreClampedOrReplaced()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"),
            """{"schemaVersion":99,"language":"xx","theme":"pink","formatIndex":99,"qualityIndex":-4,"playlistLimit":9999,"selectedBrowser":"unknown","window":{"width":10,"height":99999,"isMaximized":true}}""");

        var settings = new SettingsService(_directory).Load();

        Assert.Equal(1, settings.SchemaVersion);
        Assert.Equal("English", settings.Language);
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(2, settings.FormatIndex);
        Assert.Equal(0, settings.QualityIndex);
        Assert.Equal(500, settings.PlaylistLimit);
        Assert.Equal("Chrome", settings.SelectedBrowser);
        Assert.Equal(760, settings.Window.Width);
        Assert.Equal(2160, settings.Window.Height);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
