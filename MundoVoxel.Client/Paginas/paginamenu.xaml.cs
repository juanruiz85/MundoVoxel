using MundoVoxel.Client.Servicios;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Paginas;

public partial class PaginaMenu : ContentPage
{
    readonly ServicioRed _red;
    readonly ServicioIdioma _idioma;
    readonly PaginaMundos _paginaMundos;
    bool _conectando;

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
        BtnGuardarFavorito.Text = idioma.O("menu.favoritos_guardar");
        LblFavoritosTitulo.Text = idioma.O("menu.favoritos_titulo");
        LblControlesTitulo.Text = idioma.O("menu.controles_titulo");
        LblControles.Text = idioma.O("menu.controles_desc");

        EntNombre.Text = EstadoSesion.Nombre;
        EntIp.Text = EstadoSesion.Ip;
        EntPuerto.Text = EstadoSesion.Puerto.ToString();

        _red.AlConectar += OnConectado;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefrescarFavoritos();
    }

    void OnJugarSolo(object? sender, EventArgs e)
    {
        int puerto = ObtenerPuerto();
        ServidorLocal.Asegurar(puerto);
        if (!_red.Conectado) LblEstado.Text = _idioma.O("menu.servidor_local_ok", puerto);
        ConectarYAvanzar("127.0.0.1");
    }

    void OnConectar(object? sender, EventArgs e) => ConectarYAvanzar(EntIp.Text?.Trim() ?? "");

    // ------------------------------------------------------- servidores favoritos

    /// <summary>Redibuja la lista de favoritos persistidos (alias + ip:puerto,
    /// con un boton para conectar y otro para quitar cada uno).</summary>
    void RefrescarFavoritos()
    {
        PanelFavoritos.Children.Clear();
        var favoritos = ServidoresFavoritos.Cargar();
        LblFavoritosTitulo.IsVisible = favoritos.Count > 0;
        foreach (var f in favoritos)
        {
            var fila = new Grid { ColumnSpacing = 6 };
            fila.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            fila.ColumnDefinitions.Add(new ColumnDefinition(44));

            var btnConectar = new Button
            {
                Text = $"{f.Alias}  ({f.Ip}:{f.Puerto})",
                FontSize = 13,
                HeightRequest = 38,
                Padding = new Thickness(8, 0),
                LineBreakMode = LineBreakMode.TailTruncation,
            };
            var fav = f;
            btnConectar.Clicked += (_, _) => ConectarFavorito(fav);
            fila.Children.Add(btnConectar);

            var btnQuitar = new Button
            {
                Text = "✕",
                FontSize = 13,
                HeightRequest = 38,
                Padding = 0,
                // El estilo esta en el diccionario de la aplicacion (app.xaml)
                Style = Application.Current?.Resources["BtnPeligro"] as Style,
            };
            btnQuitar.Clicked += async (_, _) => await QuitarFavorito(fav);
            Grid.SetColumn(btnQuitar, 1);
            fila.Children.Add(btnQuitar);

            PanelFavoritos.Children.Add(fila);
        }
    }

    async void OnGuardarFavorito(object? sender, EventArgs e)
    {
        var ip = EntIp.Text?.Trim() ?? "";
        if (ip.Length == 0)
        {
            LblEstado.Text = _idioma.O("menu.favoritos_vacio_ip");
            LblEstado.IsVisible = true;
            return;
        }
        int puerto = ObtenerPuerto();

        var alias = await DisplayPromptAsync(_idioma.O("menu.favoritos_alias", $"{ip}:{puerto}"), "",
            maxLength: 24, initialValue: ip, cancel: "✕");
        if (alias == null) return; // cancelado por el usuario
        alias = alias.Trim();
        if (alias.Length == 0) alias = ip;

        ServidoresFavoritos.Agregar(new ServidorFavorito { Alias = alias, Ip = ip, Puerto = puerto });
        RefrescarFavoritos();
    }

    async Task QuitarFavorito(ServidorFavorito favorito)
    {
        bool ok = await DisplayAlert(_idioma.O("menu.favoritos_borrar", favorito.Alias), "",
            _idioma.O("mundos.borrar"), "✕");
        if (!ok) return;
        ServidoresFavoritos.Quitar(favorito.Ip, favorito.Puerto);
        RefrescarFavoritos();
    }

    /// <summary>Conecta a un favorito: rellena los campos (para que se vea a dónde
    /// se va) y sigue el mismo camino que el boton Conectar.</summary>
    void ConectarFavorito(ServidorFavorito favorito)
    {
        if (_conectando) return;
        EntIp.Text = favorito.Ip;
        EntPuerto.Text = favorito.Puerto.ToString();
        ConectarYAvanzar(favorito.Ip);
    }

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
        if (_conectando) return;
        _conectando = true;

        EstadoSesion.Nombre = nombre;
        EstadoSesion.Ip = ip;
        EstadoSesion.Puerto = ObtenerPuerto();

        LblEstado.Text = _idioma.O("menu.conectando");
        LblEstado.IsVisible = true;
        BtnSolo.IsEnabled = false;
        BtnConectar.IsEnabled = false;
        BtnGuardarFavorito.IsEnabled = false;

        bool ok = await Task.Run(() => _red.Conectar(ip, EstadoSesion.Puerto));
        if (!ok)
        {
            LblEstado.Text = _idioma.O("menu.error_conexion", $"{ip}:{EstadoSesion.Puerto}");
            BtnSolo.IsEnabled = true;
            BtnConectar.IsEnabled = true;
            BtnGuardarFavorito.IsEnabled = true;
            _conectando = false;
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
            BtnGuardarFavorito.IsEnabled = true;
            _conectando = false;
            _ = Navigation.PushAsync(_paginaMundos);
        });
    }
}
