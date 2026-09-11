using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipPull.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MediaColor = System.Windows.Media.Color;

namespace ClipPull.Views;

public partial class MainWindow : FluentWindow
{
    private static readonly MediaColor BrandAccent = MediaColor.FromRgb(0x00, 0x9C, 0x95);

    internal MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();

        var forcedTheme = GetVisualReviewTheme();
        if (forcedTheme is null)
        {
            SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: false);
        }
        else
        {
            ApplicationThemeManager.Apply(forcedTheme.Value, WindowBackdropType.Mica, updateAccent: false);
        }

        ApplyBrandAccent(forcedTheme ?? ApplicationThemeManager.GetAppTheme());
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;

        Loaded += async (_, _) =>
        {
            ViewModel.PrefillFromClipboard();
            await ViewModel.RunStartupChecksAsync();
        };

        Closing += (_, _) =>
        {
            ViewModel.CancelStartupChecks();
            ViewModel.CancelActiveOperation();
        };

        Closed += (_, _) => ApplicationThemeManager.Changed -= OnApplicationThemeChanged;
    }

    private static void OnApplicationThemeChanged(ApplicationTheme theme, MediaColor _) => ApplyBrandAccent(theme);

    private static ApplicationTheme? GetVisualReviewTheme()
    {
        var value = Environment.GetEnvironmentVariable("CLIPPULL_THEME");
        if (string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase))
            return ApplicationTheme.Dark;
        if (string.Equals(value, "light", StringComparison.OrdinalIgnoreCase))
            return ApplicationTheme.Light;

        return null;
    }

    private static void ApplyBrandAccent(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.Unknown)
            theme = ApplicationTheme.Light;

        ApplicationAccentColorManager.Apply(BrandAccent, theme, systemGlassColor: false, systemAccentColor: false);
    }

    private void SetDropZoneActive(bool isActive)
    {
        DropZoneBorder.SetResourceReference(
            Border.BorderBrushProperty,
            isActive ? "AccentFillColorDefaultBrush" : "CardStrokeColorDefaultBrush");
        DropZoneBorder.SetResourceReference(
            Border.BackgroundProperty,
            isActive ? "ControlFillColorSecondaryBrush" : "CardBackgroundFillColorDefaultBrush");
        DropZoneBorder.BorderThickness = new Thickness(isActive ? 2 : 1);
        DropZoneBorder.Padding = new Thickness(isActive ? 19 : 20);
    }

    private void OnPreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Any(path => string.Equals(System.IO.Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = DragDropEffects.Copy;
                SetDropZoneActive(true);
                e.Handled = true;
                return;
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text))
        {
            e.Effects = DragDropEffects.Copy;
            SetDropZoneActive(true);
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        SetDropZoneActive(false);
        e.Handled = true;
    }

    private void OnPreviewDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        var position = e.GetPosition(this);
        if (position.X <= 0 || position.Y <= 0 || position.X >= ActualWidth || position.Y >= ActualHeight)
            SetDropZoneActive(false);
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        SetDropZoneActive(false);

        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            ViewModel.ImportDroppedFiles(files);
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.UnicodeText) && e.Data.GetData(DataFormats.UnicodeText) is string unicodeText)
        {
            ViewModel.AddDroppedText(unicodeText);
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.Text) && e.Data.GetData(DataFormats.Text) is string text)
        {
            ViewModel.AddDroppedText(text);
        }
    }
}
