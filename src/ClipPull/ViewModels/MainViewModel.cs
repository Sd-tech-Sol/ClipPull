using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using ClipPull.Models;
using ClipPull.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Controls;

namespace ClipPull.ViewModels;

internal sealed partial class MainViewModel : ObservableObject
{
    private static readonly Regex UrlRegex = new(
        "https?://[^\\s<>\"']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HttpClient PreviewHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly YtDlpManager _engineManager = new();
    private readonly FfmpegManager _ffmpegManager = new();
    private readonly MediaService _mediaService = new();
    private readonly List<DownloadError> _lastErrors = [];

    private readonly string _archivePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipPull", "download-archive.txt");

    private CancellationTokenSource? _activeOperation;
    private CancellationTokenSource? _startupOperation;

    public MainViewModel()
    {
        OutputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "ClipPull");
        Queue.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsQueueEmpty));
            OnPropertyChanged(nameof(QueueSummaryText));
        };
    }

    public ObservableCollection<QueueItemViewModel> Queue { get; } = [];

    public bool IsQueueEmpty => Queue.Count == 0;

    public string QueueSummaryText => Queue.Count == 1 ? "1 élément" : $"{Queue.Count} éléments";

    public IReadOnlyList<string> Browsers { get; } =
        ["Chrome", "Edge", "Firefox", "Brave", "Chromium", "Opera", "Vivaldi"];

    public IReadOnlyList<string> FormatOptions { get; } = ["Vidéo", "Audio M4A", "Audio MP3"];

    public IReadOnlyList<string> QualityOptions { get; } =
        ["Auto (rapide)", "Meilleure", "1080p max", "720p max", "480p max", "Petit fichier"];

    [ObservableProperty]
    private string _urlsText = string.Empty;

    partial void OnUrlsTextChanged(string value)
    {
        OnPropertyChanged(nameof(LinkCount));
        OnPropertyChanged(nameof(HasLinks));
        OnPropertyChanged(nameof(LinkSummaryText));
        OnPropertyChanged(nameof(CanUsePrimaryAction));
    }

    public int LinkCount => GetUniqueUrls(UrlsText).Count;

    public bool HasLinks => LinkCount > 0;

    public string LinkSummaryText => LinkCount == 1 ? "1 lien prêt" : $"{LinkCount} liens prêts";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputFolderDisplay))]
    private string _outputFolder;

    public string OutputFolderDisplay
    {
        get
        {
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            if (!OutputFolder.StartsWith(downloads, StringComparison.OrdinalIgnoreCase))
                return OutputFolder;

            var relative = Path.GetRelativePath(downloads, OutputFolder);
            return relative == "." ? "Téléchargements" : $"Téléchargements\\{relative}";
        }
    }

    [ObservableProperty]
    private int _formatIndex;

    public bool IsVideoSelected
    {
        get => FormatIndex == 0;
        set { if (value) FormatIndex = 0; }
    }

    public bool IsM4aSelected
    {
        get => FormatIndex == 1;
        set { if (value) FormatIndex = 1; }
    }

    public bool IsMp3Selected
    {
        get => FormatIndex == 2;
        set { if (value) FormatIndex = 2; }
    }

    partial void OnFormatIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsVideoSelected));
        OnPropertyChanged(nameof(IsM4aSelected));
        OnPropertyChanged(nameof(IsMp3Selected));
        OnPropertyChanged(nameof(IsQualityEnabled));
    }

    [ObservableProperty]
    private int _qualityIndex;

    public bool IsQualityEnabled => FormatIndex == 0 && !IsBusy;

    [ObservableProperty]
    private bool _allowPlaylists;

    public bool IsPlaylistLimitEnabled => AllowPlaylists && !IsBusy;

    [ObservableProperty]
    private int _playlistLimit = 50;

    [ObservableProperty]
    private bool _useHistory = true;

    [ObservableProperty]
    private bool _useBrowserCookies;

    public bool IsBrowserEnabled => UseBrowserCookies && !IsBusy;

    [ObservableProperty]
    private string _selectedBrowser = "Chrome";

    [ObservableProperty]
    private bool _isAdvancedOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(IsQualityEnabled))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistLimitEnabled))]
    [NotifyPropertyChangedFor(nameof(IsBrowserEnabled))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionLabel))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionIcon))]
    [NotifyPropertyChangedFor(nameof(CanUsePrimaryAction))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public SymbolRegular PrimaryActionIcon => IsBusy ? SymbolRegular.Dismiss24 : SymbolRegular.ArrowDownload24;

    public string PrimaryActionLabel => IsBusy ? "Annuler" : "Télécharger tout";

    public bool CanUsePrimaryAction => IsBusy || HasLinks;

    [ObservableProperty]
    private bool _hasErrors;

    // Preview
    [ObservableProperty]
    private bool _isPreviewVisible;

    [ObservableProperty]
    private string _previewTitle = string.Empty;

    [ObservableProperty]
    private string _previewMeta = string.Empty;

    [ObservableProperty]
    private BitmapImage? _previewThumbnail;

    // Dependency chips (kept unobtrusive; full detail lives in the advanced panel).
    [ObservableProperty]
    private string _ytDlpStatusText = "Vérification...";

    partial void OnYtDlpStatusTextChanged(string value) => OnPropertyChanged(nameof(DependencySummaryText));

    [ObservableProperty]
    private string _ffmpegStatusText = "Non vérifié";

    partial void OnFfmpegStatusTextChanged(string value) => OnPropertyChanged(nameof(DependencySummaryText));

    public string DependencySummaryText
    {
        get
        {
            if (YtDlpStatusText == "À jour" && FfmpegStatusText is "À jour" or "Non installé")
                return "Composants prêts";
            if (YtDlpStatusText == "Hors ligne")
                return "yt-dlp hors ligne";
            if (FfmpegStatusText == "Indisponible")
                return "FFmpeg indisponible";

            return "Vérification...";
        }
    }

    // InfoBar
    [ObservableProperty]
    private bool _isInfoBarOpen;

    [ObservableProperty]
    private string _infoBarTitle = "Prêt";

    [ObservableProperty]
    private string _infoBarMessage = "Colle des liens ou glisse un fichier .txt.";

    [ObservableProperty]
    private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;

    private void SetStatus(string title, string message, InfoBarSeverity severity)
    {
        InfoBarTitle = title;
        InfoBarMessage = message;
        InfoBarSeverity = severity;
        IsInfoBarOpen = true;
    }

    [RelayCommand]
    private void DismissInfoBar() => IsInfoBarOpen = false;

    [RelayCommand]
    private void ToggleAdvanced() => IsAdvancedOpen = !IsAdvancedOpen;

    public async Task RunStartupChecksAsync()
    {
        if (_startupOperation is not null || _activeOperation is not null)
            return;

        _startupOperation = new CancellationTokenSource();
        var token = _startupOperation.Token;

        try
        {
            try
            {
                YtDlpStatusText = "Vérification...";
                await _engineManager.EnsureAsync(_ => { }, token);
                YtDlpStatusText = "À jour";
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                YtDlpStatusText = "Hors ligne";
            }

            try
            {
                if (_ffmpegManager.IsInstalled)
                {
                    FfmpegStatusText = "Vérification...";
                    var progress = new Progress<double>(_ => { });
                    await _ffmpegManager.UpdateInstalledAsync(_ => { }, progress, token);
                    FfmpegStatusText = "À jour";
                }
                else
                {
                    FfmpegStatusText = "Non installé";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                FfmpegStatusText = "Indisponible";
            }
        }
        finally
        {
            _startupOperation.Dispose();
            _startupOperation = null;
        }
    }

    public void CancelStartupChecks() => _startupOperation?.Cancel();

    public void CancelActiveOperation() => _activeOperation?.Cancel();

    [RelayCommand]
    private void Paste()
    {
        try
        {
            var urls = GetUniqueUrls(Clipboard.GetText());
            if (urls.Count > 0)
                AddUrls(urls);
            else
                SetStatus("Presse-papiers", "Le presse-papiers ne contient aucun lien web valide.", InfoBarSeverity.Informational);
        }
        catch
        {
            SetStatus("Presse-papiers", "Impossible de lire le presse-papiers.", InfoBarSeverity.Warning);
        }
    }

    public void PrefillFromClipboard()
    {
        if (string.IsNullOrWhiteSpace(UrlsText))
        {
            try
            {
                var urls = GetUniqueUrls(Clipboard.GetText());
                if (urls.Count > 0)
                    AddUrls(urls);
            }
            catch
            {
                // Silent: this is a convenience prefill, not a user action.
            }
        }
    }

    [RelayCommand]
    private async Task ImportTextFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importer une liste de liens",
            Filter = "Fichiers texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var text = await File.ReadAllTextAsync(dialog.FileName);
            var urls = GetUniqueUrls(text);
            if (urls.Count == 0)
            {
                SetStatus("Import", "Le fichier ne contient aucun lien web valide.", InfoBarSeverity.Informational);
                return;
            }

            AddUrls(urls);
            SetStatus("Import", $"{urls.Count} lien(s) importé(s) du fichier.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Import impossible", ex.Message, InfoBarSeverity.Error);
        }
    }

    public void ImportDroppedFiles(IEnumerable<string> paths)
    {
        _ = ImportDroppedFilesAsync(paths);
    }

    private async Task ImportDroppedFilesAsync(IEnumerable<string> paths)
    {
        var total = 0;
        foreach (var path in paths.Where(p => string.Equals(Path.GetExtension(p), ".txt", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var urls = GetUniqueUrls(await File.ReadAllTextAsync(path));
                AddUrls(urls);
                total += urls.Count;
            }
            catch (Exception ex)
            {
                SetStatus("Import impossible", $"{Path.GetFileName(path)} : {ex.Message}", InfoBarSeverity.Error);
            }
        }

        if (total > 0)
            SetStatus("Import", $"{total} lien(s) importé(s).", InfoBarSeverity.Success);
    }

    public void AddDroppedText(string text)
    {
        var urls = GetUniqueUrls(text);
        if (urls.Count > 0)
            AddUrls(urls);
    }

    [RelayCommand]
    private void Clear()
    {
        UrlsText = string.Empty;
        Queue.Clear();
        _lastErrors.Clear();
        HasErrors = false;
        IsPreviewVisible = false;
        PreviewThumbnail = null;
        SetStatus("Prêt", "Colle des liens ou glisse un fichier .txt.", InfoBarSeverity.Informational);
    }

    [RelayCommand]
    private async Task PreviewFirstAsync()
    {
        if (_activeOperation is not null)
            return;

        var url = GetUniqueUrls(UrlsText).FirstOrDefault();
        if (url is null)
        {
            SetStatus("Aperçu", "Ajoute un lien avant de demander un aperçu.", InfoBarSeverity.Informational);
            return;
        }

        _activeOperation = new CancellationTokenSource();
        var token = _activeOperation.Token;

        try
        {
            var engine = await _engineManager.EnsureAsync(m => SetStatus("Aperçu", m, InfoBarSeverity.Informational), token);
            var browser = UseBrowserCookies ? SelectedBrowser : null;
            SetStatus("Aperçu", "Analyse du lien...", InfoBarSeverity.Informational);
            var preview = await _mediaService.PreviewAsync(engine, url, browser, token);

            PreviewTitle = preview.Title;
            var duration = preview.DurationSeconds is > 0 ? FormatDuration(preview.DurationSeconds.Value) : "durée inconnue";
            var playlist = preview.PlaylistCount is > 0 ? $" • playlist : {preview.PlaylistCount} élément(s)" : string.Empty;
            PreviewMeta = $"{preview.Platform} • {duration}{playlist}";
            await LoadThumbnailAsync(preview.ThumbnailUrl, token);
            IsPreviewVisible = true;
            SetStatus("Aperçu", "Aperçu chargé.", InfoBarSeverity.Success);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus("Aperçu impossible", GetUsefulError(ex.Message), InfoBarSeverity.Warning);
        }
        finally
        {
            _activeOperation?.Dispose();
            _activeOperation = null;
        }
    }

    private async Task LoadThumbnailAsync(string? url, CancellationToken cancellationToken)
    {
        PreviewThumbnail = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            return;

        try
        {
            var bytes = await PreviewHttp.GetByteArrayAsync(uri, cancellationToken);
            var image = new BitmapImage();
            using var stream = new MemoryStream(bytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            PreviewThumbnail = image;
        }
        catch
        {
            // Thumbnail is a nice-to-have; failure to load it should not affect the preview text.
        }
    }

    [RelayCommand]
    private async Task DownloadOrCancelAsync()
    {
        if (_activeOperation is not null)
        {
            _activeOperation.Cancel();
            return;
        }

        await ExecuteDownloadFlowAsync(GetUniqueUrls(UrlsText));
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        if (_activeOperation is not null || _lastErrors.Count == 0)
            return;

        var urls = _lastErrors.Select(x => x.Url).Distinct(StringComparer.Ordinal).ToList();
        await ExecuteDownloadFlowAsync(urls);
    }

    [RelayCommand]
    private async Task RetryItemAsync(QueueItemViewModel? item)
    {
        if (item is null || _activeOperation is not null)
            return;

        await ExecuteDownloadFlowAsync([item.Url]);
    }

    private async Task ExecuteDownloadFlowAsync(IReadOnlyList<string> urls)
    {
        if (urls.Count == 0)
        {
            SetStatus("Aucun lien", "Ajoute au moins un lien web valide.", InfoBarSeverity.Informational);
            return;
        }

        var folder = OutputFolder.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            SetStatus("Dossier requis", "Choisis un dossier de destination.", InfoBarSeverity.Informational);
            return;
        }

        if (AllowPlaylists)
        {
            var confirm = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Confirmer les playlists",
                Content = $"Le mode playlist est activé. ClipPull autorisera jusqu'à {PlaylistLimit} éléments par lien de playlist.\n\nContinuer?",
                PrimaryButtonText = "Continuer",
                CloseButtonText = "Annuler"
            };
            if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
                return;
        }

        var needsFfmpeg = RequiresFfmpeg();
        if (needsFfmpeg && !_ffmpegManager.IsInstalled)
        {
            var confirm = new Wpf.Ui.Controls.MessageBox
            {
                Title = "FFmpeg requis",
                Content = "Cette option nécessite FFmpeg. Au premier usage, ClipPull téléchargera environ 140 Mo depuis le projet BtbN/FFmpeg-Builds, puis vérifiera le SHA-256 avant extraction.\n\nContinuer?",
                PrimaryButtonText = "Continuer",
                CloseButtonText = "Annuler"
            };
            if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
                return;
        }

        _activeOperation = new CancellationTokenSource();
        var token = _activeOperation.Token;
        _lastErrors.Clear();
        HasErrors = false;
        PrepareQueue(urls);
        IsBusy = true;

        var succeeded = 0;
        var skipped = 0;
        var cancelled = false;

        try
        {
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Path.GetDirectoryName(_archivePath)!);
            SetStatus("Préparation", "Vérification des composants...", InfoBarSeverity.Informational);
            var engine = await _engineManager.EnsureAsync(m => SetStatus("Préparation", m, InfoBarSeverity.Informational), token);

            string? ffmpegDirectory = null;
            if (needsFfmpeg)
            {
                var ffmpegProgress = new Progress<double>(v => SetActiveProgress(v));
                ffmpegDirectory = await _ffmpegManager.EnsureAsync(m => SetStatus("Préparation", m, InfoBarSeverity.Informational), ffmpegProgress, token);
            }

            var settings = ReadSettings(ffmpegDirectory);

            for (var i = 0; i < urls.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var url = urls[i];
                var item = Queue[i];
                item.State = QueueItemState.Active;
                item.StatusText = "En cours...";
                item.Progress = 0;

                var prefix = $"{i + 1}/{urls.Count}";
                var progress = new Progress<double>(v => item.Progress = Math.Clamp(v, 0, 100));

                try
                {
                    var result = await _mediaService.DownloadAsync(
                        engine, url, folder, settings, progress,
                        m => SetStatus($"Téléchargement {prefix}", m, InfoBarSeverity.Informational),
                        token);

                    if (result.Files.Count == 0 && settings.UseHistory)
                    {
                        skipped++;
                        item.State = QueueItemState.AlreadyDownloaded;
                        item.StatusText = "Déjà téléchargé";
                    }
                    else
                    {
                        succeeded++;
                        item.State = QueueItemState.Completed;
                        item.StatusText = result.Files.Count > 1 ? $"Terminé ({result.Files.Count} fichiers)" : "Terminé";
                        if (result.Files.Count > 0)
                        {
                            item.DisplayText = string.Join(", ", result.Files.Select(Path.GetFileName));
                            item.DetailText = item.DisplayText;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    item.State = QueueItemState.Cancelled;
                    item.StatusText = "Annulé";
                    throw;
                }
                catch (Exception ex)
                {
                    var message = GetUsefulError(ex.Message);
                    _lastErrors.Add(new DownloadError(url, item.Platform, message));
                    item.State = QueueItemState.Failed;
                    item.StatusText = "Échec";
                    item.DetailText = message;
                    SetStatus($"Téléchargement {prefix}", "Échec; passage au suivant...", InfoBarSeverity.Warning);
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            foreach (var item in Queue.Where(q => q.State == QueueItemState.Waiting))
            {
                item.State = QueueItemState.Cancelled;
                item.StatusText = "Non démarré";
            }
        }
        catch (Exception ex)
        {
            SetStatus("Erreur", GetUsefulError(ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            _activeOperation?.Dispose();
            _activeOperation = null;
            IsBusy = false;
            HasErrors = _lastErrors.Count > 0;
        }

        if (cancelled)
        {
            SetStatus("File annulée", $"{succeeded} réussi(s), {skipped} déjà présent(s), {_lastErrors.Count} échec(s).", InfoBarSeverity.Warning);
        }
        else if (_lastErrors.Count == 0)
        {
            SetStatus("Terminé", $"{succeeded} réussi(s), {skipped} déjà présent(s).", InfoBarSeverity.Success);
        }
        else
        {
            SetStatus("Terminé avec erreurs", $"{succeeded} réussi(s), {skipped} déjà présent(s), {_lastErrors.Count} échec(s).", InfoBarSeverity.Warning);
        }
    }

    private void SetActiveProgress(double value)
    {
        var active = Queue.FirstOrDefault(q => q.State == QueueItemState.Active);
        if (active is not null)
            active.Progress = Math.Clamp(value, 0, 100);
    }

    [RelayCommand]
    private void CopyErrors()
    {
        if (_lastErrors.Count == 0)
            return;

        var text = new StringBuilder("ClipPull - rapport d'erreurs\r\n\r\n");
        foreach (var error in _lastErrors)
            text.AppendLine($"[{error.Platform}] {error.Url}\r\n{error.Message}\r\n");

        try
        {
            Clipboard.SetText(text.ToString());
            SetStatus("Copié", "Rapport d'erreurs copié dans le presse-papiers.", InfoBarSeverity.Success);
        }
        catch
        {
            SetStatus("Presse-papiers", "Impossible de copier le rapport d'erreurs.", InfoBarSeverity.Warning);
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var folder = OutputFolder.Trim();
        if (!Directory.Exists(folder))
            return;

        var info = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = false };
        info.ArgumentList.Add(folder);
        Process.Start(info);
    }

    [RelayCommand]
    private void ChooseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choisir le dossier de téléchargement",
            InitialDirectory = Directory.Exists(OutputFolder) ? OutputFolder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
            OutputFolder = dialog.FolderName;
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!File.Exists(_archivePath))
        {
            SetStatus("Historique", "L'historique est déjà vide.", InfoBarSeverity.Informational);
            return;
        }

        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Effacer l'historique",
            Content = "Effacer l'historique local des téléchargements? Les fichiers déjà téléchargés ne seront pas supprimés.",
            PrimaryButtonText = "Effacer",
            CloseButtonText = "Annuler"
        };

        if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        try
        {
            File.Delete(_archivePath);
            SetStatus("Historique", "Historique local effacé.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Historique", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void AddUrls(IEnumerable<string> incoming)
    {
        var urls = GetUniqueUrls(UrlsText).ToList();
        var seen = new HashSet<string>(urls, StringComparer.Ordinal);
        foreach (var url in incoming)
        {
            if (seen.Add(url))
                urls.Add(url);
        }

        UrlsText = string.Join(Environment.NewLine, urls);
    }

    private bool RequiresFfmpeg() => FormatIndex != 0 || QualityIndex is >= 1 and <= 4;

    private DownloadSettings ReadSettings(string? ffmpegDirectory)
    {
        var mode = FormatIndex switch { 1 => MediaMode.AudioM4a, 2 => MediaMode.AudioMp3, _ => MediaMode.Video };
        var quality = QualityIndex switch
        {
            1 => VideoQuality.Best,
            2 => VideoQuality.P1080,
            3 => VideoQuality.P720,
            4 => VideoQuality.P480,
            5 => VideoQuality.Small,
            _ => VideoQuality.Auto
        };

        return new DownloadSettings(
            mode, quality, AllowPlaylists, PlaylistLimit,
            UseHistory, UseBrowserCookies ? SelectedBrowser : null,
            ffmpegDirectory, UseHistory ? _archivePath : null);
    }

    private void PrepareQueue(IReadOnlyList<string> urls)
    {
        Queue.Clear();
        for (var i = 0; i < urls.Count; i++)
        {
            var url = urls[i];
            var platform = GetPlatform(url);
            var options = FormatIndex == 0
                ? $"{platform}  ·  {FormatOptions[FormatIndex]}  ·  {QualityOptions[QualityIndex]}"
                : $"{platform}  ·  {FormatOptions[FormatIndex]}";
            Queue.Add(new QueueItemViewModel(url, platform, ShortenUrl(url), options));
        }
    }

    private static string ShortenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var path = uri.PathAndQuery;
        if (path.Length > 42)
            path = string.Concat(path.AsSpan(0, 39), "...");

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        return host + path;
    }

    private static IReadOnlyList<string> GetUniqueUrls(string text)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in UrlRegex.Matches(text ?? string.Empty))
        {
            var candidate = match.Value.TrimEnd('.', ',', ';', ')', ']', '}');
            if (!IsHttpUrl(candidate) || !seen.Add(candidate))
                continue;

            result.Add(candidate);
        }

        return result;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string GetPlatform(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return "Web";

        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("facebook.com") || host.Contains("fb.watch")) return "Facebook";
        if (host.Contains("instagram.com")) return "Instagram";
        if (host.Contains("tiktok.com")) return "TikTok";
        if (host.Contains("youtube.com") || host.Contains("youtu.be")) return "YouTube";
        if (host == "x.com" || host.EndsWith(".x.com") || host.Contains("twitter.com")) return "X / Twitter";
        if (host.Contains("vimeo.com")) return "Vimeo";

        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    private static string GetUsefulError(string message)
    {
        var lines = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.LastOrDefault(line => line.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            ?? lines.LastOrDefault() ?? "Échec du téléchargement.";
    }

    private static string FormatDuration(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }
}
