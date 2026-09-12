using ClipPull.Localization;
using ClipPull.Models;
using ClipPull.Services;

namespace ClipPull.Tests;

public sealed class RealMediaTests
{
    private const string CombinedMediaUrl = "https://download.samplelib.com/mp4/sample-5s.mp4";
    private const string SeparateStreamsUrl = "https://www.youtube.com/watch?v=jNQXAC9IVRw";
    private const string UnavailableMediaUrl = "https://www.youtube.com/watch?v=BaW_jenozKc";

    // "Me at the zoo" (same fixture as SeparateStreamsUrl) has manually authored en/de
    // subtitles, and YouTube serves the manual track directly as SRT (no conversion).
    private const string ManualSubtitleMediaUrl = SeparateStreamsUrl;

    // Public domain, no dialogue: neither manual nor auto-generated subtitles exist.
    private const string NoSubtitleMediaUrl = "https://www.youtube.com/watch?v=aqz-KE-bpKQ";

    // No manually authored English subtitle, and the auto-generated English caption is
    // only ever offered as VTT (no native SRT option) -- the right fixture for exercising
    // both the auto-caption fallback path and VTT-to-SRT conversion.
    private const string AutoOnlySubtitleMediaUrl = "https://www.youtube.com/watch?v=OPf0YbXqDm0";

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task SmallCombinedStreamUsesFastPhaseWithoutFfmpeg()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            var progress = new List<DownloadProgress>();
            var settings = Settings(ffmpegDirectory: null);
            var result = await new MediaService().DownloadAsync(
                YtDlpPath(), CombinedMediaUrl, directory, settings,
                new InlineProgress<DownloadProgress>(progress.Add), null, CancellationToken.None);

            Assert.NotEmpty(result.Files);
            Assert.All(result.Files, file => Assert.True(File.Exists(file)));
            Assert.Contains(progress, value => value.BytesPerSecond > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task SmallSeparateStreamMediaTriggersThenCompletesFallback()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            var service = new MediaService();
            var progress = new List<DownloadProgress>();
            var firstPhase = await Assert.ThrowsAsync<RequestedFormatUnavailableException>(() =>
                service.DownloadAsync(YtDlpPath(), SeparateStreamsUrl, directory, Settings(null),
                    new InlineProgress<DownloadProgress>(progress.Add), null, CancellationToken.None));
            Assert.Empty(firstPhase.PartialFiles);

            var result = await service.DownloadAsync(
                YtDlpPath(), SeparateStreamsUrl, directory, Settings(FfmpegDirectory()),
                new InlineProgress<DownloadProgress>(progress.Add), null, CancellationToken.None,
                useAutoFallback: true);

            Assert.Single(result.Files);
            Assert.EndsWith(".mp4", result.Files[0], StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.Files[0]));
            Assert.Contains(progress, value => value.BytesPerSecond > 0);
            Assert.Contains(progress, value => value.Eta is not null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task UnavailableMediaDoesNotTriggerFormatFallback()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                new MediaService().DownloadAsync(
                    YtDlpPath(), UnavailableMediaUrl, directory, Settings(null),
                    null, null, CancellationToken.None));

            Assert.IsNotType<RequestedFormatUnavailableException>(error);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task SubtitlesOnlyManualEnglishProducesNativeSrtWithoutDownloadingMedia()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            var settings = SubtitleSettings(SubtitleMode.SubtitlesOnly, SubtitleLanguagePreference.English);
            var result = await new MediaService().DownloadAsync(
                YtDlpPath(), ManualSubtitleMediaUrl, directory, settings, null, null, CancellationToken.None);

            Assert.Empty(result.Files);
            Assert.NotEmpty(result.SubtitleFiles);
            Assert.All(result.SubtitleFiles, file => Assert.True(File.Exists(file)));
            Assert.All(result.SubtitleFiles, file => Assert.EndsWith(".en.srt", file, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(Directory.EnumerateFiles(directory), file =>
                file.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".webm", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task SubtitlesOnlyManualEnglishSucceedsAsNativeSrtWithoutRequestingFfmpegConversion()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            // ConvertSubtitlesToSrt is left off: yt-dlp's own --sub-format "srt/best"
            // must produce SRT directly for a manually authored YouTube track, with no
            // FFmpeg involvement of any kind.
            var settings = SubtitleSettings(SubtitleMode.SubtitlesOnly, SubtitleLanguagePreference.English, useConversion: false);
            var result = await new MediaService().DownloadAsync(
                YtDlpPath(), ManualSubtitleMediaUrl, directory, settings, null, null, CancellationToken.None);

            Assert.NotEmpty(result.SubtitleFiles);
            Assert.All(result.SubtitleFiles, file => Assert.True(File.Exists(file)));
            Assert.All(result.SubtitleFiles, file => Assert.EndsWith(".srt", file, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task SubtitlesOnlyWithNoSubtitlesAvailableFailsClearly()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            var settings = SubtitleSettings(SubtitleMode.SubtitlesOnly, SubtitleLanguagePreference.English);
            var error = await Assert.ThrowsAsync<LocalizedException>(() =>
                new MediaService().DownloadAsync(
                    YtDlpPath(), NoSubtitleMediaUrl, directory, settings, null, null, CancellationToken.None));

            Assert.Contains("subtitle", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task AutomaticFallbackProducesAutoGeneratedSubtitleWhenNoManualTrackExists()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            var settings = SubtitleSettings(
                SubtitleMode.SubtitlesOnly, SubtitleLanguagePreference.English, useAutomaticFallback: true);
            var result = await new MediaService().DownloadAsync(
                YtDlpPath(), AutoOnlySubtitleMediaUrl, directory, settings, null, null, CancellationToken.None);

            Assert.NotEmpty(result.SubtitleFiles);
            Assert.All(result.SubtitleFiles, file => Assert.True(File.Exists(file)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task VttSubtitleConvertsToSrtUsingTheAlreadyInstalledFfmpegAfterDownload()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            // No FFmpeg conversion requested from yt-dlp itself: the auto-generated
            // caption comes back as native VTT, exactly like the deferred-conversion
            // flow leaves it before ClipPull decides whether to convert it locally.
            var settings = SubtitleSettings(
                SubtitleMode.SubtitlesOnly, SubtitleLanguagePreference.English,
                useAutomaticFallback: true, useConversion: false);
            var result = await new MediaService().DownloadAsync(
                YtDlpPath(), AutoOnlySubtitleMediaUrl, directory, settings, null, null, CancellationToken.None);

            Assert.NotEmpty(result.SubtitleFiles);
            // Different auto-caption variants of the same video can offer different
            // format lists; only the non-SRT ones exercise the conversion step, which
            // mirrors exactly what FinalizeSubtitlesAsync does per file in production.
            var needingConversion = result.SubtitleFiles
                .Where(file => !file.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.NotEmpty(needingConversion);

            var ffmpegDirectory = FfmpegDirectory();
            foreach (var vtt in needingConversion)
            {
                var converted = await MediaService.ConvertSubtitleToSrtAsync(ffmpegDirectory, vtt, CancellationToken.None);

                Assert.NotNull(converted);
                Assert.EndsWith(".srt", converted, StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(converted));
                Assert.False(File.Exists(vtt), "the original VTT should be removed once the SRT conversion is verified");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task WithMediaSubtitleUnavailableDoesNotFailTheMediaDownload()
    {
        if (!ShouldRun())
            return;

        var directory = NewOutputDirectory();
        try
        {
            // The generic (non-YouTube) extractor has no subtitle metadata at all, so
            // this exercises "media succeeds, subtitle silently unavailable" without
            // any extra network cost over the existing fast-path fixture.
            var settings = SubtitleSettings(SubtitleMode.WithMedia, SubtitleLanguagePreference.English);
            var result = await new MediaService().DownloadAsync(
                YtDlpPath(), CombinedMediaUrl, directory, settings, null, null, CancellationToken.None);

            Assert.NotEmpty(result.Files);
            Assert.Empty(result.SubtitleFiles);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DownloadSettings Settings(string? ffmpegDirectory) =>
        new(MediaMode.Video, VideoQuality.Auto, false, 50, false, null, ffmpegDirectory, null);

    private static DownloadSettings SubtitleSettings(
        SubtitleMode subtitleMode,
        SubtitleLanguagePreference subtitleLanguage,
        bool useAutomaticFallback = false,
        bool useConversion = true) =>
        new(MediaMode.Video, VideoQuality.Auto, false, 50, false, null, useConversion ? FfmpegDirectory() : null, null,
            subtitleMode, subtitleLanguage, useAutomaticFallback, ConvertSubtitlesToSrt: useConversion);

    private static bool ShouldRun() =>
        string.Equals(Environment.GetEnvironmentVariable("CLIPPULL_RUN_MEDIA_TESTS"), "1", StringComparison.Ordinal);

    private static string YtDlpPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipPull", "bin", "yt-dlp.exe");

    private static string FfmpegDirectory()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipPull", "ffmpeg");
        var version = File.ReadAllText(Path.Combine(baseDirectory, "active-version.txt")).Trim();
        return Path.Combine(baseDirectory, version);
    }

    private static string NewOutputDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ClipPull-media-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
