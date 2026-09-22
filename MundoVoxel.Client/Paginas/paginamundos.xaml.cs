using System.Collections.ObjectModel;
using MundoVoxel.Client.Juego;
using MundoVoxel.Client.Servicios;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Paginas;

public sealed class InfoMundoView
{
    public required InfoMundo Info { get; init; }
    public bool EsDueno { get; init; }
    /// <summary>El boton "Token" solo tiene sentido para el dueno de un mundo
    /// privado: es quien puede compartirlo.</summary>
    public bool PuedeVerToken => EsDueno && !Info.Abierto;
    public string Nombre => Info.Nombre;
    public string Detalle { get; init; } = "";
}

public partial class PaginaMundos : ContentPage
{
    readonly ServicioRed _red;
    readonly ServicioIdioma _idioma;
    readonly ServicioTeclado _teclado;
    readonly ServicioReconexion _reconexion;
    readonly ObservableCollection<InfoMundoView> _items = new();
    IDispatcherTimer? _timer;
    /// <summary>Clave del último mundo creado o al que se pidió unirse (los
    /// mundos privados la necesitan para reconectar automáticamente).</summary>
    string? _pinPendiente;
    /// <summary>Token de invitacion con el que se pidio entrar (alternativa a la
    /// clave): se recuerda para la reconexion automatica.</summary>
    string? _tokenPendiente;
    /// <summary>Reensamblado del mundo por regiones: Unido solo con la cabecera + MundoRegion.</summary>
    Unido? _unidoPendiente;
    MundoRemoto? _remotoPendiente;
    /// <summary>Color de aviso del tema (rojo), guardado para alternar con los
    /// mensajes neutros de progreso.</summary>
    Color _colorError = Colors.OrangeRed;

    public PaginaMundos(ServicioRed red, ServicioIdioma idioma, ServicioTeclado teclado, ServicioReconexion reconexion)
    {
        InitializeComponent();
        _red = red;
        _idioma = idioma;
        _teclado = teclado;
        _reconexion = reconexion;
        Lista.ItemsSource = _items;

        BtnCrear.Text = idioma.O("mundos.nuevo");
        BtnDesconectar.Text = T.Desconectar;
        LblVacio.Text = idioma.O("mundos.vacio");
        LblCrearTitulo.Text = idioma.O("mundos.crear_titulo");
        LblCrearNombre.Text = idioma.O("mundos.nombre_nuevo");
        LblCrearTipo.Text = idioma.O("mundos.publico");
        LblCrearClave.Text = idioma.O("mundos.clave");
        BtnCrearConfirmar.Text = idioma.O("mundos.crear");
        BtnCancelar.Text = idioma.O("mundos.cancelar");
        SwPublico.Toggled += OnTipoCambio;

        // Ajustes del mundo: tamano (ancho/alto/profundo) y poblacion de mobs
        PkrTamano.ItemsSource = Tamanos;
        PkrTamano.SelectedIndex = 3; // 256x64x256 (el mas grande del picker)
        LblCfgTamano.Text = idioma.O("mundos.cfg_tamano");
        LblCfgAgua.Text = idioma.O("mundos.cfg_agua");
        LblCfgLava.Text = idioma.O("mundos.cfg_lava");
        LblCfgMobs.Text = idioma.O("mundos.cfg_mobs");
        LblCfgDia.Text = idioma.O("mundos.cfg_dia");
        SldAgua.ValueChanged += (_, _) => LblCfgAgua.Text = idioma.O("mundos.cfg_agua") + " " + (int)(SldAgua.Value * 100) + "%";
        SldLava.ValueChanged += (_, _) => LblCfgLava.Text = idioma.O("mundos.cfg_lava") + " " + (int)SldLava.Value;
        SldMobs.ValueChanged += (_, _) => LblCfgMobs.Text = idioma.O("mundos.cfg_mobs") + " " + (int)SldMobs.Value;
        SldDia.ValueChanged += (_, _) => LblCfgDia.Text = idioma.O("mundos.cfg_dia") + " " + (int)SldDia.Value + " min";

        _red.AlDesconectar += OnDesconectadoRed;
        _colorError = LblEstado.TextColor;   // el rojo de aviso que trae el tema
    }

    /// <summary>Opciones de tamano de mundo: (Ancho, Alto, Profundo).</summary>
    static readonly (int Ancho, int Alto, int Profundo)[] Tamanos =
    {
        (96, 48, 96),
        (128, 48, 128),
        (192, 64, 192),
        (256, 64, 256),
    };

    /// <summary>Envía la configuración de generación con el mensaje CrearMundo.</summary>
    void EnviarCrearMundo(string nombre, bool publico, string? pin)
    {
        var (ancho, alto, profundo) = Tamanos[Math.Clamp(PkrTamano.SelectedIndex, 0, Tamanos.Length - 1)];
        _red.Enviar(new CrearMundo
        {
            Nombre = nombre,
            Pin = pin,
            Abierto = publico,
            Ancho = ancho,
            Alto = alto,
            Profundo = profundo,
            NivelAgua = (float)SldAgua.Value,
            LagosLava = (int)SldLava.Value,
            LagosAgua = (int)(SldLava.Value * 1.4),
            CantidadMobs = (int)SldMobs.Value,
            SegundosPorDia = (int)SldDia.Value * 60,
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LblTitulo.Text = _idioma.O("mundos.titulo", _red.NombreServidor);
        PanelCrear.IsVisible = false;
        if (_red.Conectado) _red.Enviar(new ListarMundos());
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
    }

    void OnTick(object? s, EventArgs e)
    {
        while (_red.Obtener() is Mensaje m)
            if (Procesar(m)) break;
    }

    /// <summary>Procesa un mensaje. Devuelve true si la pagina navego al juego
    /// (hay que dejar de consumir la cola: el resto de mensajes son del juego).</summary>
    bool Procesar(Mensaje m)
    {
        switch (m)
        {
            case ListaMundos lm:
                _items.Clear();
                foreach (var info in lm.Mundos)
                    _items.Add(new InfoMundoView
                    {
                        Info = info,
                        EsDueno = info.Dueno == EstadoSesion.Nombre,
                        Detalle = $"{_idioma.O(info.Abierto ? "mundos.estado_publico" : "mundos.estado_privado")} · " +
                                  $"{_idioma.O("mundos.jugadores", info.Jugadores, info.MaxJugadores)} · " +
                                  $"{_idioma.O("mundos.dueno", info.Dueno)}",
                    });
                break;

            case Unido u:
                // El mundo llega por regiones: guardar la cabecera y esperarlas.
                _unidoPendiente = u;
                _remotoPendiente = new MundoRemoto(u.Ancho, u.Alto, u.Profundo, u.Semilla);
                return false;

            case MundoRegion mr when _unidoPendiente != null && _remotoPendiente != null:
                {
                    _remotoPendiente.Aplicar(mr.Rx, mr.Rz, mr.Datos);
                    MostrarCarga(_remotoPendiente.Recibidas, _remotoPendiente.Total);
                    // Streaming por proximidad: basta con la region de debajo de
                    // los pies para entrar al mundo; el resto sigue llegando ya
                    // dentro de la partida (y hasta entonces se trata como solido,
                    // para no caer al vacio).
                    var (rxPies, rzPies) = MundoRemoto.RegionDe(_unidoPendiente.Ax, _unidoPendiente.Az);
                    if (!_remotoPendiente.Recibida(rxPies, rzPies)) return false;

                    var up = _unidoPendiente;
                    var rem = _remotoPendiente;
                    _unidoPendiente = null;
                    _remotoPendiente = null;
                    var datos = new DatosMundo
                    {
                        Id = up.Id,
                        Nombre = up.Nombre,
                        Dueno = up.Dueno,
                        IdDueno = up.IdDueno,
                        Mundo = rem.Mundo,
                        Remoto = rem.Completo ? null : rem,
                        Ax = up.Ax, Ay = up.Ay, Az = up.Az,
                        Sensibilidad = Preferences.Get("sensibilidad_raton", Ajustes.Actual.SensibilidadRaton),
                        PinUsado = _pinPendiente,
                        TokenUsado = _tokenPendiente,
                    };
                    // Al entrar al mundo el foco no debe quedar en ningun boton (la
                    // barra espaciadora es para saltar, no para activar el menu).
                    _timer?.Stop();
                    _ = Navigation.PushAsync(new PaginaJuego(_red, _idioma, _teclado, _reconexion, datos));
                    return true;
                }

            case MundoCreado mundoCreado when mundoCreado.Token.Length > 0:
                // Mundo privado recien creado: se muestra el token una vez para
                // que el dueno lo comparta (luego puede volver a pedirlo).
                _ = MostrarTokenAsync(mundoCreado.Token);
                break;

            case TokenMundo tokenMundo:
                _ = MostrarTokenAsync(tokenMundo.Token);
                break;

            case ErrorServidor er:
                MostrarError(TextoError(er));
                return false;
        }
        return false;
    }

    string TextoError(ErrorServidor er)
    {
        var clave = "error." + er.Codigo.ToLowerInvariant();
        return _idioma.Lang.Contiene(clave) ? _idioma.O(clave) : er.Mensaje;
    }

    void MostrarError(string texto)
    {
        LblEstado.TextColor = _colorError;
        LblEstado.Text = texto;
        LblEstado.IsVisible = true;
    }

    /// <summary>Aviso neutro en la barra de estado (no es un error).</summary>
    void MostrarAviso(string texto)
    {
        LblEstado.TextColor = Colors.White;
        LblEstado.Text = texto;
        LblEstado.IsVisible = true;
    }

    /// <summary>Progreso de la descarga del mundo: el servidor lo manda troceado y
    /// antes la pantalla se quedaba muda (con mundos grandes parecia colgada).</summary>
    void MostrarCarga(int recibidos, int total)
    {
        if (total <= 0) return;
        MostrarAviso(_idioma.O("mundos.cargando", Math.Clamp(recibidos * 100 / total, 0, 100)));
    }

    void OnUnirse(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not InfoMundoView item) return;
        _ = UnirseAsync(item);
    }

    async Task UnirseAsync(InfoMundoView item)
    {
        var info = item.Info;
        MostrarAviso(_idioma.O("mundos.entrando"));
        if (info.Abierto)
        {
            _pinPendiente = null;
            _red.Enviar(new Unirse { Id = info.Id });
            return;
        }
        // Un mundo privado se abre con la clave de 6 digitos o con el token de
        // invitacion que comparte el dueno: se acepta cualquiera de los dos.
        var entrada = await DisplayPromptAsync(_idioma.O("mundos.pedir_clave_o_token"), "",
            maxLength: 16, cancel: "?");
        if (entrada == null) return;
        entrada = entrada.Trim();
        if (entrada.Length == 6 && entrada.All(char.IsAsciiDigit))
        {
            _pinPendiente = entrada;
            _tokenPendiente = null;
            _red.Enviar(new Unirse { Id = info.Id, Pin = entrada });
            return;
        }
        if (entrada.Length >= 8)
        {
            // El servidor compara el token sin distinguir mayusculas/minusculas.
            _tokenPendiente = entrada.ToUpperInvariant();
            _pinPendiente = null;
            _red.Enviar(new Unirse { Id = info.Id, Token = _tokenPendiente });
            return;
        }
        MostrarError(_idioma.O("mundos.clave_invalida"));
    }

    /// <summary>Muestra el token de invitacion y lo copia al portapapeles.</summary>
    async Task MostrarTokenAsync(string token)
    {
        bool copiar = await DisplayAlertAsync(_idioma.O("mundos.token_titulo"),
            _idioma.O("mundos.token_texto", token), _idioma.O("mundos.token_copiar"), _idioma.O("mundos.cancelar"));
        if (!copiar) return;
        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(token);
        MostrarError(_idioma.O("mundos.token_copiado"));
    }

    void OnToken(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not InfoMundoView item) return;
        _red.Enviar(new PedirToken { Id = item.Info.Id });
    }

    async void OnBorrar(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not InfoMundoView item) return;
        bool ok = await DisplayAlertAsync(_idioma.O("mundos.borrar_confirmar", item.Nombre), "", T.Borrar, "✕");
        if (ok) _red.Enviar(new BorrarMundo { Id = item.Info.Id });
    }

    void OnAbrirCrear(object? sender, EventArgs e)
    {
        EntNombreNuevo.Text = "";
        EntPin.Text = "";
        SwPublico.IsToggled = true;
        LblCrearError.IsVisible = false;
        PanelCrear.IsVisible = true;
        EntNombreNuevo.Focus();
    }

    void OnTipoCambio(object? sender, ToggledEventArgs e)
    {
        bool publico = e.Value;
        LblCrearTipo.Text = _idioma.O(publico ? "mundos.publico" : "mundos.privado");
        EntPin.IsVisible = !publico;
        LblCrearClave.IsVisible = !publico;
    }

    void OnCrearConfirmar(object? sender, EventArgs e)
    {
        var nombre = EntNombreNuevo.Text?.Trim() ?? "";
        if (nombre.Length == 0)
        {
            LblCrearError.Text = _idioma.O("mundos.nombre_vacio");
            LblCrearError.IsVisible = true;
            return;
        }
        bool publico = SwPublico.IsToggled;
        string? pin = null;
        if (!publico)
        {
            pin = EntPin.Text?.Trim() ?? "";
            if (pin.Length != 6 || !pin.All(char.IsAsciiDigit))
            {
                LblCrearError.Text = _idioma.O("mundos.clave_invalida");
                LblCrearError.IsVisible = true;
                return;
            }
        }
        PanelCrear.IsVisible = false;
        _pinPendiente = pin; // el mundo creado era privado: recuerda su clave para reconectar
        MostrarAviso(_idioma.O("mundos.creando"));
        EnviarCrearMundo(nombre, publico, pin);
    }

    void OnCancelarCrear(object? sender, EventArgs e) => PanelCrear.IsVisible = false;

    void OnDesconectar(object? sender, EventArgs e)
    {
        _red.Desconectar();
        _ = Navigation.PopToRootAsync();
    }

    void OnDesconectadoRed()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Navigation?.NavigationStack.LastOrDefault() != this) return;
            MostrarError(_idioma.O("error.desconectado"));
            _ = Navigation.PopToRootAsync();
        });
    }
}
