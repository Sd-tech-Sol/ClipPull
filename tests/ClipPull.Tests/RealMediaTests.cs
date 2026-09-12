using ClipPull.Models;
using ClipPull.Services;

namespace ClipPull.Tests;

public sealed class RealMediaTests
{
    private const string CombinedMediaUrl = "https://download.samplelib.com/mp4/sample-5s.mp4";
    private const string SeparateStreamsUrl = "https://www.youtube.com/watch?v=jNQXAC9IVRw";
    private const string UnavailableMediaUrl = "https://www.youtube.com/watch?v=BaW_jenozKc";

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

    private static DownloadSettings Settings(string? ffmpegDirectory) =>
        new(MediaMode.Video, VideoQuality.Auto, false, 50, false, null, ffmpegDirectory, null);

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
