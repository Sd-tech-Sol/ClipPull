using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipPull.ViewModels;

internal sealed partial class QueueItemViewModel : ObservableObject
{
    public QueueItemViewModel(string url, string platform, string displayText, string optionsText)
    {
        Url = url;
        Platform = platform;
        DisplayText = displayText;
        OptionsText = optionsText;
    }

    public string Url { get; }

    public string Platform { get; }

    /// <summary>Shortened URL, replaced with the resulting file name(s) once known.</summary>
    [ObservableProperty]
    private string _displayText;

    public string OptionsText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private QueueItemState _state = QueueItemState.Waiting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private string _statusText = "En attente";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
    private double _progress;

    [ObservableProperty]
    private string? _detailText;

    public bool CanRetry => State == QueueItemState.Failed;

    public string StatusDisplayText => State == QueueItemState.Active
        ? $"Téléchargement  {Progress:0}%"
        : StatusText;
}
