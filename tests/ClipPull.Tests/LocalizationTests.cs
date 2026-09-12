using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ClipPull.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void EnglishAndFrenchResourceDictionariesHaveIdenticalKeys()
    {
        var english = ExtractKeys(ResourcePath("Strings.en.xaml"));
        var french = ExtractKeys(ResourcePath("Strings.fr.xaml"));

        Assert.NotEmpty(english);
        Assert.Equal(english, french);
    }

    [Fact]
    public void NeitherDictionaryDeclaresADuplicateKey()
    {
        AssertNoDuplicateKeys(ResourcePath("Strings.en.xaml"));
        AssertNoDuplicateKeys(ResourcePath("Strings.fr.xaml"));
    }

    [Theory]
    [InlineData("Settings.Subtitles")]
    [InlineData("Subtitles.ModeLabel")]
    [InlineData("Subtitles.ModeOff")]
    [InlineData("Subtitles.ModeWithMedia")]
    [InlineData("Subtitles.ModeSubtitlesOnly")]
    [InlineData("Subtitles.LanguageLabel")]
    [InlineData("Subtitles.LanguageAutomatic")]
    [InlineData("Subtitles.LanguageEnglish")]
    [InlineData("Subtitles.LanguageFrench")]
    [InlineData("Subtitles.LanguageAll")]
    [InlineData("Subtitles.UseAutoFallback")]
    [InlineData("Dialog.SubtitleFfmpegTitle")]
    [InlineData("Dialog.SubtitleFfmpegMessage")]
    [InlineData("Status.DownloadingSubtitles")]
    [InlineData("Status.SubtitlesSaved")]
    [InlineData("Status.NoSubtitlesAvailable")]
    [InlineData("Status.NoSubtitlesAvailableLanguage")]
    [InlineData("Status.SubtitleConversionRequiresFfmpeg")]
    [InlineData("Service.NoSubtitlesAvailable")]
    [InlineData("Service.NoSubtitlesAvailableLanguage")]
    public void RequiredSubtitleKeyExistsInBothLanguages(string key)
    {
        Assert.Contains(key, ExtractKeys(ResourcePath("Strings.en.xaml")));
        Assert.Contains(key, ExtractKeys(ResourcePath("Strings.fr.xaml")));
    }

    private static void AssertNoDuplicateKeys(string path)
    {
        var keys = ExtractKeysInOrder(path);
        var distinct = new HashSet<string>(keys, StringComparer.Ordinal);
        Assert.Equal(keys.Count, distinct.Count);
    }

    private static SortedSet<string> ExtractKeys(string path) =>
        new(ExtractKeysInOrder(path), StringComparer.Ordinal);

    private static List<string> ExtractKeysInOrder(string path)
    {
        var xml = File.ReadAllText(path);
        return Regex.Matches(xml, "x:Key=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToList();
    }

    private static string ResourcePath(string fileName, [CallerFilePath] string callerFilePath = "") =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(callerFilePath)!, "..", "..", "src", "ClipPull", "Resources", fileName));
}
