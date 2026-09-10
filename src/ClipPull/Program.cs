namespace ClipPull;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var form = new AdvancedMainForm
        {
            Text = "ClipPull 0.3.1"
        };
        form.EnableStartupDependencyUpdates();
        Application.Run(form);
    }
}
