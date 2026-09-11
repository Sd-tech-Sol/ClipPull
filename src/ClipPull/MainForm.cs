using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ClipPull.Services;

namespace ClipPull;

internal sealed partial class MainForm : Form
{
    private readonly TextBox _urlsBox = new();
    private readonly TextBox _folderBox = new();
    private readonly Button _pasteButton = new();
    private readonly Button _importButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _browseButton = new();
    private readonly Button _downloadButton = new();
    private readonly Button _retryButton = new();
    private readonly Button _openFolderButton = new();
    private readonly CheckBox _cookiesCheck = new();
    private readonly ComboBox _browserBox = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();
    private readonly Label _linkCountLabel = new();
    private readonly ListView _queueView = new();

    private readonly YtDlpManager _engineManager = new();
    private readonly DownloadService _downloadService = new();
    private readonly List<string> _failedUrls = [];
    private CancellationTokenSource? _activeDownload;

    public MainForm()
    {
        Text = "ClipPull";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 610);
        ClientSize = new Size(860, 680);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(248, 249, 250);
        AutoScaleMode = AutoScaleMode.Dpi;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // The embedded application icon is cosmetic only.
        }

        BuildInterface();
        Shown += (_, _) => PrefillClipboardUrls();
        FormClosing += (_, _) => _activeDownload?.Cancel();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(26),
            ColumnCount = 1,
            RowCount = 9
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildUrlSection(), 0, 1);
        root.Controls.Add(BuildQueueSection(), 0, 2);
        root.Controls.Add(BuildFolderSection(), 0, 3);
        root.Controls.Add(BuildBrowserSection(), 0, 4);
        root.Controls.Add(BuildActionRow(), 0, 5);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Style = ProgressBarStyle.Continuous;
        root.Controls.Add(_progressBar, 0, 6);

        root.Controls.Add(BuildStatusRow(), 0, 7);

        var privacy = new Label
        {
            Text = "Local : aucun compte ClipPull, aucune télémétrie. Télécharge seulement du contenu que tu as le droit de conserver.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(105, 105, 105),
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(privacy, 0, 8);

        Controls.Add(root);
        AcceptButton = _downloadButton;
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var title = new Label
        {
            Text = "ClipPull",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(32, 33, 36),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Text = "Facebook • TikTok • Instagram • YouTube • X • Vimeo • et plus",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(85, 85, 85),
            TextAlign = ContentAlignment.TopLeft
        };

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(subtitle, 0, 1);
        return panel;
    }

    private Control BuildUrlSection()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = "Liens vidéo — un par ligne",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };

        _linkCountLabel.Text = "0 lien";
        _linkCountLabel.AutoSize = true;
        _linkCountLabel.ForeColor = Color.FromArgb(95, 95, 95);
        _linkCountLabel.TextAlign = ContentAlignment.BottomRight;

        header.Controls.Add(label, 0, 0);
        header.Controls.Add(_linkCountLabel, 1, 0);

        _urlsBox.Dock = DockStyle.Fill;
        _urlsBox.Multiline = true;
        _urlsBox.AcceptsReturn = true;
        _urlsBox.ScrollBars = ScrollBars.Vertical;
        _urlsBox.PlaceholderText = "Colle un ou plusieurs liens ici...";
        _urlsBox.TextChanged += (_, _) => UpdateLinkCount();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0)
        };

        _pasteButton.Text = "Coller";
        _pasteButton.AutoSize = true;
        _pasteButton.Click += (_, _) => PasteClipboardUrls();

        _importButton.Text = "Importer .txt";
        _importButton.AutoSize = true;
        _importButton.Click += async (_, _) => await ImportTextFileAsync();

        _clearButton.Text = "Effacer";
        _clearButton.AutoSize = true;
        _clearButton.Click += (_, _) =>
        {
            _urlsBox.Clear();
            _queueView.Items.Clear();
            _failedUrls.Clear();
            _retryButton.Enabled = false;
            _progressBar.Value = 0;
            SetStatusSafe("Prêt. Colle un ou plusieurs liens vidéo.");
        };

        buttons.Controls.Add(_pasteButton);
        buttons.Controls.Add(_importButton);
        buttons.Controls.Add(_clearButton);

        panel.Controls.Add(header, 0, 0);
        panel.Controls.Add(_urlsBox, 0, 1);
        panel.Controls.Add(buttons, 0, 2);
        return panel;
    }

    private Control BuildQueueSection()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 6, 0, 6)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = "File de téléchargement",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };

        _queueView.Dock = DockStyle.Fill;
        _queueView.View = View.Details;
        _queueView.FullRowSelect = true;
        _queueView.GridLines = false;
        _queueView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _queueView.ShowItemToolTips = true;
        _queueView.Columns.Add("#", 42);
        _queueView.Columns.Add("Plateforme", 115);
        _queueView.Columns.Add("Statut", 150);
        _queueView.Columns.Add("Lien", 500);

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(_queueView, 0, 1);
        return panel;
    }

    private Control BuildFolderSection()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));

        var label = new Label
        {
            Text = "Enregistrer dans",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };

        _folderBox.Dock = DockStyle.Fill;
        _folderBox.Text = GetDefaultOutputFolder();

        _browseButton.Text = "Choisir...";
        _browseButton.AutoSize = true;
        _browseButton.Height = 30;
        _browseButton.Margin = new Padding(8, 3, 0, 3);
        _browseButton.Click += (_, _) => ChooseFolder();

        panel.Controls.Add(label, 0, 0);
        panel.SetColumnSpan(label, 2);
        panel.Controls.Add(_folderBox, 0, 1);
        panel.Controls.Add(_browseButton, 1, 1);
        return panel;
    }

    private Control BuildBrowserSection()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 7, 0, 0)
        };

        _cookiesCheck.Text = "Utiliser ma session du navigateur si nécessaire";
        _cookiesCheck.AutoSize = true;
        _cookiesCheck.CheckedChanged += (_, _) => _browserBox.Enabled = _cookiesCheck.Checked && _activeDownload is null;

        _browserBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _browserBox.Width = 115;
        _browserBox.Items.AddRange(["Chrome", "Edge", "Firefox", "Brave", "Chromium", "Opera", "Vivaldi"]);
        _browserBox.SelectedIndex = 0;
        _browserBox.Enabled = false;

        panel.Controls.Add(_cookiesCheck);
        panel.Controls.Add(_browserBox);
        return panel;
    }

    private Control BuildActionRow()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _downloadButton.Text = "Télécharger tout";
        _downloadButton.Dock = DockStyle.Fill;
        _downloadButton.Font = new Font("Segoe UI Semibold", 11F);
        _downloadButton.FlatStyle = FlatStyle.System;
        _downloadButton.Click += DownloadButton_Click;

        _retryButton.Text = "Réessayer les échecs";
        _retryButton.AutoSize = true;
        _retryButton.Enabled = false;
        _retryButton.Margin = new Padding(8, 3, 0, 3);
        _retryButton.Click += RetryButton_Click;

        panel.Controls.Add(_downloadButton, 0, 0);
        panel.Controls.Add(_retryButton, 1, 0);
        return panel;
    }

    private Control BuildStatusRow()
    {
        var statusRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusLabel.Text = "Prêt. Colle un ou plusieurs liens vidéo.";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(80, 80, 80);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        _openFolderButton.Text = "Ouvrir le dossier";
        _openFolderButton.AutoSize = true;
        _openFolderButton.Enabled = false;
        _openFolderButton.Click += (_, _) => OpenOutputFolder();

        statusRow.Controls.Add(_statusLabel, 0, 0);
        statusRow.Controls.Add(_openFolderButton, 1, 0);
        return statusRow;
    }

    private async void DownloadButton_Click(object? sender, EventArgs e)
    {
        if (_activeDownload is not null)
        {
            _activeDownload.Cancel();
            return;
        }

        var urls = GetUniqueUrls(_urlsBox.Text);
        if (urls.Count == 0)
        {
            MessageBox.Show(this, "Colle au moins une adresse web valide qui commence par http:// ou https://.", "Aucun lien valide", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _urlsBox.Focus();
            return;
        }

        var folder = _folderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(this, "Choisis un dossier où enregistrer les vidéos.", "Dossier requis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _activeDownload = new CancellationTokenSource();
        var token = _activeDownload.Token;
        _failedUrls.Clear();
        PrepareQueue(urls);
        SetBusy(true);

        var succeeded = 0;
        var cancelled = false;

        try
        {
            Directory.CreateDirectory(folder);
            var enginePath = await _engineManager.EnsureAsync(message => SetStatusSafe($"Préparation • {message}"), token);
            var browser = _cookiesCheck.Checked ? _browserBox.SelectedItem?.ToString() : null;

            for (var index = 0; index < urls.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var url = urls[index];
                _progressBar.Value = 0;
                SetQueueStatus(index, "En cours...");

                var prefix = $"{index + 1}/{urls.Count}";
                var progress = new Progress<double>(value =>
                {
                    _progressBar.Value = (int)Math.Clamp(Math.Round(value), 0, 100);
                });

                try
                {
                    var file = await _downloadService.DownloadAsync(
                        enginePath,
                        url,
                        folder,
                        browser,
                        progress,
                        message => SetStatusSafe($"{prefix} • {message}"),
                        token);

                    succeeded++;
                    SetQueueStatus(index, "Terminé", file is null ? null : Path.GetFileName(file));
                    _openFolderButton.Enabled = true;
                }
                catch (OperationCanceledException)
                {
                    SetQueueStatus(index, "Annulé");
                    throw;
                }
                catch (Exception ex)
                {
                    _failedUrls.Add(url);
                    SetQueueStatus(index, "Échec", GetUsefulError(ex.Message));
                    SetStatusSafe($"{prefix} • Échec; passage au lien suivant...");
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        finally
        {
            _activeDownload.Dispose();
            _activeDownload = null;
            SetBusy(false);
            _retryButton.Enabled = _failedUrls.Count > 0;
        }

        if (cancelled)
        {
            SetStatusSafe($"File annulée. {succeeded} téléchargement(s) terminé(s).");
            MarkRemainingAsCancelled();
            return;
        }

        _progressBar.Value = succeeded > 0 ? 100 : 0;
        if (_failedUrls.Count == 0)
        {
            SetStatusSafe($"Terminé : {succeeded}/{urls.Count} téléchargement(s) réussi(s).");
        }
        else
        {
            SetStatusSafe($"Terminé : {succeeded} réussi(s), {_failedUrls.Count} échec(s). Tu peux réessayer seulement les échecs.");
        }
    }

    private void RetryButton_Click(object? sender, EventArgs e)
    {
        if (_activeDownload is not null || _failedUrls.Count == 0)
            return;

        _urlsBox.Lines = _failedUrls.ToArray();
        DownloadButton_Click(_downloadButton, EventArgs.Empty);
    }

    private void PrepareQueue(IReadOnlyList<string> urls)
    {
        _queueView.BeginUpdate();
        try
        {
            _queueView.Items.Clear();
            for (var i = 0; i < urls.Count; i++)
            {
                var url = urls[i];
                var item = new ListViewItem((i + 1).ToString());
                item.SubItems.Add(GetPlatform(url));
                item.SubItems.Add("En attente");
                item.SubItems.Add(url);
                item.Tag = url;
                _queueView.Items.Add(item);
            }
        }
        finally
        {
            _queueView.EndUpdate();
        }
    }

    private void SetQueueStatus(int index, string status, string? tooltip = null)
    {
        if (index < 0 || index >= _queueView.Items.Count)
            return;

        var item = _queueView.Items[index];
        item.SubItems[2].Text = status;
        item.ToolTipText = string.IsNullOrWhiteSpace(tooltip) ? status : tooltip;
        item.EnsureVisible();
    }

    private void MarkRemainingAsCancelled()
    {
        foreach (ListViewItem item in _queueView.Items)
        {
            if (item.SubItems[2].Text == "En attente")
                item.SubItems[2].Text = "Non démarré";
        }
    }

    private void SetBusy(bool busy)
    {
        _urlsBox.Enabled = !busy;
        _folderBox.Enabled = !busy;
        _pasteButton.Enabled = !busy;
        _importButton.Enabled = !busy;
        _clearButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _cookiesCheck.Enabled = !busy;
        _browserBox.Enabled = !busy && _cookiesCheck.Checked;
        _retryButton.Enabled = !busy && _failedUrls.Count > 0;
        _downloadButton.Text = busy ? "Annuler la file" : "Télécharger tout";
    }

    private void SetStatusSafe(string text)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatusSafe(text));
            return;
        }

        _statusLabel.Text = text;
    }

    private void UpdateLinkCount()
    {
        var count = GetUniqueUrls(_urlsBox.Text).Count;
        _linkCountLabel.Text = count == 1 ? "1 lien" : $"{count} liens";
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choisir le dossier de téléchargement",
            SelectedPath = Directory.Exists(_folderBox.Text) ? _folderBox.Text : GetDefaultOutputFolder(),
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _folderBox.Text = dialog.SelectedPath;
    }

    private void OpenOutputFolder()
    {
        var folder = _folderBox.Text.Trim();
        if (!Directory.Exists(folder))
            return;

        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(folder);
        Process.Start(startInfo);
    }

    private void PrefillClipboardUrls()
    {
        if (string.IsNullOrWhiteSpace(_urlsBox.Text))
            PasteClipboardUrls(silent: true);
    }

    private void PasteClipboardUrls(bool silent = false)
    {
        try
        {
            var clipboard = Clipboard.GetText();
            var urls = GetUniqueUrls(clipboard);
            if (urls.Count > 0)
            {
                AddUrls(urls);
                return;
            }

            if (!silent)
                SetStatusSafe("Le presse-papiers ne contient pas de lien web valide.");
        }
        catch
        {
            if (!silent)
                SetStatusSafe("Impossible de lire le presse-papiers.");
        }
    }

    private async Task ImportTextFileAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Importer une liste de liens",
            Filter = "Fichiers texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var text = await File.ReadAllTextAsync(dialog.FileName);
            var urls = GetUniqueUrls(text);
            if (urls.Count == 0)
            {
                SetStatusSafe("Le fichier ne contient aucun lien web valide.");
                return;
            }

            AddUrls(urls);
            SetStatusSafe($"{urls.Count} lien(s) importé(s) du fichier.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Impossible d'importer le fichier", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddUrls(IEnumerable<string> newUrls)
    {
        var urls = GetUniqueUrls(_urlsBox.Text).ToList();
        var known = new HashSet<string>(urls, StringComparer.Ordinal);

        foreach (var url in newUrls)
        {
            if (known.Add(url))
                urls.Add(url);
        }

        _urlsBox.Lines = urls.ToArray();
    }

    private static IReadOnlyList<string> GetUniqueUrls(string text)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in UrlRegex().Matches(text))
        {
            var candidate = match.Value.TrimEnd('.', ',', ';', ')', ']', '}');
            if (!IsHttpUrl(candidate) || !seen.Add(candidate))
                continue;

            result.Add(candidate);
        }

        return result;
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

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
        return lines.LastOrDefault() ?? "Échec du téléchargement.";
    }

    private static string GetDefaultOutputFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "ClipPull");
    }

    [GeneratedRegex("https?://[^\\s<>\"']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}
