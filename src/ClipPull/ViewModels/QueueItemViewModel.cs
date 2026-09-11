using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipPull.ViewModels;

internal sealed partial class QueueItemViewModel : ObservableObject
{
    public QueueItemViewModel(string url, string platform, string displayText, int accentIndex)
    {
        Url = url;
        Platform = platform;
        DisplayText = displayText;
        AccentIndex = accentIndex;
    }

    public string Url { get; }

    public string Platform { get; }

    /// <summary>Shortened URL, replaced with the resulting file name(s) once known.</summary>
    [ObservableProperty]
    private string _displayText;

    /// <summary>Index into the neutral per-row accent palette (not a platform brand color).</summary>
    public int AccentIndex { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    private QueueItemState _state = QueueItemState.Waiting;

    [ObservableProperty]
    private string _statusText = "En attente";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _detailText;

    public bool CanRetry => State == QueueItemState.Failed;
}
