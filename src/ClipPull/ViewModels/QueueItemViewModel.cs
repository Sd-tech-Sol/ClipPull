using CommunityToolkit.Mvvm.ComponentModel;
using ClipPull.Localization;
using ClipPull.Models;

namespace ClipPull.ViewModels;

internal sealed partial class QueueItemViewModel : ObservableObject
{
    public QueueItemViewModel(string url, string platform, string displayText, int formatIndex, int qualityIndex, int subtitleModeIndex = 0)
    {
        Url = url;
        Platform = platform;
        DisplayText = displayText;
        FormatIndex = formatIndex;
        QualityIndex = qualityIndex;
        SubtitleModeIndex = subtitleModeIndex;
    }

    public string Url { get; }

    public string Platform { get; }

    /// <summary>Shortened URL, replaced with the resulting file name(s) once known.</summary>
    [ObservableProperty]
    private string _displayText;

    public int FormatIndex { get; }

    public int QualityIndex { get; }

    public int SubtitleModeIndex { get; }

    public string OptionsText
    {
        get
        {
            var format = LocalizationService.Get(FormatIndex switch
            {
                1 => "Options.FormatM4A",
                2 => "Options.FormatMP3",
                _ => "Options.FormatVideo"
            });

            string baseText;
            if (FormatIndex != 0)
            {
                baseText = $"{Platform}  ·  {format}";
            }
            else
            {
                var quality = LocalizationService.Get(QualityIndex switch
                {
                    1 => "Options.QualityBest",
                    2 => "Options.Quality1080",
                    3 => "Options.Quality720",
                    4 => "Options.Quality480",
                    5 => "Options.QualitySmall",
                    _ => "Options.QualityAuto"
                });
                baseText = $"{Platform}  ·  {format}  ·  {quality}";
            }

            return SubtitleModeIndex switch
            {
                1 => $"{baseText}  ·  {LocalizationService.Get("Subtitles.ModeWithMedia")}",
                2 => LocalizationService.Get("Subtitles.ModeSubtitlesOnly"),
                _ => baseText
            };
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubtitleStatus))]
    [NotifyPropertyChangedFor(nameof(SubtitleStatusText))]
    private SubtitleResultState _subtitleResultState = SubtitleResultState.None;

    public bool HasSubtitleStatus => SubtitleResultState != SubtitleResultState.None;

    public string SubtitleStatusText => SubtitleResultState switch
    {
        SubtitleResultState.Saved => LocalizationService.Get("Status.SubtitlesSaved"),
        SubtitleResultState.NoSubtitlesAvailable => LocalizationService.Get("Status.NoSubtitlesAvailable"),
        SubtitleResultState.NoSubtitlesAvailableLanguage => LocalizationService.Get("Status.NoSubtitlesAvailableLanguage"),
        SubtitleResultState.ConversionRequiresFfmpeg => LocalizationService.Get("Status.SubtitleConversionRequiresFfmpeg"),
        _ => string.Empty
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private QueueItemState _state = QueueItemState.Waiting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TransferDetailText))]
    private double? _bytesPerSecond;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TransferDetailText))]
    private TimeSpan? _eta;

    [ObservableProperty]
    private string? _detailText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private int _completedFileCount;

    public bool CanRetry => State == QueueItemState.Failed;

    public bool CanRemove => State != QueueItemState.Active;

    public string TransferDetailText
    {
        get
        {
            if (State != QueueItemState.Active)
                return string.Empty;

            var speed = FormatSpeed(BytesPerSecond);
            var eta = Eta is { } remaining
                ? LocalizationService.Get("Queue.EtaRemaining", FormatEta(remaining))
                : string.Empty;
            return string.Join("  •  ", new[] { speed, eta }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }

    public string StatusDisplayText => State switch
    {
        QueueItemState.Active => LocalizationService.Get("Queue.DownloadingProgress", Progress),
        QueueItemState.Completed when CompletedFileCount > 1 => LocalizationService.Get("Queue.CompletedFiles", CompletedFileCount),
        QueueItemState.Completed => LocalizationService.Get("Queue.Completed"),
        QueueItemState.AlreadyDownloaded => LocalizationService.Get("Queue.AlreadyDownloaded"),
        QueueItemState.Failed => LocalizationService.Get("Queue.Failed"),
        QueueItemState.Cancelled => LocalizationService.Get("Queue.Cancelled"),
        _ => LocalizationService.Get("Queue.Waiting")
    };

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(OptionsText));
        OnPropertyChanged(nameof(StatusDisplayText));
        OnPropertyChanged(nameof(TransferDetailText));
        OnPropertyChanged(nameof(SubtitleStatusText));
    }

    public void ApplyProgress(DownloadProgress progress)
    {
        Progress = Math.Clamp(progress.Percent, 0, 100);
        BytesPerSecond = progress.BytesPerSecond;
        Eta = progress.Eta;
    }

    public void ResetProgress()
    {
        Progress = 0;
        BytesPerSecond = null;
        Eta = null;
        SubtitleResultState = SubtitleResultState.None;
    }

    partial void OnStateChanged(QueueItemState value)
    {
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(TransferDetailText));
        if (value != QueueItemState.Active)
        {
            BytesPerSecond = null;
            Eta = null;
        }
    }

    private static string FormatSpeed(double? bytesPerSecond)
    {
        if (bytesPerSecond is not > 0)
            return string.Empty;

        var (value, key) = bytesPerSecond.Value switch
        {
            >= 1024d * 1024d * 1024d => (bytesPerSecond.Value / (1024d * 1024d * 1024d), "Queue.SpeedGigabytes"),
            >= 1024d * 1024d => (bytesPerSecond.Value / (1024d * 1024d), "Queue.SpeedMegabytes"),
            >= 1024d => (bytesPerSecond.Value / 1024d, "Queue.SpeedKilobytes"),
            _ => (bytesPerSecond.Value, "Queue.SpeedBytes")
        };
        return LocalizationService.Get(key, value);
    }

    private static string FormatEta(TimeSpan eta) =>
        eta.TotalHours >= 1 ? eta.ToString(@"h\:mm\:ss") : eta.ToString(@"mm\:ss");
}
