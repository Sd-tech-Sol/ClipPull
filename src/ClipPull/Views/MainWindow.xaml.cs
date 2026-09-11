using System.Windows;
using ClipPull.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;

namespace ClipPull.Views;

public partial class MainWindow : FluentWindow
{
    internal MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();

        SystemThemeWatcher.Watch(this);

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
    }

    private void OnPreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Any(path => string.Equals(System.IO.Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
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
