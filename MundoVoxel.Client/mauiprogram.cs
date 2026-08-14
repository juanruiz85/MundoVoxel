using Microsoft.Extensions.Logging;
using MundoVoxel.Client.Paginas;
using MundoVoxel.Client.Servicios;

namespace MundoVoxel.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            EscribirCrash("Unhandled: " + (e.ExceptionObject?.ToString() ?? "sin detalle"));
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            EscribirCrash("UnobservedTask: " + (e.Exception?.ToString() ?? "sin detalle"));
            e.SetObserved();
        };
        try
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>();

            builder.Services.AddSingleton<ServicioIdioma>();
            builder.Services.AddSingleton<ServicioRed>();
            builder.Services.AddSingleton<ServicioTeclado>();
            builder.Services.AddSingleton<PaginaMenu>();
            builder.Services.AddSingleton<PaginaMundos>();

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
        catch (Exception ex)
        {
            EscribirCrash("CreateMauiApp: " + ex);
            throw;
        }
    }

    static void EscribirCrash(string detalle)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now:HH:mm:ss}] {detalle}{Environment.NewLine}");
        }
        catch { }
    }
}
