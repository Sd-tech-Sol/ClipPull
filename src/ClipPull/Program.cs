using ClipPull.Views;

namespace ClipPull;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run(new MainWindow());
    }
}
