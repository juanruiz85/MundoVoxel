using Microsoft.Extensions.DependencyInjection;
using MundoVoxel.Client.Paginas;

namespace MundoVoxel.Client;

public partial class App : Application
{
    readonly IServiceProvider _servicios;

    public App(IServiceProvider servicios)
    {
        InitializeComponent();
        _servicios = servicios;
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
        });
    }
}
