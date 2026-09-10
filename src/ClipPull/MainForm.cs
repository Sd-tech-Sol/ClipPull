using System.Diagnostics;
using ClipPull.Services;

namespace ClipPull;

internal sealed class MainForm : Form
{
    private readonly TextBox _urlBox = new();
    private readonly TextBox _folderBox = new();
    private readonly Button _pasteButton = new();
    private readonly Button _browseButton = new();
    private readonly Button _downloadButton = new();
    private readonly Button _openFolderButton = new();
    private readonly CheckBox _cookiesCheck = new();
    private readonly ComboBox _browserBox = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();

    private readonly YtDlpManager _engineManager = new();
    private readonly DownloadService _downloadService = new();
    private CancellationTokenSource? _activeDownload;

    public MainForm()
    {
        Text = "ClipPull";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 430);
        ClientSize = new Size(760, 450);
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
        Shown += (_, _) => PrefillClipboardUrl();
        FormClosing += (_, _) => _activeDownload?.Cancel();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            ColumnCount = 1,
            RowCount = 8
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "ClipPull",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(32, 33, 36),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);

        root.Controls.Add(BuildUrlSection(), 0, 1);
        root.Controls.Add(BuildFolderSection(), 0, 2);
        root.Controls.Add(BuildBrowserSection(), 0, 3);

        _downloadButton.Text = "Télécharger";
        _downloadButton.Dock = DockStyle.Fill;
        _downloadButton.Font = new Font("Segoe UI Semibold", 11F);
        _downloadButton.FlatStyle = FlatStyle.System;
        _downloadButton.Click += DownloadButton_Click;
        root.Controls.Add(_downloadButton, 0, 4);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Style = ProgressBarStyle.Continuous;
        root.Controls.Add(_progressBar, 0, 5);

        var statusRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusLabel.Text = "Prêt. Colle un lien vidéo.";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(80, 80, 80);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        _openFolderButton.Text = "Ouvrir le dossier";
        _openFolderButton.AutoSize = true;
        _openFolderButton.Enabled = false;
        _openFolderButton.Click += (_, _) => OpenOutputFolder();

        statusRow.Controls.Add(_statusLabel, 0, 0);
        statusRow.Controls.Add(_openFolderButton, 1, 0);
        root.Controls.Add(statusRow, 0, 6);

        var privacy = new Label
        {
            Text = "Local : aucun compte ClipPull, aucune télémétrie. Télécharge seulement du contenu que tu as le droit de conserver.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(105, 105, 105),
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.TopLeft
        };
        root.Controls.Add(privacy, 0, 7);

        Controls.Add(root);
        AcceptButton = _downloadButton;
    }

    private Control BuildUrlSection()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var label = new Label
        {
            Text = "Lien de la vidéo",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };

        _urlBox.Dock = DockStyle.Fill;
        _urlBox.PlaceholderText = "https://www.facebook.com/share/v/...";

        _pasteButton.Text = "Coller";
        _pasteButton.AutoSize = true;
        _pasteButton.Height = 30;
        _pasteButton.Margin = new Padding(8, 3, 0, 3);
        _pasteButton.Click += (_, _) => PasteClipboardUrl();

        panel.Controls.Add(label, 0, 0);
        panel.SetColumnSpan(label, 2);
        panel.Controls.Add(_urlBox, 0, 1);
        panel.Controls.Add(_pasteButton, 1, 1);
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
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

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
        _cookiesCheck.CheckedChanged += (_, _) => _browserBox.Enabled = _cookiesCheck.Checked;

        _browserBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _browserBox.Width = 110;
        _browserBox.Items.AddRange(["Chrome", "Edge", "Firefox", "Brave"]);
        _browserBox.SelectedIndex = 0;
        _browserBox.Enabled = false;

        panel.Controls.Add(_cookiesCheck);
        panel.Controls.Add(_browserBox);
        return panel;
    }

    private async void DownloadButton_Click(object? sender, EventArgs e)
    {
        if (_activeDownload is not null)
        {
            _activeDownload.Cancel();
            return;
        }

        var url = _urlBox.Text.Trim();
        if (!IsHttpUrl(url))
        {
            MessageBox.Show(this, "Colle une adresse web valide qui commence par http:// ou https://.", "Lien invalide", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _urlBox.Focus();
            return;
        }

        var folder = _folderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(this, "Choisis un dossier où enregistrer la vidéo.", "Dossier requis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _activeDownload = new CancellationTokenSource();
        var token = _activeDownload.Token;
        SetBusy(true);

        try
        {
            Directory.CreateDirectory(folder);
            var enginePath = await _engineManager.EnsureAsync(SetStatusSafe, token);
            var progress = new Progress<double>(value => _progressBar.Value = (int)Math.Clamp(Math.Round(value), 0, 100));
            var browser = _cookiesCheck.Checked ? _browserBox.SelectedItem?.ToString() : null;

            var file = await _downloadService.DownloadAsync(
                enginePath,
                url,
                folder,
                browser,
                progress,
                SetStatusSafe,
                token);

            SetStatusSafe(file is null ? "Téléchargement terminé." : $"Terminé : {Path.GetFileName(file)}");
            _openFolderButton.Enabled = true;
        }
        catch (OperationCanceledException)
        {
            SetStatusSafe("Téléchargement annulé.");
        }
        catch (Exception ex)
        {
            SetStatusSafe("Échec du téléchargement.");
            MessageBox.Show(this, ex.Message, "ClipPull", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _activeDownload.Dispose();
            _activeDownload = null;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _urlBox.Enabled = !busy;
        _folderBox.Enabled = !busy;
        _pasteButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _cookiesCheck.Enabled = !busy;
        _browserBox.Enabled = !busy && _cookiesCheck.Checked;
        _downloadButton.Text = busy ? "Annuler" : "Télécharger";
        _progressBar.Value = busy ? 0 : _progressBar.Value;
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

    private void PrefillClipboardUrl()
    {
        if (string.IsNullOrWhiteSpace(_urlBox.Text))
            PasteClipboardUrl(silent: true);
    }

    private void PasteClipboardUrl(bool silent = false)
    {
        try
        {
            var text = Clipboard.GetText().Trim();
            if (IsHttpUrl(text))
            {
                _urlBox.Text = text;
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

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string GetDefaultOutputFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "ClipPull");
    }
}
