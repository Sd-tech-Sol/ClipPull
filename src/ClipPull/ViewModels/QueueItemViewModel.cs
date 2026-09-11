using CommunityToolkit.Mvvm.ComponentModel;
using ClipPull.Localization;

namespace ClipPull.ViewModels;

internal sealed partial class QueueItemViewModel : ObservableObject
{
    public QueueItemViewModel(string url, string platform, string displayText, int formatIndex, int qualityIndex)
    {
        Url = url;
        Platform = platform;
        DisplayText = displayText;
        FormatIndex = formatIndex;
        QualityIndex = qualityIndex;
    }

    public string Url { get; }

    public string Platform { get; }

    /// <summary>Shortened URL, replaced with the resulting file name(s) once known.</summary>
    [ObservableProperty]
    private string _displayText;

    public int FormatIndex { get; }

    public int QualityIndex { get; }

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
            if (FormatIndex != 0)
                return $"{Platform}  ·  {format}";

            var quality = LocalizationService.Get(QualityIndex switch
            {
                1 => "Options.QualityBest",
                2 => "Options.Quality1080",
                3 => "Options.Quality720",
                4 => "Options.Quality480",
                5 => "Options.QualitySmall",
                _ => "Options.QualityAuto"
            });
            return $"{Platform}  ·  {format}  ·  {quality}";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private QueueItemState _state = QueueItemState.Waiting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private double _progress;

    [ObservableProperty]
    private string? _detailText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private int _completedFileCount;

    public bool CanRetry => State == QueueItemState.Failed;

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
    }
}
