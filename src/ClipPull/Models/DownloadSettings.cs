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

internal enum SubtitleMode
{
    Off,
    WithMedia,
    SubtitlesOnly
}

internal enum SubtitleLanguagePreference
{
    Automatic,
    English,
    French,
    All
}

internal sealed record DownloadSettings(
    MediaMode Mode,
    VideoQuality Quality,
    bool AllowPlaylists,
    int PlaylistLimit,
    bool UseHistory,
    string? Browser,
    string? FfmpegDirectory,
    string? ArchivePath,
    SubtitleMode SubtitleMode = SubtitleMode.Off,
    SubtitleLanguagePreference SubtitleLanguage = SubtitleLanguagePreference.Automatic,
    bool UseAutomaticSubtitleFallback = false,
    bool ConvertSubtitlesToSrt = false);

internal sealed record DownloadResult(IReadOnlyList<string> Files, IReadOnlyList<string> SubtitleFiles);

internal sealed record MediaPreview(
    string Title,
    string Platform,
    string? ThumbnailUrl,
    double? DurationSeconds,
    int? PlaylistCount);

internal sealed record DownloadError(string Url, string Platform, string Message);
