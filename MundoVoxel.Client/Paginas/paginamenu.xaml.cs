using MundoVoxel.Client.Servicios;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Paginas;

public partial class PaginaMenu : ContentPage
{
    readonly ServicioRed _red;
    readonly ServicioIdioma _idioma;
    readonly PaginaMundos _paginaMundos;

    public PaginaMenu(ServicioRed red, ServicioIdioma idioma, PaginaMundos paginaMundos)
    {
        InitializeComponent();
        _red = red;
        _idioma = idioma;
        _paginaMundos = paginaMundos;

        LblTitulo.Text = idioma.O("menu.titulo");
        LblSubtitulo.Text = idioma.O("menu.subtitulo");
        LblNombre.Text = idioma.O("menu.nombre");
        LblIp.Text = idioma.O("menu.ip");
        EntPuerto.Placeholder = idioma.O("menu.puerto");
        BtnSolo.Text = idioma.O("menu.jugar_solo");
        BtnConectar.Text = idioma.O("menu.conectar");
        LblControlesTitulo.Text = idioma.O("menu.controles_titulo");
        LblControles.Text = idioma.O("menu.controles_desc");

        EntNombre.Text = EstadoSesion.Nombre;
        EntIp.Text = EstadoSesion.Ip;
        EntPuerto.Text = EstadoSesion.Puerto.ToString();

        _red.AlConectar += OnConectado;
    }

    void OnJugarSolo(object? sender, EventArgs e)
    {
        int puerto = ObtenerPuerto();
        ServidorLocal.Asegurar(puerto);
        if (!_red.Conectado) LblEstado.Text = _idioma.O("menu.servidor_local_ok", puerto);
        ConectarYAvanzar("127.0.0.1");
    }

    void OnConectar(object? sender, EventArgs e) => ConectarYAvanzar(EntIp.Text?.Trim() ?? "");

    int ObtenerPuerto()
    {
        if (int.TryParse(EntPuerto.Text, out int p) && p >= 1 && p <= 65535) return p;
        EntPuerto.Text = "25575";
        return 25575;
    }

    async void ConectarYAvanzar(string ip)
    {
        var nombre = EntNombre.Text?.Trim() ?? "";
        if (nombre.Length == 0)
        {
            LblEstado.Text = _idioma.O("menu.nombre_vacio");
            LblEstado.IsVisible = true;
            return;
        }
        if (ip.Length == 0)
        {
            LblEstado.Text = _idioma.O("menu.ip_vacia");
            LblEstado.IsVisible = true;
            return;
        }

        EstadoSesion.Nombre = nombre;
        EstadoSesion.Ip = ip;
        EstadoSesion.Puerto = ObtenerPuerto();

        LblEstado.Text = _idioma.O("menu.conectando");
        LblEstado.IsVisible = true;
        BtnSolo.IsEnabled = false;
        BtnConectar.IsEnabled = false;

        bool ok = await Task.Run(() => _red.Conectar(ip, EstadoSesion.Puerto));
        if (!ok)
        {
            LblEstado.Text = _idioma.O("menu.error_conexion", $"{ip}:{EstadoSesion.Puerto}");
            BtnSolo.IsEnabled = true;
            BtnConectar.IsEnabled = true;
            return;
        }
        _red.Enviar(new Hola { Nombre = nombre, Version = "1.0" });
    }

    void OnConectado()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Si ya estamos en la lista de mundos (reconexión), no apilar otra vez.
            if (Navigation?.NavigationStack.Count > 1) return;
            LblEstado.IsVisible = false;
            BtnSolo.IsEnabled = true;
            BtnConectar.IsEnabled = true;
            _ = Navigation.PushAsync(_paginaMundos);
        });
    }
}
