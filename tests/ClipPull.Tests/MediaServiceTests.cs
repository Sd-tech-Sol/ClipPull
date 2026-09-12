using System.Diagnostics;
using ClipPull.Models;
using ClipPull.Services;

namespace ClipPull.Tests;

public sealed class MediaServiceTests
{
    [Fact]
    public void ProgressParserUsesInvariantMachineFields()
    {
        var parsed = MediaService.TryParseProgress(
            "CLIPPULL_PROGRESS|5242880|10485760|13002342.500|18", out var progress);

        Assert.True(parsed);
        Assert.Equal(50, progress.Percent);
        Assert.Equal(13002342.5, progress.BytesPerSecond);
        Assert.Equal(TimeSpan.FromSeconds(18), progress.Eta);
    }

    [Theory]
    [InlineData("CLIPPULL_PROGRESS|12|0|0.000|-1")]
    [InlineData("unrelated yt-dlp output")]
    public void UnknownOrUnrelatedProgressDoesNotThrow(string line)
    {
        var parsed = MediaService.TryParseProgress(line, out var progress);

        if (line.StartsWith("CLIPPULL", StringComparison.Ordinal))
        {
            Assert.True(parsed);
            Assert.Null(progress.BytesPerSecond);
            Assert.Null(progress.Eta);
        }
        else
        {
            Assert.False(parsed);
        }
    }

    [Fact]
    public void FormatFallbackClassifierRequiresExactYtDlpError()
    {
        Assert.True(MediaService.IsRequestedFormatUnavailable([
            "ERROR: [youtube] abc: Requested format is not available. Use --list-formats for a list of available formats"]));
        Assert.False(MediaService.IsRequestedFormatUnavailable(["ERROR: HTTP Error 403: Forbidden"]));
        Assert.False(MediaService.IsRequestedFormatUnavailable(["ERROR: This video is private"]));
        Assert.False(MediaService.IsRequestedFormatUnavailable([
            "WARNING: Only images are available for download",
            "ERROR: [youtube] abc: Requested format is not available. Use --list-formats for a list of available formats"]));
    }

    [Fact]
    public void AutoSelectorsKeepCombinedFirstAndSeparateStreamsForFallback()
    {
        var settings = new DownloadSettings(MediaMode.Video, VideoQuality.Auto, false, 50, true, null, null, null);
        var combined = new ProcessStartInfo();
        var fallback = new ProcessStartInfo();

        MediaService.ConfigureFormat(combined, settings, useAutoFallback: false);
        MediaService.ConfigureFormat(fallback, settings, useAutoFallback: true);

        Assert.Contains("best[ext=mp4]/best", combined.ArgumentList);
        Assert.DoesNotContain("--merge-output-format", combined.ArgumentList);
        Assert.Contains("bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio", fallback.ArgumentList);
        Assert.Contains("--merge-output-format", fallback.ArgumentList);
        Assert.Contains("mp4/mkv", fallback.ArgumentList);
    }
}
