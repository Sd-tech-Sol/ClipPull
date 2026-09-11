using ClipPull.Views;
using ClipPull.Localization;

namespace ClipPull;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        LocalizationService.Initialize();
        app.Run(new MainWindow());
    }
}
