using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MundoVoxel.Core;

namespace MundoVoxel.Server;

/// <summary>
/// Servidor multijugador de MundoVoxel.
/// Se ejecuta como: proceso normal (dotnet run), servicio de Windows (sc.exe) o servicio systemd en Linux.
/// Los mundos se mantienen en memoria: el creador puede borrarlos o dejarlos para volver después.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        // Cargar configuración desde el directorio del ejecutable (en un servicio de Windows el CWD es System32)
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false);

        var puerto = builder.Configuration.GetValue("Servidor:Puerto", 25575);
        var nombre = builder.Configuration.GetValue("Servidor:Nombre", "MundoVoxel");
        var maxMundos = builder.Configuration.GetValue("Servidor:MaxMundos", 40);
        var maxJugadores = builder.Configuration.GetValue("Servidor:MaxJugadoresPorMundo", 12);

        builder.Services.AddSingleton(new GameServer(puerto, nombre, maxMundos, maxJugadores));
        builder.Services.AddHostedService<ServidorServicio>();
        builder.Services.AddLogging(l =>
        {
            l.ClearProviders();
            l.AddSimpleConsole(o => o.SingleLine = true);
        });

        // Modo servicio según la plataforma (no-op si no se ejecuta como servicio)
        if (OperatingSystem.IsWindows())
            builder.Services.AddWindowsService(o => o.ServiceName = "MundoVoxelServer");
        if (OperatingSystem.IsLinux())
            builder.Services.AddSystemd();

        var host = builder.Build();
        await host.RunAsync();
    }
}

public sealed class ServidorServicio : BackgroundService
{
    readonly GameServer _servidor;

    public ServidorServicio(GameServer servidor, ILogger<ServidorServicio> log)
    {
        _servidor = servidor;
        _servidor.AlRegistrar += msg => log.LogInformation("{Mensaje}", msg);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _servidor.Iniciar();
        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _servidor.DetenerAsync();
        await base.StopAsync(cancellationToken);
    }
}
