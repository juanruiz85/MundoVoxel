using System.Net.Sockets;
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
        LblTls.Text = idioma.O("menu.servidor_cifrado");
        SwTls.IsToggled = EstadoSesion.Tls;
        // Certificado TLS recordado por el cliente (trust-on-first-use)
        _red.AlHuellaNueva += h => MainThread.BeginInvokeOnMainThread(() =>
            MostrarEstado(idioma.O("menu.huella_nueva", h)));
        _red.AlHuellaCambiada += h => MainThread.BeginInvokeOnMainThread(() =>
            MostrarEstado(idioma.O("menu.huella_cambiada", h)));

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
        ConectarYAvanzar("127.0.0.1", false); // el servidor local va sin cifrar
    }

    void OnConectar(object? sender, EventArgs e) => ConectarYAvanzar(EntIp.Text?.Trim() ?? "", SwTls.IsToggled);

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
            fila.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            fila.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var btnConectar = new Button
            {
                Text = f.Cifrado ? $"{f.Alias}  ({f.Ip}:{f.Puerto}) (TLS)" : $"{f.Alias}  ({f.Ip}:{f.Puerto})",
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

            // Estado en vivo del servidor (latencia + jugadores en linea): ping ligero
            var lblEstado = new Label
            {
                Text = _idioma.O("menu.favoritos_esperando"),
                FontSize = 11,
                Opacity = 0.75,
                TextColor = Colors.White,
                Margin = new Thickness(6, 0, 0, 4),
            };
            Grid.SetRow(lblEstado, 1);
            Grid.SetColumn(lblEstado, 0);
            Grid.SetColumnSpan(lblEstado, 2);
            fila.Children.Add(lblEstado);
            _ = ComprobarEstadoAsync(fav, lblEstado);
        }
    }

    /// <summary>Comprueba el estado del favorito con un ping ligero y escribe en la
    /// etiqueta la latencia y los jugadores en linea (o "sin respuesta").</summary>
    async Task ComprobarEstadoAsync(ServidorFavorito fav, Label lbl)
    {
        var r = await SondearAsync(fav.Ip, fav.Puerto);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            lbl.Text = r.HasValue
                ? _idioma.O("menu.favoritos_estado", r.Value.Jugadores, r.Value.Ms)
                : _idioma.O("menu.favoritos_sin_respuesta");
        });
    }

    /// <summary>Ping ligero (no se identifica ni entra a ningun mundo): devuelve
    /// (jugadores en linea, latencia ms) o null si no responde en 2.5 s.</summary>
    static async Task<(int Jugadores, long Ms)?> SondearAsync(string ip, int puerto)
    {
        try
        {
            using var tcp = new TcpClient { NoDelay = true };
            var conectar = tcp.ConnectAsync(ip, puerto);
            if (await Task.WhenAny(conectar, Task.Delay(2500)) != conectar || !tcp.Connected) return null;
            using var flujo = tcp.GetStream();
            long marca = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await flujo.WriteAsync(Protocolo.Codificar(new Ping { MarcaTiempo = marca }));
            using var cts = new CancellationTokenSource(2500);
            if (await Frames.LeerAsync(flujo, cts.Token) is Pong p)
                return (p.JugadoresEnLinea, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - p.MarcaTiempo);
        }
        catch { /* servidor caido o respuesta invalida */ }
        return null;
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

        ServidoresFavoritos.Agregar(new ServidorFavorito { Alias = alias, Ip = ip, Puerto = puerto, Cifrado = SwTls.IsToggled });
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
        SwTls.IsToggled = favorito.Cifrado; // el favorito recuerda si su servidor va cifrado
        ConectarYAvanzar(favorito.Ip, favorito.Cifrado);
    }

    int ObtenerPuerto()
    {
        if (int.TryParse(EntPuerto.Text, out int p) && p >= 1 && p <= 65535) return p;
        EntPuerto.Text = "25575";
        return 25575;
    }

    /// <summary>Muestra un aviso en el menu (huellas TLS, avisos, errores).</summary>
    void MostrarEstado(string texto)
    {
        LblEstado.Text = texto;
        LblEstado.IsVisible = true;
    }

    async void ConectarYAvanzar(string ip, bool cifrado)
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
        // Guardar el modo real de la sesion: lo reusan la reconexion automatica y el
        // sondeo de estado de los favoritos (antes nunca se fijaba: reconectar a un
        // servidor cifrado iba sin cifrar y fallaba).
        EstadoSesion.Tls = cifrado;

        LblEstado.Text = _idioma.O("menu.conectando");
        LblEstado.IsVisible = true;
        BtnSolo.IsEnabled = false;
        BtnConectar.IsEnabled = false;
        BtnGuardarFavorito.IsEnabled = false;

        bool ok = await Task.Run(() => _red.Conectar(ip, EstadoSesion.Puerto, cifrado: cifrado));
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
