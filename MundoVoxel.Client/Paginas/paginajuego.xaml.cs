using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using MundoVoxel.Client.Juego;
using MundoVoxel.Client.Servicios;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Paginas;

public partial class PaginaJuego : ContentPage
{
    readonly ServicioRed _red;
    readonly ServicioIdioma _idioma;
    readonly ServicioTeclado _teclado;
    readonly DatosMundo _datos;
    readonly VistaJuego _vista = new();
    readonly ObservableCollection<string> _chat = new();
    readonly Dictionary<int, string> _nombres = new();
    readonly Stopwatch _reloj = new();
    readonly bool _esMovil = DeviceInfo.Platform == DevicePlatform.Android;

    IDispatcherTimer? _timer;
    float _fps;
    int _frames, _ticksPosicion;    bool _pausado;
    int _nivelDistancia = 1;
    static readonly int[] Distancias = { 1, 2, 3 };
    static bool _tecladoVinculado;
    bool _saliendo;
    bool _renderizando;
    List<SlotEstado> _inventario = new();
    // Inventario tipo Minecraft (slots)
    struct SlotUI { public ushort Material; public int Cantidad; }
    SlotUI[] _slots = new SlotUI[27];
    SlotUI[] _grid = new SlotUI[9];
    int _gridTamaño = 2;
    int _indiceResultado = -1;
    ushort _cursorMaterial;
    int _cursorCantidad;
    Button[,] _botonesCraft = new Button[3, 3];
    Button[,] _botonesInv = new Button[3, 9];

    static readonly Color[] ColoresRemoto =
    {
        Colors.Red, Colors.Yellow, Colors.Lime, Colors.Cyan, Colors.Magenta, Colors.Orange,
        Colors.Purple, Colors.Pink, Colors.Teal, Colors.Gold, Colors.Silver, Colors.DeepSkyBlue,
    };

    public PaginaJuego(ServicioRed red, ServicioIdioma idioma, ServicioTeclado teclado, DatosMundo datos)
    {
        try
        {
            InitializeComponent();
            _red = red;
            _idioma = idioma;
            _teclado = teclado;
            _datos = datos;

        Vista.Drawable = _vista;
        Chat.ItemsSource = _chat;
        LblMundo.Text = datos.Nombre;
        PanelMovil.IsVisible = _esMovil;

        // Entrada táctil / ratón sobre el área de juego
        Vista.StartInteraction += (_, e) => { if (e.Touches.Length > 0) _vista.IniciarInteraccion(e.Touches[0], _esMovil); };
        Vista.DragInteraction += (_, e) => { if (e.Touches.Length > 0) _vista.ArrastrarInteraccion(e.Touches[0]); };
        Vista.EndInteraction += (_, e) => { if (e.Touches.Length > 0) _vista.TerminarInteraccion(e.Touches[0]); };

        BtnRomper.Text = T.Romper;
        BtnColocar.Text = T.Colocar;
        BtnSaltar.Text = T.Saltar;
        BtnVolar.Text = T.Volar;
        BtnChat.Text = T.Chat;
        BtnMenu.Text = T.Menu;
        BtnReanudar.Text = T.Reanudar;
        LblPausaTitulo.Text = idioma.O("pausa.titulo");
        BtnBorrarMundo.Text = idioma.O("pausa.borrar_mundo");
        BtnSalirMundo.Text = idioma.O("pausa.salir_mundo");
        BtnDesconectar.Text = idioma.O("pausa.desconectar");
        BtnInventario.Text = "Inventario (E)";
        ConstruirPanelInventario();
        BtnDistancia.Text = idioma.O("juego.distancia", NombreDistancia());
        LblControlesPausa.Text = idioma.O("pausa.controles");
        EntradaChat.Placeholder = idioma.O("chat.placeholder");

        // Construir el mundo local desde los datos comprimidos del servidor
        var mundo = Mundo.Deserializar(Mundo.Descomprimir(datos.MundoComprimido));
        _vista.Mundo = mundo;
        _vista.Renderizador.DistanciaChunks = Distancias[_nivelDistancia];
        _vista.Renderizador.ConstruirMallas(mundo);
        _vista.Jugador.Pos = new Vector3(datos.Ax, datos.Ay, datos.Az);
        _vista.Jugador.Yaw = 0;
        _vista.Jugador.Pitch = 0;
        ActualizarLblBloque();

        AgregarChat(_idioma.O("chat.sistema", _idioma.O("juego.bienvenido", datos.Nombre)));

        _red.AlDesconectar += OnDesconectadoRed;
        _teclado.AlPulsar += OnTecla;
        }
        catch (Exception ex)
        {
            Diag.Log("PaginaJuego ctor: " + ex);
            throw;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if WINDOWS
        // Enlazar el teclado de la ventana (una sola vez por ejecución)
        if (!_tecladoVinculado && Application.Current?.Windows.Count > 0 &&
            Application.Current.Windows[0].Handler?.PlatformView is Microsoft.UI.Xaml.Window ventana && ventana.Content != null)
        {
            _teclado.Vincular(ventana.Content);
            _tecladoVinculado = true;
        }
#endif
        _reloj.Restart();
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
    }

    // ------------------------------------------------------------- bucle

    void OnTick(object? s, EventArgs e)
    {
        try
        {
            TickInterno();
        }
        catch (Exception ex)
        {
            Diag.Log("OnTick: " + ex);
        }
    }

    void TickInterno()
    {
        float dt = Math.Clamp((float)_reloj.Elapsed.TotalSeconds, 0.001f, 0.06f);
        _reloj.Restart();

        while (_red.Obtener() is Mensaje m) ProcesarRed(m);

        bool chatAbierto = EntradaChat.IsFocused;
        if (!_pausado && !chatAbierto)
        {
            _vista.Tick(dt, _teclado.EstaPulsada, _esMovil);

            if (_vista.ConsumirRomper())
            {
                int mobId = _vista.BuscarMobApuntado();
                if (mobId >= 0)
                {
                    _red.Enviar(new GolpearMob { Id = mobId });
                }
                else
                {
                    var g = _vista.GolpeActual;
                    _red.Enviar(new RomperBloque { X = g.X, Y = g.Y, Z = g.Z });
                }
            }
            if (_vista.ConsumirColocar())
            {
                var g = _vista.GolpeActual;
                int tx = g.X + (int)g.Normal.X, ty = g.Y + (int)g.Normal.Y, tz = g.Z + (int)g.Normal.Z;
                _red.Enviar(new ColocarBloque { X = tx, Y = ty, Z = tz, Bloque = _vista.BloqueSeleccionado });
            }

            _ticksPosicion++;
            if (_ticksPosicion >= 3) // cada ~100 ms
            {
                _ticksPosicion = 0;
                var p = _vista.Jugador.Pos;
                _red.Enviar(new Posicion
                {
                    Id = _red.MiId,
                    Px = p.X, Py = p.Y, Pz = p.Z,
                    Ry = _vista.Jugador.Yaw,
                    Pitch = _vista.Jugador.Pitch,
                });
            }
        }

        _frames++;
        if (_frames >= 10)
        {
            _fps = 10f / dt;
            _frames = 0;
        }

        var pos = _vista.Jugador.Pos;
        LblCoordenadas.Text = _idioma.O("juego.coordenadas", pos.X, pos.Y, pos.Z);
        LblFps.Text = _idioma.O("juego.fps", (int)MathF.Round(_fps));
        LblJugadores.Text = _idioma.O("juego.jugadores_conectados", _vista.Remotos.Count + 1);

        DispararRender();
    }

    /// <summary>Render en segundo plano: no bloquea el hilo de la UI.</summary>
    void DispararRender()
    {
        if (_renderizando) return;
        _renderizando = true;

        double vw = Vista.Width > 1 ? Vista.Width : 640;
        double vh = Vista.Height > 1 ? Vista.Height : 360;
        const int anchoMax = 480;
        int rw = anchoMax;
        int rh = (int)(anchoMax * vh / vw);
        if (rh < 1) rh = 1;

        var camPos = _vista.Cam.Pos;
        var yaw = _vista.Cam.Yaw;
        var pitch = _vista.Cam.Pitch;
        var cajas = new List<VistaJuego.CajaJugador>();        foreach (var j in _vista.Remotos.Values)
            cajas.Add(new VistaJuego.CajaJugador(
                new Vector3(j.Pos.X - 0.3f, j.Pos.Y, j.Pos.Z - 0.3f),
                new Vector3(j.Pos.X + 0.3f, j.Pos.Y + 1.8f, j.Pos.Z + 0.3f),
                j.Color));
        foreach (var m in _vista.Mobs.Values)
            VistaJuego.AgregarMobFigura(cajas, m.Tipo, m.Pos, m.Ry);
        foreach (var d in _vista.Drops.Values)
        {
            const float s = 0.25f;
            cajas.Add(new VistaJuego.CajaJugador(
                new Vector3(d.Pos.X - s, d.Pos.Y, d.Pos.Z - s),
                new Vector3(d.Pos.X + s, d.Pos.Y + 0.5f, d.Pos.Z + s),
                VistaJuego.ColorMaterial(d.Material)));
        }
        // Herramienta en mano (si el slot seleccionado es una herramienta)
        var camTemp = new Camara { Pos = camPos, Yaw = yaw, Pitch = pitch };
        var itemMano = _vista.ItemEnMano;
        if (Objetos.TipoDe(itemMano) != TipoHerramienta.Ninguna)
            _vista.AgregarHerramientaMano(cajas, itemMano, camTemp);
        var cajasArr = cajas.ToArray();

        _ = Task.Run(() =>
        {
            try
            {
                var bmp = _vista.RenderFrame(rw, rh, camPos, yaw, pitch, cajasArr);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        Pantalla.Source = ImageSource.FromStream(() => new MemoryStream(bmp));
                    }
                    catch (Exception ex)
                    {
                        Diag.Log("blit: " + ex);
                    }
                    finally
                    {
                        _renderizando = false;
                    }
                    Vista.Invalidate();
                });
            }
            catch (Exception ex)
            {
                Diag.Log("render fondo: " + ex);
                MainThread.BeginInvokeOnMainThread(() => _renderizando = false);
            }
        });
    }

    void ProcesarRed(Mensaje m)
    {
        switch (m)
        {
            case BloqueCambio bc:
                _vista.Mundo.Poner(bc.X, bc.Y, bc.Z, bc.Bloque);
                _vista.Renderizador.ReconstruirAlrededor(_vista.Mundo, bc.X, bc.Y, bc.Z);
                break;

            case Posiciones ps:
                foreach (var p in ps.Jugadores)
                {
                    if (p.Id == _red.MiId) continue;
                    _vista.Remotos[p.Id] = new VistaJuego.JugadorRemoto(
                        p.Id, NombreDe(p.Id), new Vector3(p.Px, p.Py, p.Pz), p.Ry, p.Pitch, ColorRemoto(p.Id));
                }
                break;

            case JugadorEntro je:
                if (je.Id == _red.MiId) break;
                _nombres[je.Id] = je.Nombre;
                _vista.Remotos[je.Id] = new VistaJuego.JugadorRemoto(
                    je.Id, je.Nombre, new Vector3(je.Px, je.Py, je.Pz), 0, 0, ColorRemoto(je.Id));
                AgregarChat(_idioma.O("chat.entro", je.Nombre));
                break;

            case JugadorSalio js:
                _nombres.Remove(js.Id);
                _vista.Remotos.Remove(js.Id);
                AgregarChat(_idioma.O("chat.salio", js.Nombre));
                break;

            case Mobs ms:
                _vista.Mobs.Clear();
                foreach (var e in ms.Lista)
                    _vista.Mobs[e.Id] = new VistaJuego.MobRemoto((TipoMob)e.Tipo, new Vector3(e.Px, e.Py, e.Pz), e.Ry, e.Salud, e.MaxSalud);
                break;

            case Drops ds:
                _vista.Drops.Clear();
                foreach (var d in ds.Lista)
                    _vista.Drops[d.Id] = new VistaJuego.DropRemoto(d.Material, new Vector3(d.Px, d.Py, d.Pz));
                break;

            case Inventario inv:
                _inventario = inv.Slots;
                RellenarSlots();
                break;

            case TiempoMundo tm:
                _vista.Renderizador.AplicarHora(tm.Hora);
                break;

            case JugadorSalud js:
                _vista.Salud = js.Salud;
                _vista.MaxSalud = js.MaxSalud;
                break;

            case Respawn rp:
                _vista.Jugador.Pos = new Vector3(rp.Px, rp.Py, rp.Pz);
                _vista.Jugador.Vel = Vector3.Zero;
                break;

            case Chat ch:
                AgregarChat($"{ch.Nombre}: {ch.Texto}");
                break;

            case ErrorServidor er:
                AgregarChat("✖ " + TextoError(er));
                if (er.Codigo == "MUNDO_BORRADO")
                    _ = SalirAlMenu();
                break;
        }
    }

    string NombreDe(int id) => _nombres.TryGetValue(id, out var n) ? n : $"Jugador {id}";
    Color ColorRemoto(int id) => ColoresRemoto[Math.Abs(id) % ColoresRemoto.Length];

    string TextoError(ErrorServidor er)
    {
        var clave = "error." + er.Codigo.ToLowerInvariant();
        return _idioma.Lang.Contiene(clave) ? _idioma.O(clave) : er.Mensaje;
    }

    void AgregarChat(string linea)
    {
        _chat.Add(linea);
        if (_chat.Count > 60) _chat.RemoveAt(0);
    }

    // ------------------------------------------------------------- teclado

    void OnTecla(int codigo)
    {
        if (codigo == Teclas.Escape)
        {
            if (EntradaChat.IsVisible) OcultarChat();
            else MainThread.BeginInvokeOnMainThread(AlternarPausa);
            return;
        }
        if (codigo == Teclas.T)
        {
            if (!_pausado && !EntradaChat.IsVisible) MostrarChat();
            return;
        }
        if (_pausado) return;
        if (codigo == Teclas.F) { AlternarVolar(); return; }
        if (codigo == Teclas.R) { _vista.PedirRomper(); return; }
        if (codigo == Teclas.E) { AlternarInventario(); return; }
        if (codigo == Teclas.Q)
        {
            _red.Enviar(new SoltarItem { Slot = _vista.Slot });
            return;
        }
        if (codigo == Teclas.U) { UsarItemApuntado(); return; }
        if (codigo >= Teclas.Num1 && codigo <= Teclas.Num9)
        {
            _vista.Slot = codigo - Teclas.Num1;
            var mat = _vista.Hotbar[_vista.Slot].Material;
            _red.Enviar(new SeleccionarSlot { Slot = _vista.Slot, Material = mat });
            ActualizarLblBloque();
        }
    }

    /// <summary>Usa el item en mano sobre el bloque apuntado (azada, semillas, planton, mechero...).</summary>
    void UsarItemApuntado()
    {
        var g = _vista.GolpeActual;
        _red.Enviar(new UsarBloque { X = g.X, Y = g.Y, Z = g.Z });
    }

    // ------------------------------------------------------------- chat

    void MostrarChat()
    {
        EntradaChat.IsVisible = true;
        EntradaChat.Focus();
    }

    void OcultarChat()
    {
        EntradaChat.IsVisible = false;
        EntradaChat.Unfocus();
    }

    void OnChatEnviado(object? sender, EventArgs e)
    {
        var texto = EntradaChat.Text?.Trim() ?? "";
        EntradaChat.Text = "";
        OcultarChat();
        if (texto.Length > 0) _red.Enviar(new Chat { Texto = texto });
    }

    void OnBtnChat(object? sender, EventArgs e) => MostrarChat();

    // ------------------------------------------------------------- acciones

    void OnBtnRomper(object? sender, EventArgs e) => _vista.PedirRomper();
    void OnBtnColocar(object? sender, EventArgs e) => _vista.PedirColocar();
    void OnSaltarPulsado(object? sender, EventArgs e) => _vista.BotonSaltar = true;
    void OnSaltarSoltado(object? sender, EventArgs e) => _vista.BotonSaltar = false;

    // ------------------------------------------------------------- inventario (tipo Minecraft)

    void OnInventario(object? sender, EventArgs e) => AlternarInventario();
    void OnCerrarInv(object? sender, EventArgs e) => AlternarInventario();

    void AlternarInventario()
    {
        if (PanelInv.IsVisible)
        {
            PanelInv.IsVisible = false;
            _pausado = false;
        }
        else
        {
            Pausa.IsVisible = false;
            _pausado = true;
            PanelInv.IsVisible = true;
            _gridTamaño = HayMesaCerca() ? 3 : 2;
            LblInvCrafteo.Text = _gridTamaño == 3 ? "Mesa de trabajo (3x3)" : "Crafteo (2x2)";
            RefrescarCrafteo();
            RefrescarInventario();
        }
    }

    /// <summary>Detecta si hay una mesa de trabajo cerca (habilita el grid 3x3).</summary>
    bool HayMesaCerca()
    {
        var p = _vista.Jugador.Pos;
        var m = _vista.Mundo;
        int r = 6;
        for (int x = (int)p.X - r; x <= (int)p.X + r; x++)
            for (int y = (int)p.Y - r; y <= (int)p.Y + r; y++)
                for (int z = (int)p.Z - r; z <= (int)p.Z + r; z++)
                    if (m.Obtener(x, y, z) == Bloques.Mesa) return true;
        return false;
    }

    void ConstruirPanelInventario()
    {
        // Cuadrícula de crafteo 3x3
        for (int i = 0; i < 3; i++)
        {
            GridCraft.RowDefinitions.Add(new RowDefinition { Height = 50 });
            GridCraft.ColumnDefinitions.Add(new ColumnDefinition { Width = 50 });
        }
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                int ix = x, iy = y;
                var b = NuevoSlot();
                b.Clicked += (s, e) => OnSlotCraft(ix, iy);
                Grid.SetRow(b, y); Grid.SetColumn(b, x);
                GridCraft.Children.Add(b);
                _botonesCraft[y, x] = b;
            }

        // Cuadrícula de inventario 3x9
        for (int i = 0; i < 3; i++) GridInv.RowDefinitions.Add(new RowDefinition { Height = 50 });
        for (int i = 0; i < 9; i++) GridInv.ColumnDefinitions.Add(new ColumnDefinition { Width = 50 });
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 9; x++)
            {
                int ix = x, iy = y;
                var b = NuevoSlot();
                b.Clicked += (s, e) => OnSlotInv(iy, ix);
                Grid.SetRow(b, y); Grid.SetColumn(b, x);
                GridInv.Children.Add(b);
                _botonesInv[y, x] = b;
            }

        // Botones de cocina (horno)
        for (int i = 0; i < Objetos.RecetasCocina.Length; i++)
        {
            var b = new Button { Text = Objetos.RecetasCocina[i].Nombre, CommandParameter = i, FontSize = 12 };
            b.Clicked += OnCocinar;
            ListaCocina.Children.Add(b);
        }
    }

    static Button NuevoSlot() => new()
    {
        BackgroundColor = Color.FromArgb("#333a45"),
        TextColor = Colors.White,
        FontSize = 14,
        Padding = 0,
        CornerRadius = 4,
    };

    void OnSlotCraft(int x, int y) { IntercambiarCon(ref _grid[y * 3 + x]); RefrescarCrafteo(); }
    void OnSlotInv(int y, int x) { IntercambiarCon(ref _slots[y * 9 + x]); RefrescarInventario(); }

    void IntercambiarCon(ref SlotUI slot)
    {
        if (_cursorMaterial == 0)
        {
            if (slot.Cantidad > 0)
            {
                _cursorMaterial = slot.Material;
                _cursorCantidad = slot.Cantidad;
                slot = default;
            }
        }
        else if (slot.Cantidad == 0)
        {
            slot.Material = _cursorMaterial;
            slot.Cantidad = _cursorCantidad;
            _cursorMaterial = 0; _cursorCantidad = 0;
        }
        else if (slot.Material == _cursorMaterial)
        {
            slot.Cantidad += _cursorCantidad;
            _cursorMaterial = 0; _cursorCantidad = 0;
        }
        else
        {
            var tmp = slot;
            slot.Material = _cursorMaterial;
            slot.Cantidad = _cursorCantidad;
            _cursorMaterial = tmp.Material;
            _cursorCantidad = tmp.Cantidad;
        }
    }

    void PintarSlot(Button b, in SlotUI slot)
    {
        if (slot.Material == 0 || slot.Cantidad <= 0)
        {
            b.BackgroundColor = Color.FromArgb("#333a45");
            b.Text = "";
        }
        else
        {
            var (r, g, bl) = Objetos.Color(slot.Material);
            b.BackgroundColor = Color.FromRgb(r, g, bl);
            b.Text = slot.Cantidad > 1 ? slot.Cantidad.ToString() : "";
        }
    }

    void RefrescarCrafteo()
    {
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                bool activo = x < _gridTamaño && y < _gridTamaño;
                var b = _botonesCraft[y, x];
                if (!activo)
                {
                    b.IsEnabled = false;
                    b.BackgroundColor = Color.FromArgb("#161b22");
                    b.Text = "";
                }
                else
                {
                    b.IsEnabled = true;
                    PintarSlot(b, _grid[y * 3 + x]);
                }
            }
        ActualizarResultado();
    }

    void RefrescarInventario()
    {
        for (int i = 0; i < 27; i++)
            PintarSlot(_botonesInv[i / 9, i % 9], _slots[i]);
        LblCursor.Text = _cursorMaterial == 0
            ? "Cursor: (vacío)"
            : $"Cursor: {Objetos.Nombre(_cursorMaterial)} x {_cursorCantidad}";
    }

    void ActualizarResultado()
    {
        var grid = new ushort[_gridTamaño * _gridTamaño];
        for (int y = 0; y < _gridTamaño; y++)
            for (int x = 0; x < _gridTamaño; x++)
                grid[y * _gridTamaño + x] = _grid[y * 3 + x].Material;
        _indiceResultado = Objetos.CoincidirReceta(grid, _gridTamaño, _gridTamaño);
        if (_indiceResultado >= 0)
        {
            var r = Objetos.RecetasCrafteo[_indiceResultado];
            var (cr, cg, cb) = Objetos.Color(r.Salida);
            BtnResultado.BackgroundColor = Color.FromRgb(cr, cg, cb);
            BtnResultado.Text = r.SalidaCantidad > 1 ? r.SalidaCantidad.ToString() : "";
            BtnResultado.IsEnabled = true;
            LblRecetaInfo.Text = r.Nombre;
        }
        else
        {
            BtnResultado.BackgroundColor = Color.FromArgb("#333a45");
            BtnResultado.Text = "";
            BtnResultado.IsEnabled = false;
            LblRecetaInfo.Text = "";
        }
    }

    void OnResultado(object? sender, EventArgs e)
    {
        if (_indiceResultado < 0) return;
        _red.Enviar(new Craftear { Receta = _indiceResultado });
        Array.Clear(_grid);
        _indiceResultado = -1;
        RefrescarCrafteo();
        RefrescarInventario();
    }

    void RellenarSlots()
    {
        Array.Clear(_slots);
        int i = 0;
        foreach (var s in _inventario)
        {
            if (i >= 27) break;
            _slots[i] = new SlotUI { Material = s.Material, Cantidad = s.Cantidad };
            i++;
        }
        Array.Clear(_grid);
        _indiceResultado = -1;
        RefrescarInventario();
        RefrescarCrafteo();
        // Hotbar: primeros 9 slots
        for (int k = 0; k < 9; k++)
            _vista.Hotbar[k] = (_slots[k].Material, _slots[k].Cantidad);
    }

    void OnCocinar(object? sender, EventArgs e)
    {
        if (sender is Button b && int.TryParse(b.CommandParameter?.ToString(), out int r))
            _red.Enviar(new Cocinar { Receta = r });
    }

    void AlternarVolar()
    {
        _vista.Volando = !_vista.Volando;
        BtnPausaVolar.Text = _idioma.O("juego.volando", _vista.Volando ? _idioma.O("juego.si") : _idioma.O("juego.no"));
    }

    void ActualizarLblBloque()
    {
        var mat = _vista.BloqueSeleccionado;
        LblBloque.Text = mat == 0 ? "—" : Objetos.Nombre(mat);
    }

    void OnCambiarDistancia(object? sender, EventArgs e)
    {
        _nivelDistancia = (_nivelDistancia + 1) % Distancias.Length;
        _vista.Renderizador.DistanciaChunks = Distancias[_nivelDistancia];
        BtnDistancia.Text = _idioma.O("juego.distancia", NombreDistancia());
    }

    string NombreDistancia() => _idioma.O(_nivelDistancia switch
    {
        0 => "juego.distancia_baja",
        1 => "juego.distancia_media",
        _ => "juego.distancia_alta",
    });

    // ------------------------------------------------------------- pausa

    void AlternarPausa()
    {
        _pausado = !_pausado;
        Pausa.IsVisible = _pausado;
        if (_pausado)
        {
            BtnBorrarMundo.IsVisible = _red.MiId == _datos.IdDueno;
            BtnPausaVolar.Text = _idioma.O("juego.volando", _vista.Volando ? _idioma.O("juego.si") : _idioma.O("juego.no"));
        }
    }

    void OnPausa(object? sender, EventArgs e) => AlternarPausa();
    void OnBtnVolar(object? sender, EventArgs e) => AlternarVolar();

    async void OnBorrarMundo(object? sender, EventArgs e)
    {
        bool ok = await DisplayAlert(_idioma.O("mundos.borrar_confirmar", _datos.Nombre), "", T.Borrar, "✕");
        if (ok) _red.Enviar(new BorrarMundo { Id = _datos.Id });
    }

    async void OnSalirMundo(object? sender, EventArgs e)
    {
        _red.Enviar(new Salir());
        await SalirAlMenu();
    }

    async void OnDesconectar(object? sender, EventArgs e)
    {
        _red.Desconectar();
        await SalirAlMenu();
    }

    async Task SalirAlMenu()
    {
        if (_saliendo) return;
        _saliendo = true;
        _timer?.Stop();
        if (Navigation?.NavigationStack.Count > 1)
            await Navigation.PopAsync();
        else
            await Navigation.PopToRootAsync();
    }

    void OnDesconectadoRed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_saliendo) return;
            AgregarChat("✖ " + _idioma.O("error.desconectado"));
            await Task.Delay(600);
            await SalirAlMenu();
        });
    }

    protected override bool OnBackButtonPressed()
    {
        if (EntradaChat.IsVisible) OcultarChat();
        else AlternarPausa();
        return true;
    }
}
