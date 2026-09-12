using ClipPull.Views;
using ClipPull.Localization;
using ClipPull.Services;

namespace ClipPull;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        var startupSettings = new SettingsService().Load();
        LocalizationService.Initialize(startupSettings.Language);
        app.Run(new MainWindow());
    }
}
