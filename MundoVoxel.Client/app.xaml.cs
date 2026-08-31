using Microsoft.Extensions.DependencyInjection;
using MundoVoxel.Client.Paginas;
using MundoVoxel.Client.Servicios;

namespace MundoVoxel.Client;

public partial class App : Application
{
    readonly IServiceProvider _servicios;

    public App(IServiceProvider servicios)
    {
        InitializeComponent();
        _servicios = servicios;
    }

    protected override void OnSleep()
    {
        // Guardar los mundos del servidor local a disco al cerrar o suspender
        // la app (sin esto, al cerrar el juego se perdia todo el progreso).
        ServidorLocal.GuardarMundos();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Se resuelve aquí (y no en el constructor) para que los recursos
        // de App.xaml ya estén cargados cuando la página se construya.
        var menu = _servicios.GetRequiredService<PaginaMenu>();
        return new Window(new NavigationPage(menu)
        {
            BarBackgroundColor = Color.FromArgb("#10161f"),
            BarTextColor = Colors.White,
        })
        {
            Title = "MundoVoxel",
        };
    }
}
