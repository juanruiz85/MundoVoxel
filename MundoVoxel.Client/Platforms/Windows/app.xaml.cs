using Microsoft.UI.Xaml;

namespace MundoVoxel.Client.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"[{DateTime.Now:HH:mm:ss}] WinUI: {(e.Exception?.ToString() ?? e.Message)}{Environment.NewLine}");
            }
            catch { }
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
