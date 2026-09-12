using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using ClipPull.Models;
using ClipPull.Localization;
using ClipPull.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Controls;

namespace ClipPull.ViewModels;

internal sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly Regex UrlRegex = new(
        "https?://[^\\s<>\"']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HttpClient PreviewHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly YtDlpManager _engineManager = new();
    private readonly FfmpegManager _ffmpegManager = new();
    private readonly MediaService _mediaService = new();
    private readonly UpdateService _updateService;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly List<DownloadError> _lastErrors = [];

    private readonly string _archivePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipPull", "download-archive.txt");

    private CancellationTokenSource? _activeOperation;
    private CancellationTokenSource? _startupOperation;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _updateCheckGate = new(1, 1);
    private Uri? _availableReleasePage;
    private string _updateStatusKey = "Update.NotChecked";

    private static readonly Uri RepositoryPage = new("https://github.com/Sd-tech-Sol/ClipPull");
    private static readonly Uri LicensePage = new("https://github.com/Sd-tech-Sol/ClipPull/blob/main/LICENSE");
    private static readonly Uri AiAssistancePage = new("https://github.com/Sd-tech-Sol/ClipPull/blob/main/AI_ASSISTANCE.md");

    public MainViewModel() : this(new SettingsService(), new UpdateService())
    {
    }

    internal MainViewModel(SettingsService settingsService, UpdateService updateService)
    {
        _settingsService = settingsService;
        _updateService = updateService;
        _settings = _settingsService.Load();
        _outputFolder = _settings.OutputFolder;
        _formatIndex = _settings.FormatIndex;
        _qualityIndex = _settings.QualityIndex;
        _playlistLimit = _settings.PlaylistLimit;
        _subtitleModeIndex = _settings.SubtitleModeIndex;
        _subtitleLanguageIndex = _settings.SubtitleLanguageIndex;
        _useAutomaticSubtitleFallback = _settings.UseAutomaticSubtitleFallback;
        _useHistory = _settings.UseHistory;
        _useBrowserCookies = _settings.UseBrowserCookies;
        _selectedBrowser = _settings.SelectedBrowser;
        _checkForUpdates = _settings.CheckForUpdates;
        Queue.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsQueueEmpty));
            OnPropertyChanged(nameof(QueueSummaryText));
        };
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<QueueItemViewModel> Queue { get; } = [];

    public bool IsQueueEmpty => Queue.Count == 0;

    public string QueueSummaryText => L(Queue.Count == 1 ? "Queue.ItemOne" : "Queue.ItemMany", Queue.Count);

    public IReadOnlyList<string> Browsers { get; } =
        ["Chrome", "Edge", "Firefox", "Brave", "Chromium", "Opera", "Vivaldi"];

    public string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "0.6.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

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

    public string LinkSummaryText => L(LinkCount == 1 ? "Links.ReadyOne" : "Links.ReadyMany", LinkCount);

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
            var downloadsLabel = L("Output.DownloadsFolder");
            return relative == "." ? downloadsLabel : $"{downloadsLabel}\\{relative}";
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
        SaveGeneralSettings();
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
    [NotifyPropertyChangedFor(nameof(IsSubtitleLanguageEnabled))]
    [NotifyPropertyChangedFor(nameof(IsSubtitleFallbackEnabled))]
    private int _subtitleModeIndex;

    public bool IsSubtitlesEnabled => SubtitleModeIndex != 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSubtitleFallbackEnabled))]
    private int _subtitleLanguageIndex;

    public bool IsSubtitleLanguageEnabled => IsSubtitlesEnabled && !IsBusy;

    // The fallback toggle is meaningless for "All available": that mode only ever
    // downloads manually authored tracks, never auto-generated captions.
    public bool IsSubtitleFallbackEnabled => IsSubtitlesEnabled && SubtitleLanguageIndex != 3 && !IsBusy;

    [ObservableProperty]
    private bool _useAutomaticSubtitleFallback;

    [ObservableProperty]
    private bool _useHistory = true;

    [ObservableProperty]
    private bool _useBrowserCookies;

    public bool IsBrowserEnabled => UseBrowserCookies && !IsBusy;

    [ObservableProperty]
    private string _selectedBrowser = "Chrome";

    [ObservableProperty]
    private bool _checkForUpdates = true;

    [ObservableProperty]
    private bool _isAdvancedOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateAvailableText))]
    private string _availableVersion = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateStatusText))]
    [NotifyPropertyChangedFor(nameof(CanCheckForUpdates))]
    private bool _isCheckingForUpdates;

    public string UpdateAvailableText => L("Update.Available", AvailableVersion);

    public string UpdateStatusText => L(IsCheckingForUpdates ? "Update.Checking" : _updateStatusKey);

    public bool CanCheckForUpdates => !IsCheckingForUpdates;

    internal string ThemePreference => _settings.Theme;

    internal WindowSettings SavedWindow => _settings.Window;

    internal void SetThemePreference(string preference)
    {
        _settings.Theme = preference;
        _settingsService.Save(_settings);
    }

    internal void SetLanguagePreference(AppLanguage language)
    {
        _settings.Language = language.ToString();
        _settingsService.Save(_settings);
    }

    internal void SaveWindow(double width, double height, bool isMaximized)
    {
        _settings.Window.Width = width;
        _settings.Window.Height = height;
        _settings.Window.IsMaximized = isMaximized;
        _settingsService.Save(_settings);
    }

    private void SaveGeneralSettings()
    {
        _settings.OutputFolder = OutputFolder;
        _settings.FormatIndex = FormatIndex;
        _settings.QualityIndex = QualityIndex;
        _settings.UseHistory = UseHistory;
        _settings.UseBrowserCookies = UseBrowserCookies;
        _settings.SelectedBrowser = SelectedBrowser;
        _settings.PlaylistLimit = PlaylistLimit;
        _settings.CheckForUpdates = CheckForUpdates;
        _settings.SubtitleModeIndex = SubtitleModeIndex;
        _settings.SubtitleLanguageIndex = SubtitleLanguageIndex;
        _settings.UseAutomaticSubtitleFallback = UseAutomaticSubtitleFallback;
        _settingsService.Save(_settings);
    }

    partial void OnOutputFolderChanged(string value) => SaveGeneralSettings();
    partial void OnQualityIndexChanged(int value) => SaveGeneralSettings();
    partial void OnPlaylistLimitChanged(int value) => SaveGeneralSettings();
    partial void OnSubtitleModeIndexChanged(int value) => SaveGeneralSettings();
    partial void OnSubtitleLanguageIndexChanged(int value) => SaveGeneralSettings();
    partial void OnUseAutomaticSubtitleFallbackChanged(bool value) => SaveGeneralSettings();
    partial void OnUseHistoryChanged(bool value) => SaveGeneralSettings();
    partial void OnUseBrowserCookiesChanged(bool value) => SaveGeneralSettings();
    partial void OnSelectedBrowserChanged(string value) => SaveGeneralSettings();
    partial void OnCheckForUpdatesChanged(bool value) => SaveGeneralSettings();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(IsQualityEnabled))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistLimitEnabled))]
    [NotifyPropertyChangedFor(nameof(IsBrowserEnabled))]
    [NotifyPropertyChangedFor(nameof(IsSubtitleLanguageEnabled))]
    [NotifyPropertyChangedFor(nameof(IsSubtitleFallbackEnabled))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionLabel))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionIcon))]
    [NotifyPropertyChangedFor(nameof(CanUsePrimaryAction))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public SymbolRegular PrimaryActionIcon => IsBusy ? SymbolRegular.Dismiss24 : SymbolRegular.ArrowDownload24;

    public string PrimaryActionLabel => L(IsBusy ? "Common.Cancel" : "Options.DownloadAll");

    public bool CanUsePrimaryAction => IsBusy || HasLinks;

    [ObservableProperty]
    private bool _hasErrors;

    // Preview
    [ObservableProperty]
    private bool _isPreviewVisible;

    [ObservableProperty]
    private string _previewTitle = string.Empty;

    private double? _previewDurationSeconds;
    private int? _previewPlaylistCount;

    public string PreviewMeta
    {
        get
        {
            if (!IsPreviewVisible && _previewDurationSeconds is null && _previewPlaylistCount is null)
                return string.Empty;

            var duration = _previewDurationSeconds is > 0
                ? FormatDuration(_previewDurationSeconds.Value)
                : L("Status.UnknownDuration");
            var playlist = _previewPlaylistCount switch
            {
                1 => L("Status.PreviewPlaylistOne"),
                > 1 => L("Status.PreviewPlaylistMany", _previewPlaylistCount.Value),
                _ => string.Empty
            };
            return $"{_previewPlatform} • {duration}{playlist}";
        }
    }

    private string _previewPlatform = string.Empty;

    [ObservableProperty]
    private BitmapImage? _previewThumbnail;

    // Dependency chips (kept unobtrusive; full detail lives in the advanced panel).
    private DependencyState _ytDlpState = DependencyState.Checking;
    private DependencyState _ffmpegState = DependencyState.NotInstalled;

    public string YtDlpStatusText => LocalizeDependencyState(_ytDlpState);

    public string FfmpegStatusText => LocalizeDependencyState(_ffmpegState);

    public string DependencySummaryText
    {
        get
        {
            if (_ytDlpState == DependencyState.UpToDate &&
                _ffmpegState is DependencyState.UpToDate or DependencyState.NotInstalled)
                return L("Dependency.ComponentsReady");
            if (_ytDlpState == DependencyState.Offline)
                return L("Dependency.YtDlpOffline");
            if (_ffmpegState == DependencyState.Unavailable)
                return L("Dependency.FfmpegUnavailable");

            return L("Dependency.Checking");
        }
    }

    // InfoBar
    [ObservableProperty]
    private bool _isInfoBarOpen;

    private LocalizedMessage _infoBarTitleResource = new("Status.ReadyTitle");
    private LocalizedMessage _infoBarMessageResource = new("Status.ReadyMessage");
    private string? _infoBarRawMessage;

    public string InfoBarTitle => _infoBarTitleResource.Resolve();

    public string InfoBarMessage => _infoBarRawMessage ?? _infoBarMessageResource.Resolve();

    [ObservableProperty]
    private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;

    private void SetStatus(string titleKey, string messageKey, InfoBarSeverity severity, params object[] messageArguments)
    {
        _infoBarTitleResource = new LocalizedMessage(titleKey);
        _infoBarMessageResource = new LocalizedMessage(messageKey, messageArguments);
        _infoBarRawMessage = null;
        OnPropertyChanged(nameof(InfoBarTitle));
        OnPropertyChanged(nameof(InfoBarMessage));
        InfoBarSeverity = severity;
        IsInfoBarOpen = true;
    }

    private void SetStatus(string titleKey, LocalizedMessage message, InfoBarSeverity severity)
    {
        _infoBarTitleResource = new LocalizedMessage(titleKey);
        _infoBarMessageResource = message;
        _infoBarRawMessage = null;
        OnPropertyChanged(nameof(InfoBarTitle));
        OnPropertyChanged(nameof(InfoBarMessage));
        InfoBarSeverity = severity;
        IsInfoBarOpen = true;
    }

    private void SetStatus(LocalizedMessage title, LocalizedMessage message, InfoBarSeverity severity)
    {
        _infoBarTitleResource = title;
        _infoBarMessageResource = message;
        _infoBarRawMessage = null;
        OnPropertyChanged(nameof(InfoBarTitle));
        OnPropertyChanged(nameof(InfoBarMessage));
        InfoBarSeverity = severity;
        IsInfoBarOpen = true;
    }

    private void SetRawStatus(string titleKey, string message, InfoBarSeverity severity)
    {
        _infoBarTitleResource = new LocalizedMessage(titleKey);
        _infoBarRawMessage = message;
        OnPropertyChanged(nameof(InfoBarTitle));
        OnPropertyChanged(nameof(InfoBarMessage));
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
                SetYtDlpState(DependencyState.Checking);
                await _engineManager.EnsureAsync(_ => { }, token);
                SetYtDlpState(DependencyState.UpToDate);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                SetYtDlpState(DependencyState.Offline);
            }

            try
            {
                if (_ffmpegManager.IsInstalled)
                {
                    SetFfmpegState(DependencyState.Checking);
                    var progress = new Progress<double>(_ => { });
                    await _ffmpegManager.UpdateInstalledAsync(_ => { }, progress, token);
                    SetFfmpegState(DependencyState.UpToDate);
                }
                else
                {
                    SetFfmpegState(DependencyState.NotInstalled);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                SetFfmpegState(DependencyState.Unavailable);
            }
        }
        finally
        {
            _startupOperation.Dispose();
            _startupOperation = null;
        }
    }

    public Task RunStartupUpdateCheckAsync()
    {
        if (!UpdateService.ShouldRunAutomaticCheck(
                CheckForUpdates, _settings.LastUpdateCheckUtc, DateTimeOffset.UtcNow, TimeSpan.FromHours(18)))
        {
            return Task.CompletedTask;
        }

        return CheckForUpdatesCoreAsync(manual: false);
    }

    [RelayCommand]
    private Task CheckForUpdatesAsync() => CheckForUpdatesCoreAsync(manual: true);

    private async Task CheckForUpdatesCoreAsync(bool manual)
    {
        await _updateCheckGate.WaitAsync();
        try
        {
            if (!manual && !UpdateService.ShouldRunAutomaticCheck(
                    CheckForUpdates, _settings.LastUpdateCheckUtc, DateTimeOffset.UtcNow, TimeSpan.FromHours(18)))
            {
                return;
            }

            IsCheckingForUpdates = true;
            var result = await _updateService.CheckAsync(AppVersion, _lifetimeCancellation.Token);

            // A failed check (offline, rate-limited, transient GitHub error) must not be
            // recorded as a successful 18-hour check, or a momentary outage would silence
            // automatic checks for the rest of that window.
            if (result.Status != UpdateCheckStatus.Failed)
            {
                _settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
                _settingsService.Save(_settings);
            }

            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable when result.ReleasePage is not null:
                    AvailableVersion = result.LatestVersion ?? string.Empty;
                    _availableReleasePage = result.ReleasePage;
                    IsUpdateAvailable = true;
                    _updateStatusKey = "Update.AvailableShort";
                    break;
                case UpdateCheckStatus.UpToDate:
                    IsUpdateAvailable = false;
                    _availableReleasePage = null;
                    _updateStatusKey = "Update.UpToDate";
                    if (manual)
                        SetStatus("Update.Title", "Update.UpToDate", InfoBarSeverity.Success);
                    break;
                default:
                    _updateStatusKey = "Update.Unable";
                    if (manual)
                        SetStatus("Update.Title", "Update.Unable", InfoBarSeverity.Warning);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // The window closed while a check was in flight; nothing to report.
        }
        finally
        {
            IsCheckingForUpdates = false;
            OnPropertyChanged(nameof(UpdateStatusText));
            _updateCheckGate.Release();
        }
    }

    public void CancelUpdateChecks() => _lifetimeCancellation.Cancel();

    [RelayCommand]
    private void ViewUpdate()
    {
        var releaseTag = _availableReleasePage is null
            ? string.Empty
            : Uri.UnescapeDataString(_availableReleasePage.Segments[^1].TrimEnd('/'));
        if (_availableReleasePage is not null &&
            UpdateService.TryValidateReleasePage(_availableReleasePage.AbsoluteUri, releaseTag, out var validated) &&
            UpdateService.TryParseVersion(releaseTag, out var parsed) &&
            string.Equals($"{parsed.Major}.{parsed.Minor}.{parsed.Build}", AvailableVersion, StringComparison.Ordinal))
        {
            OpenTrustedWebPage(validated);
        }
    }

    [RelayCommand]
    private static void OpenGitHub() => OpenTrustedWebPage(RepositoryPage);

    [RelayCommand]
    private static void OpenLicense() => OpenTrustedWebPage(LicensePage);

    [RelayCommand]
    private static void OpenAiAssistance() => OpenTrustedWebPage(AiAssistancePage);

    private static void OpenTrustedWebPage(Uri page)
    {
        if (page.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(page.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !(string.Equals(page.AbsolutePath.TrimEnd('/'), "/Sd-tech-Sol/ClipPull", StringComparison.OrdinalIgnoreCase) ||
              page.AbsolutePath.StartsWith("/Sd-tech-Sol/ClipPull/", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = page.AbsoluteUri,
            UseShellExecute = true
        });
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
                SetStatus("Status.ClipboardTitle", "Status.ClipboardNoLinks", InfoBarSeverity.Informational);
        }
        catch
        {
            SetStatus("Status.ClipboardTitle", "Status.ClipboardReadFailed", InfoBarSeverity.Warning);
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
            Title = L("Dialog.ImportTitle"),
            Filter = L("Dialog.TextFilesFilter"),
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
                SetStatus("Status.ImportTitle", "Status.ImportNoLinks", InfoBarSeverity.Informational);
                return;
            }

            AddUrls(urls);
            SetStatus("Status.ImportTitle",
                urls.Count == 1 ? "Status.ImportedFromFileOne" : "Status.ImportedFromFileMany",
                InfoBarSeverity.Success, urls.Count);
        }
        catch (Exception ex)
        {
            SetRawStatus("Status.ImportFailedTitle", ex.Message, InfoBarSeverity.Error);
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
                SetStatus("Status.ImportFailedTitle", "Status.FileError", InfoBarSeverity.Error, Path.GetFileName(path), ex.Message);
            }
        }

        if (total > 0)
            SetStatus("Status.ImportTitle", total == 1 ? "Status.ImportedOne" : "Status.ImportedMany",
                InfoBarSeverity.Success, total);
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
        _previewDurationSeconds = null;
        _previewPlaylistCount = null;
        _previewPlatform = string.Empty;
        OnPropertyChanged(nameof(PreviewMeta));
        SetStatus("Status.ReadyTitle", "Status.ReadyMessage", InfoBarSeverity.Informational);
    }

    [RelayCommand]
    private async Task PreviewFirstAsync()
    {
        if (_activeOperation is not null)
            return;

        var url = GetUniqueUrls(UrlsText).FirstOrDefault();
        if (url is null)
        {
            SetStatus("Status.PreviewTitle", "Status.PreviewNeedsLink", InfoBarSeverity.Informational);
            return;
        }

        _activeOperation = new CancellationTokenSource();
        var token = _activeOperation.Token;

        try
        {
            var engine = await _engineManager.EnsureAsync(m => SetStatus("Status.PreviewTitle", m, InfoBarSeverity.Informational), token);
            var browser = UseBrowserCookies ? SelectedBrowser : null;
            SetStatus("Status.PreviewTitle", "Status.AnalyzingLink", InfoBarSeverity.Informational);
            var preview = await _mediaService.PreviewAsync(engine, url, browser, token);

            PreviewTitle = preview.Title;
            _previewDurationSeconds = preview.DurationSeconds;
            _previewPlaylistCount = preview.PlaylistCount;
            _previewPlatform = preview.Platform;
            OnPropertyChanged(nameof(PreviewMeta));
            await LoadThumbnailAsync(preview.ThumbnailUrl, token);
            IsPreviewVisible = true;
            SetStatus("Status.PreviewTitle", "Status.PreviewLoaded", InfoBarSeverity.Success);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetRawStatus("Status.PreviewFailedTitle", GetUsefulError(ex), InfoBarSeverity.Warning);
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
            SetStatus("Status.NoLinkTitle", "Status.NoValidLink", InfoBarSeverity.Informational);
            return;
        }

        var folder = OutputFolder.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            SetStatus("Status.FolderRequiredTitle", "Status.ChooseDestination", InfoBarSeverity.Informational);
            return;
        }

        if (AllowPlaylists)
        {
            var confirm = new Wpf.Ui.Controls.MessageBox
            {
                Title = L("Dialog.PlaylistTitle"),
                Content = L("Dialog.PlaylistMessage", PlaylistLimit),
                PrimaryButtonText = L("Common.Continue"),
                CloseButtonText = L("Common.Cancel")
            };
            if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
                return;
        }

        var needsFfmpeg = RequiresFfmpeg();
        if (needsFfmpeg && !_ffmpegManager.IsInstalled)
        {
            var confirm = new Wpf.Ui.Controls.MessageBox
            {
                Title = L("Dialog.FfmpegTitle"),
                Content = L("Dialog.FfmpegMessage"),
                PrimaryButtonText = L("Common.Continue"),
                CloseButtonText = L("Common.Cancel")
            };
            if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
                return;
        }

        // If subtitles are requested and FFmpeg happens to already be installed
        // (for this or any other reason), ensure it up front so the very first
        // yt-dlp call can request SRT directly via --convert-subs. If FFmpeg is
        // NOT installed, nothing is asked here -- yt-dlp is given the chance to
        // produce SRT natively first, and FFmpeg is only ever offered afterward,
        // per item, if a resulting sidecar still isn't SRT (see FinalizeSubtitlesAsync).
        var subtitlesWantFfmpegIfAvailable = SubtitleModeIndex != 0 && _ffmpegManager.IsInstalled;

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
            SetStatus("Status.PreparingTitle", "Status.CheckingComponents", InfoBarSeverity.Informational);
            var engine = await _engineManager.EnsureAsync(m => SetStatus("Status.PreparingTitle", m, InfoBarSeverity.Informational), token);

            string? ffmpegDirectory = null;
            if (needsFfmpeg || subtitlesWantFfmpegIfAvailable)
            {
                // Already installed: FfmpegManager.EnsureAsync returns the verified local
                // directory immediately, with no network access.
                var ffmpegProgress = new Progress<double>(v => SetActiveProgress(v));
                ffmpegDirectory = await _ffmpegManager.EnsureAsync(m => SetStatus("Status.PreparingTitle", m, InfoBarSeverity.Informational), ffmpegProgress, token);
            }

            var settings = ReadSettings(ffmpegDirectory);
            var subtitleConversion = new SubtitleConversionState { FfmpegDirectory = ffmpegDirectory };
            var workItems = Queue.ToList();

            for (var i = 0; i < workItems.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var item = workItems[i];
                if (!Queue.Contains(item))
                    continue;

                var url = item.Url;
                item.State = QueueItemState.Active;
                item.ResetProgress();

                var prefix = $"{i + 1}/{workItems.Count}";
                var progress = new Progress<DownloadProgress>(item.ApplyProgress);

                try
                {
                    DownloadResult result;
                    try
                    {
                        result = await _mediaService.DownloadAsync(
                            engine, url, folder, settings, progress,
                            m => SetStatus(new LocalizedMessage("Status.DownloadTitle", prefix), m, InfoBarSeverity.Informational),
                            token);
                    }
                    catch (RequestedFormatUnavailableException formatError)
                    {
                        ffmpegDirectory = await EnsureAutoFallbackFfmpegAsync(token);
                        subtitleConversion.FfmpegDirectory = ffmpegDirectory;
                        settings = ReadSettings(ffmpegDirectory);
                        item.ResetProgress();
                        SetStatus(new LocalizedMessage("Status.DownloadTitle", prefix),
                            new LocalizedMessage("Status.AutoFallback"), InfoBarSeverity.Informational);
                        var fallbackResult = await _mediaService.DownloadAsync(
                            engine, url, folder, settings, progress,
                            m => SetStatus(new LocalizedMessage("Status.DownloadTitle", prefix), m, InfoBarSeverity.Informational),
                            token, useAutoFallback: true);
                        result = new DownloadResult(
                            formatError.PartialFiles.Concat(fallbackResult.Files)
                                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                            fallbackResult.SubtitleFiles);
                    }

                    var subtitleState = SubtitleResultState.None;
                    if (settings.SubtitleMode != SubtitleMode.Off)
                        (result, subtitleState) = await FinalizeSubtitlesAsync(result, settings, subtitleConversion, token);

                    if (settings.SubtitleMode == SubtitleMode.SubtitlesOnly)
                    {
                        // MediaService already throws when nothing was found, so reaching
                        // here means at least one subtitle file was written.
                        succeeded++;
                        item.State = QueueItemState.Completed;
                        item.CompletedFileCount = result.SubtitleFiles.Count;
                        item.DisplayText = string.Join(", ", result.SubtitleFiles.Select(Path.GetFileName));
                        item.DetailText = item.DisplayText;
                        item.SubtitleResultState = subtitleState;
                    }
                    else if (result.Files.Count == 0 && settings.UseHistory)
                    {
                        skipped++;
                        item.State = QueueItemState.AlreadyDownloaded;
                    }
                    else
                    {
                        succeeded++;
                        item.State = QueueItemState.Completed;
                        item.CompletedFileCount = result.Files.Count;
                        if (result.Files.Count > 0)
                        {
                            item.DisplayText = string.Join(", ", result.Files.Select(Path.GetFileName));
                            item.DetailText = item.DisplayText;
                        }

                        if (settings.SubtitleMode == SubtitleMode.WithMedia)
                            item.SubtitleResultState = subtitleState;
                    }
                }
                catch (OperationCanceledException)
                {
                    item.State = QueueItemState.Cancelled;
                    throw;
                }
                catch (Exception ex)
                {
                    var message = GetUsefulError(ex);
                    _lastErrors.Add(new DownloadError(url, item.Platform, message));
                    item.State = QueueItemState.Failed;
                    item.DetailText = message;
                    SetStatus(new LocalizedMessage("Status.DownloadTitle", prefix),
                        new LocalizedMessage("Status.FailedContinue"), InfoBarSeverity.Warning);
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            foreach (var item in Queue.Where(q => q.State == QueueItemState.Waiting))
            {
                item.State = QueueItemState.Cancelled;
            }
        }
        catch (Exception ex)
        {
            SetRawStatus("Common.Error", GetUsefulError(ex), InfoBarSeverity.Error);
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
            SetStatus("Status.QueueCancelledTitle", "Status.SummaryWithErrors", InfoBarSeverity.Warning,
                FormatResultCount(succeeded, "Status.SucceededOne", "Status.SucceededMany"),
                FormatResultCount(skipped, "Status.AlreadyPresentOne", "Status.AlreadyPresentMany"),
                FormatResultCount(_lastErrors.Count, "Status.FailedOne", "Status.FailedMany"));
        }
        else if (_lastErrors.Count == 0)
        {
            SetStatus("Status.CompletedTitle", "Status.SummaryNoErrors", InfoBarSeverity.Success,
                FormatResultCount(succeeded, "Status.SucceededOne", "Status.SucceededMany"),
                FormatResultCount(skipped, "Status.AlreadyPresentOne", "Status.AlreadyPresentMany"));
        }
        else
        {
            SetStatus("Status.CompletedWithErrorsTitle", "Status.SummaryWithErrors", InfoBarSeverity.Warning,
                FormatResultCount(succeeded, "Status.SucceededOne", "Status.SucceededMany"),
                FormatResultCount(skipped, "Status.AlreadyPresentOne", "Status.AlreadyPresentMany"),
                FormatResultCount(_lastErrors.Count, "Status.FailedOne", "Status.FailedMany"));
        }
    }

    private void SetActiveProgress(double value)
    {
        var active = Queue.FirstOrDefault(q => q.State == QueueItemState.Active);
        if (active is not null)
            active.Progress = Math.Clamp(value, 0, 100);
    }

    private async Task<string> EnsureAutoFallbackFfmpegAsync(CancellationToken cancellationToken)
    {
        if (!_ffmpegManager.IsInstalled)
        {
            var confirm = new Wpf.Ui.Controls.MessageBox
            {
                Title = L("Dialog.FfmpegTitle"),
                Content = L("Dialog.AutoFallbackFfmpegMessage"),
                PrimaryButtonText = L("Common.Continue"),
                CloseButtonText = L("Common.Cancel")
            };
            if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
                throw new LocalizedException("Service.AutoFallbackDeclined");
        }

        var ffmpegProgress = new Progress<double>(SetActiveProgress);
        var directory = await _ffmpegManager.EnsureAsync(
            message => SetStatus("Status.PreparingTitle", message, InfoBarSeverity.Informational),
            ffmpegProgress, cancellationToken);
        SetFfmpegState(DependencyState.UpToDate);
        return directory;
    }

    [RelayCommand]
    private void CopyErrors()
    {
        if (_lastErrors.Count == 0)
            return;

        var text = new StringBuilder(L("Status.ErrorReportHeading") + "\r\n\r\n");
        foreach (var error in _lastErrors)
            text.AppendLine($"[{error.Platform}] {error.Url}\r\n{error.Message}\r\n");

        try
        {
            Clipboard.SetText(text.ToString());
            SetStatus("Status.CopiedTitle", "Status.ErrorReportCopied", InfoBarSeverity.Success);
        }
        catch
        {
            SetStatus("Status.ClipboardTitle", "Status.ErrorReportCopyFailed", InfoBarSeverity.Warning);
        }
    }

    [RelayCommand]
    private void RemoveQueueItem(QueueItemViewModel? item)
    {
        if (item is null || item.State == QueueItemState.Active)
            return;

        Queue.Remove(item);
        _lastErrors.RemoveAll(error => string.Equals(error.Url, item.Url, StringComparison.Ordinal));
        HasErrors = _lastErrors.Count > 0;
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
            Title = L("Output.ChooseFolderTitle"),
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
            SetStatus("Status.HistoryTitle", "Status.HistoryAlreadyEmpty", InfoBarSeverity.Informational);
            return;
        }

        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title = L("Dialog.ClearHistoryTitle"),
            Content = L("Dialog.ClearHistoryMessage"),
            PrimaryButtonText = L("Common.Clear"),
            CloseButtonText = L("Common.Cancel")
        };

        if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        try
        {
            File.Delete(_archivePath);
            SetStatus("Status.HistoryTitle", "Status.HistoryCleared", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetRawStatus("Status.HistoryTitle", ex.Message, InfoBarSeverity.Error);
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

        var subtitleMode = ParseSubtitleMode(SubtitleModeIndex);
        var subtitleLanguage = ParseSubtitleLanguage(SubtitleLanguageIndex);
        if (subtitleLanguage == SubtitleLanguagePreference.Automatic)
        {
            subtitleLanguage = LocalizationService.CurrentLanguage == AppLanguage.French
                ? SubtitleLanguagePreference.French
                : SubtitleLanguagePreference.English;
        }

        // A subtitles-only request must never be recorded in, or suppressed by, the
        // media download archive: the user may already have the media and just want
        // the subtitle now, and fetching only the subtitle must not poison future
        // media-only re-downloads of the same URL.
        var useHistory = UseHistory && subtitleMode != SubtitleMode.SubtitlesOnly;

        return new DownloadSettings(
            mode, quality, AllowPlaylists, PlaylistLimit,
            useHistory, UseBrowserCookies ? SelectedBrowser : null,
            ffmpegDirectory, useHistory ? _archivePath : null,
            subtitleMode, subtitleLanguage, UseAutomaticSubtitleFallback,
            ConvertSubtitlesToSrt: subtitleMode != SubtitleMode.Off && ffmpegDirectory is not null);
    }

    private static SubtitleMode ParseSubtitleMode(int index) => index switch
    {
        1 => SubtitleMode.WithMedia,
        2 => SubtitleMode.SubtitlesOnly,
        _ => SubtitleMode.Off
    };

    private static SubtitleLanguagePreference ParseSubtitleLanguage(int index) => index switch
    {
        1 => SubtitleLanguagePreference.English,
        2 => SubtitleLanguagePreference.French,
        3 => SubtitleLanguagePreference.All,
        _ => SubtitleLanguagePreference.Automatic
    };

    private void PrepareQueue(IReadOnlyList<string> urls)
    {
        Queue.Clear();
        for (var i = 0; i < urls.Count; i++)
        {
            var url = urls[i];
            var platform = GetPlatform(url);
            Queue.Add(new QueueItemViewModel(url, platform, ShortenUrl(url), FormatIndex, QualityIndex, SubtitleModeIndex));
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

    private static string GetUsefulError(Exception exception)
    {
        if (exception is LocalizedException localized)
            return localized.Message;

        var message = exception.Message;
        var lines = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.LastOrDefault(line => line.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            ?? lines.LastOrDefault() ?? L("Status.DownloadFailed");
    }

    private static string FormatDuration(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }

    private static string L(string key, params object[] arguments) => LocalizationService.Get(key, arguments);

    private static LocalizedMessage FormatResultCount(int count, string singularKey, string pluralKey) =>
        new(count == 1 ? singularKey : pluralKey, count);

    /// <summary>Tracks the FFmpeg decision for subtitle conversion across one download batch, so the
    /// confirmation dialog is shown at most once and every item reuses the same answer.</summary>
    private sealed class SubtitleConversionState
    {
        public string? FfmpegDirectory;
        public bool Declined;
    }

    private async Task<(DownloadResult Result, SubtitleResultState State)> FinalizeSubtitlesAsync(
        DownloadResult result, DownloadSettings settings, SubtitleConversionState conversionState, CancellationToken cancellationToken)
    {
        if (result.SubtitleFiles.Count == 0)
        {
            return (result, settings.SubtitleLanguage == SubtitleLanguagePreference.All
                ? SubtitleResultState.NoSubtitlesAvailable
                : SubtitleResultState.NoSubtitlesAvailableLanguage);
        }

        var files = result.SubtitleFiles.ToList();
        var needsConversion = files.Any(file => !IsSrt(file));

        if (needsConversion && conversionState.FfmpegDirectory is null && !conversionState.Declined)
        {
            if (_ffmpegManager.IsInstalled)
            {
                // Became available since the batch started (e.g. a prior item's format
                // fallback installed it); reuse it instead of asking again.
                conversionState.FfmpegDirectory = await _ffmpegManager.EnsureAsync(_ => { }, null, cancellationToken);
            }
            else
            {
                var confirm = new Wpf.Ui.Controls.MessageBox
                {
                    Title = L("Dialog.SubtitleFfmpegTitle"),
                    Content = L("Dialog.SubtitleFfmpegMessage"),
                    PrimaryButtonText = L("Common.Continue"),
                    CloseButtonText = L("Dialog.KeepOriginalFormat")
                };
                if (await confirm.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    var ffmpegProgress = new Progress<double>(SetActiveProgress);
                    conversionState.FfmpegDirectory = await _ffmpegManager.EnsureAsync(
                        m => SetStatus("Status.PreparingTitle", m, InfoBarSeverity.Informational), ffmpegProgress, cancellationToken);
                    SetFfmpegState(DependencyState.UpToDate);
                }
                else
                {
                    conversionState.Declined = true;
                }
            }
        }

        if (needsConversion && conversionState.FfmpegDirectory is not null)
        {
            for (var i = 0; i < files.Count; i++)
            {
                if (IsSrt(files[i]))
                    continue;

                var converted = await MediaService.ConvertSubtitleToSrtAsync(conversionState.FfmpegDirectory, files[i], cancellationToken);
                if (converted is not null)
                    files[i] = converted;
            }
        }

        var finalResult = new DownloadResult(result.Files, files);
        var state = files.All(IsSrt) ? SubtitleResultState.Saved : SubtitleResultState.ConversionRequiresFfmpeg;
        return (finalResult, state);
    }

    private static bool IsSrt(string path) => string.Equals(Path.GetExtension(path), ".srt", StringComparison.OrdinalIgnoreCase);

    private static string LocalizeDependencyState(DependencyState state) => L(state switch
    {
        DependencyState.UpToDate => "Dependency.UpToDate",
        DependencyState.NotInstalled => "Dependency.NotInstalled",
        DependencyState.Unavailable => "Dependency.Unavailable",
        DependencyState.Offline => "Dependency.Offline",
        _ => "Dependency.Checking"
    });

    private void SetYtDlpState(DependencyState state)
    {
        _ytDlpState = state;
        OnPropertyChanged(nameof(YtDlpStatusText));
        OnPropertyChanged(nameof(DependencySummaryText));
    }

    private void SetFfmpegState(DependencyState state)
    {
        _ffmpegState = state;
        OnPropertyChanged(nameof(FfmpegStatusText));
        OnPropertyChanged(nameof(DependencySummaryText));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(QueueSummaryText));
        OnPropertyChanged(nameof(LinkSummaryText));
        OnPropertyChanged(nameof(OutputFolderDisplay));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(PreviewMeta));
        OnPropertyChanged(nameof(YtDlpStatusText));
        OnPropertyChanged(nameof(FfmpegStatusText));
        OnPropertyChanged(nameof(DependencySummaryText));
        OnPropertyChanged(nameof(InfoBarTitle));
        OnPropertyChanged(nameof(InfoBarMessage));
        OnPropertyChanged(nameof(UpdateAvailableText));
        OnPropertyChanged(nameof(UpdateStatusText));

        foreach (var item in Queue)
            item.RefreshLocalization();
    }

    public void Dispose()
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _lifetimeCancellation.Dispose();
    }
}
