namespace ClipPull.ViewModels;

/// <summary>Language-neutral subtitle outcome for one queue item; the corresponding
/// display text is resolved from this at read time so it relocalizes automatically
/// on a runtime language switch.</summary>
internal enum SubtitleResultState
{
    None,
    Saved,
    NoSubtitlesAvailable,
    NoSubtitlesAvailableLanguage,
    ConversionRequiresFfmpeg
}
