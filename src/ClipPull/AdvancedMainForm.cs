using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using ClipPull.Models;
using ClipPull.Services;
using ClipPull.Localization;

namespace ClipPull;

internal sealed partial class AdvancedMainForm : Form
{
    private static readonly HttpClient PreviewHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly TextBox _urlsBox = new();
    private readonly TextBox _folderBox = new();
    private readonly Button _pasteButton = new();
    private readonly Button _importButton = new();
    private readonly Button _previewButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _browseButton = new();
    private readonly Button _downloadButton = new();
    private readonly Button _retryButton = new();
    private readonly Button _copyErrorsButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _clearHistoryButton = new();
    private readonly CheckBox _cookiesCheck = new();
    private readonly CheckBox _playlistCheck = new();
    private readonly CheckBox _historyCheck = new();
    private readonly ComboBox _browserBox = new();
    private readonly ComboBox _modeBox = new();
    private readonly ComboBox _qualityBox = new();
    private readonly NumericUpDown _playlistLimit = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();
    private readonly Label _linkCountLabel = new();
    private readonly ListView _queueView = new();
    private readonly PictureBox _thumbnailBox = new();
    private readonly Label _previewTitle = new();
    private readonly Label _previewMeta = new();

    private readonly YtDlpManager _engineManager = new();
    private readonly FfmpegManager _ffmpegManager = new();
    private readonly MediaService _mediaService = new();
    private readonly List<DownloadError> _lastErrors = [];
    private CancellationTokenSource? _activeOperation;

    private readonly string _archivePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipPull", "download-archive.txt");

    public AdvancedMainForm()
    {
        Text = "ClipPull 0.5";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(880, 700);
        ClientSize = new Size(1000, 790);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Color.FromArgb(248, 249, 250);
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        BuildInterface();
        Shown += (_, _) => PrefillClipboardUrls();
        FormClosing += (_, _) =>
        {
            _activeOperation?.Cancel();
            _thumbnailBox.Image?.Dispose();
        };
        DragEnter += AdvancedMainForm_DragEnter;
        DragDrop += AdvancedMainForm_DragDrop;
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 1,
            RowCount = 10
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildUrlSection(), 0, 1);
        root.Controls.Add(BuildOptions(), 0, 2);
        root.Controls.Add(BuildQueue(), 0, 3);
        root.Controls.Add(BuildPreview(), 0, 4);
        root.Controls.Add(BuildFolderSection(), 0, 5);
        root.Controls.Add(BuildActions(), 0, 6);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Style = ProgressBarStyle.Continuous;
        root.Controls.Add(_progressBar, 0, 7);
        root.Controls.Add(BuildStatus(), 0, 8);

        root.Controls.Add(new Label
        {
            Text = "Local : aucun compte ClipPull, aucune télémétrie. Télécharge seulement le contenu que tu as le droit de conserver.",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.2F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 9);

        Controls.Add(root);
        AcceptButton = _downloadButton;
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.Controls.Add(new Label
        {
            Text = "ClipPull",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 21F),
            ForeColor = Color.FromArgb(32, 33, 36),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        panel.Controls.Add(new Label
        {
            Text = "Facebook • TikTok • Instagram • YouTube • X • Vimeo • et plus",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(85, 85, 85),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        return panel;
    }

    private Control BuildUrlSection()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new Label { Text = "Liens — un par ligne", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        _linkCountLabel.Text = "0 lien";
        _linkCountLabel.AutoSize = true;
        _linkCountLabel.ForeColor = Color.DimGray;
        header.Controls.Add(_linkCountLabel, 1, 0);

        _urlsBox.Dock = DockStyle.Fill;
        _urlsBox.Multiline = true;
        _urlsBox.AcceptsReturn = true;
        _urlsBox.ScrollBars = ScrollBars.Vertical;
        _urlsBox.PlaceholderText = "Colle un ou plusieurs liens ici, ou glisse un fichier .txt dans la fenêtre...";
        _urlsBox.TextChanged += (_, _) => UpdateLinkCount();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
        ConfigureButton(_pasteButton, "Coller", (_, _) => PasteClipboardUrls());
        ConfigureButton(_importButton, "Importer .txt", async (_, _) => await ImportTextFileAsync());
        ConfigureButton(_previewButton, "Aperçu du 1er lien", async (_, _) => await PreviewFirstAsync());
        ConfigureButton(_clearButton, "Effacer", (_, _) => ClearQueue());
        buttons.Controls.AddRange([_pasteButton, _importButton, _previewButton, _clearButton]);

        panel.Controls.Add(header, 0, 0);
        panel.Controls.Add(_urlsBox, 0, 1);
        panel.Controls.Add(buttons, 0, 2);
        return panel;
    }

    private Control BuildOptions()
    {
        var group = new GroupBox { Text = "Options", Dock = DockStyle.Fill, Padding = new Padding(10, 18, 10, 8) };
        var rows = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var first = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        first.Controls.Add(new Label { Text = "Format :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        _modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeBox.Width = 125;
        _modeBox.Items.AddRange(["Vidéo", "Audio M4A", "Audio MP3"]);
        _modeBox.SelectedIndex = 0;
        _modeBox.SelectedIndexChanged += (_, _) => _qualityBox.Enabled = _modeBox.SelectedIndex == 0 && _activeOperation is null;
        first.Controls.Add(_modeBox);

        first.Controls.Add(new Label { Text = "Qualité :", AutoSize = true, Margin = new Padding(14, 8, 4, 0) });
        _qualityBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _qualityBox.Width = 155;
        _qualityBox.Items.AddRange(["Auto (rapide)", "Meilleure", "1080p max", "720p max", "480p max", "Petit fichier"]);
        _qualityBox.SelectedIndex = 0;
        first.Controls.Add(_qualityBox);

        _playlistCheck.Text = "Autoriser les playlists";
        _playlistCheck.AutoSize = true;
        _playlistCheck.Margin = new Padding(16, 7, 4, 0);
        _playlistCheck.CheckedChanged += (_, _) => _playlistLimit.Enabled = _playlistCheck.Checked && _activeOperation is null;
        first.Controls.Add(_playlistCheck);
        first.Controls.Add(new Label { Text = "max", AutoSize = true, Margin = new Padding(4, 8, 3, 0) });
        _playlistLimit.Minimum = 1;
        _playlistLimit.Maximum = 500;
        _playlistLimit.Value = 50;
        _playlistLimit.Width = 58;
        _playlistLimit.Enabled = false;
        first.Controls.Add(_playlistLimit);

        var second = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _historyCheck.Text = "Éviter les doublons avec l'historique local";
        _historyCheck.AutoSize = true;
        _historyCheck.Checked = true;
        _historyCheck.Margin = new Padding(0, 7, 4, 0);
        second.Controls.Add(_historyCheck);

        ConfigureButton(_clearHistoryButton, "Effacer historique", (_, _) => ClearHistory());
        second.Controls.Add(_clearHistoryButton);

        _cookiesCheck.Text = "Utiliser ma session navigateur si nécessaire";
        _cookiesCheck.AutoSize = true;
        _cookiesCheck.Margin = new Padding(16, 7, 4, 0);
        _cookiesCheck.CheckedChanged += (_, _) => _browserBox.Enabled = _cookiesCheck.Checked && _activeOperation is null;
        second.Controls.Add(_cookiesCheck);
        _browserBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _browserBox.Width = 105;
        _browserBox.Items.AddRange(["Chrome", "Edge", "Firefox", "Brave", "Chromium", "Opera", "Vivaldi"]);
        _browserBox.SelectedIndex = 0;
        _browserBox.Enabled = false;
        second.Controls.Add(_browserBox);

        rows.Controls.Add(first, 0, 0);
        rows.Controls.Add(second, 0, 1);
        group.Controls.Add(rows);
        return group;
    }

    private Control BuildQueue()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 5, 0, 5) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = "File de téléchargement", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 0);

        _queueView.Dock = DockStyle.Fill;
        _queueView.View = View.Details;
        _queueView.FullRowSelect = true;
        _queueView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _queueView.ShowItemToolTips = true;
        _queueView.Columns.Add("#", 42);
        _queueView.Columns.Add("Plateforme", 105);
        _queueView.Columns.Add("Statut", 170);
        _queueView.Columns.Add("Lien", 610);
        panel.Controls.Add(_queueView, 0, 1);
        return panel;
    }

    private Control BuildPreview()
    {
        var group = new GroupBox { Text = "Aperçu", Dock = DockStyle.Fill, Padding = new Padding(10, 18, 10, 8) };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _thumbnailBox.Dock = DockStyle.Fill;
        _thumbnailBox.SizeMode = PictureBoxSizeMode.Zoom;
        _thumbnailBox.BackColor = Color.FromArgb(235, 235, 235);
        panel.Controls.Add(_thumbnailBox, 0, 0);

        var text = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10, 3, 0, 0) };
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        _previewTitle.Text = "Clique « Aperçu du 1er lien » pour voir le titre et la miniature.";
        _previewTitle.Dock = DockStyle.Fill;
        _previewTitle.Font = new Font("Segoe UI Semibold", 10.5F);
        _previewTitle.AutoEllipsis = true;
        _previewTitle.TextAlign = ContentAlignment.MiddleLeft;
        _previewMeta.Dock = DockStyle.Fill;
        _previewMeta.ForeColor = Color.DimGray;
        _previewMeta.TextAlign = ContentAlignment.TopLeft;
        text.Controls.Add(_previewTitle, 0, 0);
        text.Controls.Add(_previewMeta, 0, 1);
        panel.Controls.Add(text, 1, 0);
        group.Controls.Add(panel);
        return group;
    }

    private Control BuildFolderSection()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.Controls.Add(new Label { Text = "Enregistrer dans", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        panel.SetColumnSpan(panel.GetControlFromPosition(0, 0)!, 2);
        _folderBox.Dock = DockStyle.Fill;
        _folderBox.Text = GetDefaultOutputFolder();
        ConfigureButton(_browseButton, "Choisir...", (_, _) => ChooseFolder());
        _browseButton.Margin = new Padding(8, 3, 0, 3);
        panel.Controls.Add(_folderBox, 0, 1);
        panel.Controls.Add(_browseButton, 1, 1);
        return panel;
    }

    private Control BuildActions()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _downloadButton.Text = "Télécharger tout";
        _downloadButton.Dock = DockStyle.Fill;
        _downloadButton.Font = new Font("Segoe UI Semibold", 10.5F);
        _downloadButton.Click += async (_, _) =>
        {
            if (_activeOperation is not null) _activeOperation.Cancel();
            else await StartDownloadsAsync();
        };

        ConfigureButton(_retryButton, "Réessayer les échecs", async (_, _) => await RetryErrorsAsync());
        _retryButton.Enabled = false;
        ConfigureButton(_copyErrorsButton, "Copier erreurs", (_, _) => CopyErrors());
        _copyErrorsButton.Enabled = false;
        ConfigureButton(_openFolderButton, "Ouvrir dossier", (_, _) => OpenOutputFolder());

        panel.Controls.Add(_downloadButton, 0, 0);
        panel.Controls.Add(_retryButton, 1, 0);
        panel.Controls.Add(_copyErrorsButton, 2, 0);
        panel.Controls.Add(_openFolderButton, 3, 0);
        return panel;
    }

    private Control BuildStatus()
    {
        _statusLabel.Text = "Prêt. Colle des liens ou glisse un fichier .txt.";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(75, 75, 75);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        return _statusLabel;
    }

    private async Task StartDownloadsAsync()
    {
        var urls = GetUniqueUrls(_urlsBox.Text);
        if (urls.Count == 0)
        {
            MessageBox.Show(this, "Ajoute au moins un lien web valide.", "Aucun lien", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var folder = _folderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(this, "Choisis un dossier de destination.", "Dossier requis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_playlistCheck.Checked)
        {
            var answer = MessageBox.Show(this,
                $"Le mode playlist est activé. ClipPull autorisera jusqu'à {(int)_playlistLimit.Value} éléments PAR lien de playlist.\n\nContinuer?",
                "Confirmer les playlists", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
                return;
        }

        var needsFfmpeg = RequiresFfmpeg();
        if (needsFfmpeg && !_ffmpegManager.IsInstalled)
        {
            var answer = MessageBox.Show(this,
                "Cette option nécessite FFmpeg. Au premier usage, ClipPull téléchargera environ 140 Mo depuis le projet BtbN/FFmpeg-Builds, puis vérifiera le SHA-256 avant extraction.\n\nContinuer?",
                "FFmpeg requis", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer != DialogResult.Yes)
                return;
        }

        _activeOperation = new CancellationTokenSource();
        var token = _activeOperation.Token;
        _lastErrors.Clear();
        PrepareQueue(urls);
        SetBusy(true);
        var succeeded = 0;
        var skipped = 0;
        var cancelled = false;

        try
        {
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Path.GetDirectoryName(_archivePath)!);
            var engine = await _engineManager.EnsureAsync(message => SetStatusSafe($"Préparation • {message.Resolve()}"), token);
            string? ffmpegDirectory = null;
            if (needsFfmpeg)
            {
                var ffmpegProgress = new Progress<double>(v => SetProgress(v));
                ffmpegDirectory = await _ffmpegManager.EnsureAsync(message => SetStatusSafe($"Préparation • {message.Resolve()}"), ffmpegProgress, token);
            }

            var settings = ReadSettings(ffmpegDirectory);
            for (var i = 0; i < urls.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var url = urls[i];
                SetQueueStatus(i, "En cours...");
                SetProgress(0);
                var prefix = $"{i + 1}/{urls.Count}";
                var progress = new Progress<double>(SetProgress);

                try
                {
                    var result = await _mediaService.DownloadAsync(engine, url, folder, settings, progress,
                        message => SetStatusSafe($"{prefix} • {message.Resolve()}"), token);
                    if (result.Files.Count == 0 && settings.UseHistory)
                    {
                        skipped++;
                        SetQueueStatus(i, "Déjà téléchargé");
                    }
                    else
                    {
                        succeeded++;
                        SetQueueStatus(i, result.Files.Count > 1 ? $"Terminé ({result.Files.Count})" : "Terminé",
                            result.Files.Count > 0 ? string.Join(Environment.NewLine, result.Files.Select(Path.GetFileName)) : null);
                    }
                }
                catch (OperationCanceledException)
                {
                    SetQueueStatus(i, "Annulé");
                    throw;
                }
                catch (Exception ex)
                {
                    var message = GetUsefulError(ex.Message);
                    _lastErrors.Add(new DownloadError(url, GetPlatform(url), message));
                    SetQueueStatus(i, "Échec", message);
                    SetStatusSafe($"{prefix} • Échec; passage au suivant...");
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            MarkPendingCancelled();
        }
        finally
        {
            _activeOperation.Dispose();
            _activeOperation = null;
            SetBusy(false);
        }

        _retryButton.Enabled = _lastErrors.Count > 0;
        _copyErrorsButton.Enabled = _lastErrors.Count > 0;
        if (cancelled)
            SetStatusSafe($"File annulée • {succeeded} réussi(s), {skipped} déjà présent(s), {_lastErrors.Count} échec(s).");
        else
            SetStatusSafe($"Terminé • {succeeded} réussi(s), {skipped} déjà présent(s), {_lastErrors.Count} échec(s).");
    }

    private async Task PreviewFirstAsync()
    {
        if (_activeOperation is not null)
            return;
        var url = GetUniqueUrls(_urlsBox.Text).FirstOrDefault();
        if (url is null)
        {
            SetStatusSafe("Ajoute un lien avant de demander un aperçu.");
            return;
        }

        _activeOperation = new CancellationTokenSource();
        var token = _activeOperation.Token;
        SetBusy(true, allowCancel: false);
        try
        {
            var engine = await _engineManager.EnsureAsync(message => SetStatusSafe($"Aperçu • {message.Resolve()}"), token);
            var browser = _cookiesCheck.Checked ? _browserBox.SelectedItem?.ToString() : null;
            SetStatusSafe("Analyse de l'aperçu...");
            var preview = await _mediaService.PreviewAsync(engine, url, browser, token);
            _previewTitle.Text = preview.Title;
            var duration = preview.DurationSeconds is > 0 ? FormatDuration(preview.DurationSeconds.Value) : "durée inconnue";
            var playlist = preview.PlaylistCount is > 0 ? $" • playlist : {preview.PlaylistCount} élément(s)" : string.Empty;
            _previewMeta.Text = $"{preview.Platform} • {duration}{playlist}";
            await LoadThumbnailAsync(preview.ThumbnailUrl, token);
            SetStatusSafe("Aperçu chargé.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetStatusSafe("Impossible de charger l'aperçu.");
            MessageBox.Show(this, GetUsefulError(ex.Message), "Aperçu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            _activeOperation.Dispose();
            _activeOperation = null;
            SetBusy(false);
        }
    }

    private async Task LoadThumbnailAsync(string? url, CancellationToken cancellationToken)
    {
        _thumbnailBox.Image?.Dispose();
        _thumbnailBox.Image = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            return;
        try
        {
            var bytes = await PreviewHttp.GetByteArrayAsync(uri, cancellationToken);
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream);
            _thumbnailBox.Image = new Bitmap(image);
        }
        catch { }
    }

    private DownloadSettings ReadSettings(string? ffmpegDirectory)
    {
        var mode = _modeBox.SelectedIndex switch { 1 => MediaMode.AudioM4a, 2 => MediaMode.AudioMp3, _ => MediaMode.Video };
        var quality = _qualityBox.SelectedIndex switch
        {
            1 => VideoQuality.Best,
            2 => VideoQuality.P1080,
            3 => VideoQuality.P720,
            4 => VideoQuality.P480,
            5 => VideoQuality.Small,
            _ => VideoQuality.Auto
        };
        return new DownloadSettings(mode, quality, _playlistCheck.Checked, (int)_playlistLimit.Value,
            _historyCheck.Checked, _cookiesCheck.Checked ? _browserBox.SelectedItem?.ToString() : null,
            ffmpegDirectory, _historyCheck.Checked ? _archivePath : null);
    }

    private bool RequiresFfmpeg() => _modeBox.SelectedIndex != 0 || _qualityBox.SelectedIndex is >= 1 and <= 4;

    private void PrepareQueue(IReadOnlyList<string> urls)
    {
        _queueView.BeginUpdate();
        try
        {
            _queueView.Items.Clear();
            for (var i = 0; i < urls.Count; i++)
            {
                var item = new ListViewItem((i + 1).ToString());
                item.SubItems.Add(GetPlatform(urls[i]));
                item.SubItems.Add("En attente");
                item.SubItems.Add(urls[i]);
                _queueView.Items.Add(item);
            }
        }
        finally { _queueView.EndUpdate(); }
    }

    private void SetQueueStatus(int index, string status, string? tooltip = null)
    {
        if (index < 0 || index >= _queueView.Items.Count) return;
        var item = _queueView.Items[index];
        item.SubItems[2].Text = status;
        item.ToolTipText = string.IsNullOrWhiteSpace(tooltip) ? status : tooltip;
        item.EnsureVisible();
    }

    private void MarkPendingCancelled()
    {
        foreach (ListViewItem item in _queueView.Items)
            if (item.SubItems[2].Text == "En attente") item.SubItems[2].Text = "Non démarré";
    }

    private void SetBusy(bool busy, bool allowCancel = true)
    {
        _urlsBox.Enabled = !busy;
        _folderBox.Enabled = !busy;
        _pasteButton.Enabled = !busy;
        _importButton.Enabled = !busy;
        _previewButton.Enabled = !busy;
        _clearButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _modeBox.Enabled = !busy;
        _qualityBox.Enabled = !busy && _modeBox.SelectedIndex == 0;
        _playlistCheck.Enabled = !busy;
        _playlistLimit.Enabled = !busy && _playlistCheck.Checked;
        _historyCheck.Enabled = !busy;
        _clearHistoryButton.Enabled = !busy;
        _cookiesCheck.Enabled = !busy;
        _browserBox.Enabled = !busy && _cookiesCheck.Checked;
        _retryButton.Enabled = !busy && _lastErrors.Count > 0;
        _copyErrorsButton.Enabled = !busy && _lastErrors.Count > 0;
        _downloadButton.Enabled = !busy || allowCancel;
        _downloadButton.Text = busy && allowCancel ? "Annuler" : "Télécharger tout";
    }

    private async Task RetryErrorsAsync()
    {
        if (_lastErrors.Count == 0 || _activeOperation is not null) return;
        _urlsBox.Lines = _lastErrors.Select(x => x.Url).Distinct(StringComparer.Ordinal).ToArray();
        await StartDownloadsAsync();
    }

    private void CopyErrors()
    {
        if (_lastErrors.Count == 0) return;
        var text = new StringBuilder("ClipPull - rapport d'erreurs\r\n\r\n");
        foreach (var error in _lastErrors)
            text.AppendLine($"[{error.Platform}] {error.Url}\r\n{error.Message}\r\n");
        try
        {
            Clipboard.SetText(text.ToString());
            SetStatusSafe("Rapport d'erreurs copié dans le presse-papiers.");
        }
        catch { SetStatusSafe("Impossible de copier le rapport d'erreurs."); }
    }

    private void ClearHistory()
    {
        if (!File.Exists(_archivePath))
        {
            SetStatusSafe("L'historique est déjà vide.");
            return;
        }
        if (MessageBox.Show(this, "Effacer l'historique local des téléchargements? Les fichiers déjà téléchargés ne seront pas supprimés.",
            "Effacer l'historique", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            File.Delete(_archivePath);
            SetStatusSafe("Historique local effacé.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Historique", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ClearQueue()
    {
        _urlsBox.Clear();
        _queueView.Items.Clear();
        _lastErrors.Clear();
        _retryButton.Enabled = false;
        _copyErrorsButton.Enabled = false;
        SetProgress(0);
        _previewTitle.Text = "Clique « Aperçu du 1er lien » pour voir le titre et la miniature.";
        _previewMeta.Text = string.Empty;
        _thumbnailBox.Image?.Dispose();
        _thumbnailBox.Image = null;
        SetStatusSafe("Prêt. Colle des liens ou glisse un fichier .txt.");
    }

    private void AdvancedMainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files?.Any(path => string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)) == true)
                e.Effect = DragDropEffects.Copy;
        }
    }

    private async void AdvancedMainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var path in files.Where(path => string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)))
        {
            try { AddUrls(GetUniqueUrls(await File.ReadAllTextAsync(path))); }
            catch (Exception ex) { SetStatusSafe($"Impossible de lire {Path.GetFileName(path)} : {ex.Message}"); }
        }
    }

    private async Task ImportTextFileAsync()
    {
        using var dialog = new OpenFileDialog { Title = "Importer une liste de liens", Filter = "Fichiers texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var urls = GetUniqueUrls(await File.ReadAllTextAsync(dialog.FileName));
            AddUrls(urls);
            SetStatusSafe($"{urls.Count} lien(s) trouvé(s) dans le fichier.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Import", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void PrefillClipboardUrls()
    {
        if (string.IsNullOrWhiteSpace(_urlsBox.Text)) PasteClipboardUrls(true);
    }

    private void PasteClipboardUrls(bool silent = false)
    {
        try
        {
            var urls = GetUniqueUrls(Clipboard.GetText());
            if (urls.Count > 0) AddUrls(urls);
            else if (!silent) SetStatusSafe("Le presse-papiers ne contient aucun lien web valide.");
        }
        catch { if (!silent) SetStatusSafe("Impossible de lire le presse-papiers."); }
    }

    private void AddUrls(IEnumerable<string> incoming)
    {
        var urls = GetUniqueUrls(_urlsBox.Text).ToList();
        var seen = new HashSet<string>(urls, StringComparer.Ordinal);
        foreach (var url in incoming)
            if (seen.Add(url)) urls.Add(url);
        _urlsBox.Lines = urls.ToArray();
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choisir le dossier de téléchargement",
            SelectedPath = Directory.Exists(_folderBox.Text) ? _folderBox.Text : GetDefaultOutputFolder(),
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _folderBox.Text = dialog.SelectedPath;
    }

    private void OpenOutputFolder()
    {
        var folder = _folderBox.Text.Trim();
        if (!Directory.Exists(folder)) return;
        var info = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = false };
        info.ArgumentList.Add(folder);
        Process.Start(info);
    }

    private void UpdateLinkCount()
    {
        var count = GetUniqueUrls(_urlsBox.Text).Count;
        _linkCountLabel.Text = count == 1 ? "1 lien" : $"{count} liens";
    }

    private void SetProgress(double value)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => SetProgress(value)); return; }
        _progressBar.Value = (int)Math.Clamp(Math.Round(value), 0, 100);
    }

    private void SetStatusSafe(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => SetStatusSafe(text)); return; }
        _statusLabel.Text = text;
    }

    private static void ConfigureButton(Button button, string text, EventHandler handler)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Click += handler;
    }

    private static IReadOnlyList<string> GetUniqueUrls(string text)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in UrlRegex().Matches(text ?? string.Empty))
        {
            var value = match.Value.TrimEnd('.', ',', ';', ')', ']', '}');
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https") && seen.Add(value))
                result.Add(value);
        }
        return result;
    }

    private static string GetPlatform(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return "Web";
        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("facebook.com") || host.Contains("fb.watch")) return "Facebook";
        if (host.Contains("instagram.com")) return "Instagram";
        if (host.Contains("tiktok.com")) return "TikTok";
        if (host.Contains("youtube.com") || host.Contains("youtu.be")) return "YouTube";
        if (host == "x.com" || host.EndsWith(".x.com") || host.Contains("twitter.com")) return "X / Twitter";
        if (host.Contains("vimeo.com")) return "Vimeo";
        return host.StartsWith("www.") ? host[4..] : host;
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

    private static string GetDefaultOutputFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ClipPull");

    [GeneratedRegex("https?://[^\\s<>\\\"']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}
