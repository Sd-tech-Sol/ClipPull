namespace ClipPull.Models;

internal enum MediaMode
{
    Video,
    AudioM4a,
    AudioMp3
}

internal enum VideoQuality
{
    Auto,
    Best,
    P1080,
    P720,
    P480,
    Small
}

internal sealed record DownloadSettings(
    MediaMode Mode,
    VideoQuality Quality,
    bool AllowPlaylists,
    int PlaylistLimit,
    bool UseHistory,
    string? Browser,
    string? FfmpegDirectory,
    string? ArchivePath);

internal sealed record DownloadResult(IReadOnlyList<string> Files);

internal sealed record MediaPreview(
    string Title,
    string Platform,
    string? ThumbnailUrl,
    double? DurationSeconds,
    int? PlaylistCount);

internal sealed record DownloadError(string Url, string Platform, string Message);
