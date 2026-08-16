using System.Collections.ObjectModel;
using MundoVoxel.Client.Juego;
using MundoVoxel.Client.Servicios;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Paginas;

public sealed class InfoMundoView
{
    public required InfoMundo Info { get; init; }
    public bool EsDueno { get; init; }
    public string Nombre => Info.Nombre;
    public string Detalle { get; init; } = "";
}

public partial class PaginaMundos : ContentPage
{
    readonly ServicioRed _red;
    readonly ServicioIdioma _idioma;
    readonly ServicioTeclado _teclado;
    readonly ObservableCollection<InfoMundoView> _items = new();
    IDispatcherTimer? _timer;

    public PaginaMundos(ServicioRed red, ServicioIdioma idioma, ServicioTeclado teclado)
    {
        InitializeComponent();
        _red = red;
        _idioma = idioma;
        _teclado = teclado;
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
        PkrTamano.SelectedIndex = 2;
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
        while (_red.Obtener() is Mensaje m) Procesar(m);
    }

    void Procesar(Mensaje m)
    {
        switch (m)
        {
            case ListaMundos lm:
                _items.Clear();
                foreach (var info in lm.Mundos)
                    _items.Add(new InfoMundoView
                    {
                        Info = info,
                        EsDueno = info.IdDueno == _red.MiId,
                        Detalle = $"{_idioma.O(info.Abierto ? "mundos.estado_publico" : "mundos.estado_privado")} · " +
                                  $"{_idioma.O("mundos.jugadores", info.Jugadores, info.MaxJugadores)} · " +
                                  $"{_idioma.O("mundos.dueno", info.Dueno)}",
                    });
                break;

            case Unido u:
                var datos = new DatosMundo
                {
                    Id = u.Id,
                    Nombre = u.Nombre,
                    Dueno = u.Dueno,
                    IdDueno = u.IdDueno,
                    MundoComprimido = u.MundoComprimido,
                    Ax = u.Ax, Ay = u.Ay, Az = u.Az,
                    Sensibilidad = Preferences.Get("sensibilidad_raton", 1f),
                };
                // Al entrar al mundo el foco no debe quedar en ningA-on botA3n (la barra
                // espaciadora es para saltar, no para activar el menA-o).
                _timer?.Stop();
                _ = Navigation.PushAsync(new PaginaJuego(_red, _idioma, _teclado, datos));
                break;

            case ErrorServidor er:
                MostrarError(TextoError(er));
                break;
        }
    }

    string TextoError(ErrorServidor er)
    {
        var clave = "error." + er.Codigo.ToLowerInvariant();
        return _idioma.Lang.Contiene(clave) ? _idioma.O(clave) : er.Mensaje;
    }

    void MostrarError(string texto)
    {
        LblEstado.Text = texto;
        LblEstado.IsVisible = true;
    }

    void OnUnirse(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not InfoMundoView item) return;
        _ = UnirseAsync(item);
    }

    async Task UnirseAsync(InfoMundoView item)
    {
        var info = item.Info;
        if (info.Abierto)
        {
            _red.Enviar(new Unirse { Id = info.Id });
            return;
        }
        var pin = await DisplayPromptAsync(_idioma.O("mundos.pedir_clave"), "",
            maxLength: 4, keyboard: Keyboard.Numeric, cancel: "✕");
        if (pin == null) return;
        pin = pin.Trim();
        if (pin.Length != 4 || !pin.All(char.IsAsciiDigit))
        {
            MostrarError(_idioma.O("mundos.clave_invalida"));
            return;
        }
        _red.Enviar(new Unirse { Id = info.Id, Pin = pin });
    }

    async void OnBorrar(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not InfoMundoView item) return;
        bool ok = await DisplayAlert(_idioma.O("mundos.borrar_confirmar", item.Nombre), "", T.Borrar, "✕");
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
            if (pin.Length != 4 || !pin.All(char.IsAsciiDigit))
            {
                LblCrearError.Text = _idioma.O("mundos.clave_invalida");
                LblCrearError.IsVisible = true;
                return;
            }
        }
        PanelCrear.IsVisible = false;
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
