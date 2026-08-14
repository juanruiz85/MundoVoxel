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

        BtnCrear.Text = T.Crear;
        BtnDesconectar.Text = T.Desconectar;
        LblVacio.Text = idioma.O("mundos.vacio");
        LblCrearTitulo.Text = idioma.O("mundos.crear_titulo");
        LblCrearNombre.Text = idioma.O("mundos.nombre_nuevo");
        LblCrearTipo.Text = idioma.O("mundos.publico");
        LblCrearClave.Text = idioma.O("mundos.clave");
        BtnCrearConfirmar.Text = idioma.O("mundos.crear");
        BtnCancelar.Text = idioma.O("mundos.cancelar");
        SwPublico.Toggled += OnTipoCambio;

        _red.AlDesconectar += OnDesconectadoRed;
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
                };
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
        _red.Enviar(new CrearMundo { Nombre = nombre, Pin = pin, Abierto = publico });
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
