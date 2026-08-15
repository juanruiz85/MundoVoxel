using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace MundoVoxel.Core;

/// <summary>
/// Servidor multijugador: mantiene varios mundos en memoria.
/// Cada mundo puede ser pÃºblico (cualquiera entra) o privado (requiere clave de 4 dÃ­gitos).
/// El creador puede borrar su mundo; los mundos vacÃ­os se conservan en memoria para volver luego.
/// </summary>
public sealed class GameServer : IAsyncDisposable
{
    public int Puerto { get; }
    public string NombreServidor { get; }
    public int MaxMundos { get; }
    public int MaxJugadoresPorMundo { get; }

    readonly TcpListener _oyente;
    readonly CancellationTokenSource _cts = new();
    readonly ConcurrentDictionary<int, ConexionJugador> _conexiones = new();
    readonly Dictionary<string, MundoServidor> _mundos = new();
    readonly object _cerrojo = new();
    int _siguienteId = 1;

    public event Action<string>? AlRegistrar;

    public GameServer(int puerto = 25575, string nombreServidor = "MundoVoxel", int maxMundos = 40, int maxJugadoresPorMundo = 12)
    {
        Puerto = puerto;
        NombreServidor = nombreServidor;
        MaxMundos = maxMundos;
        MaxJugadoresPorMundo = maxJugadoresPorMundo;
        _oyente = new TcpListener(IPAddress.Any, puerto);
    }

    public bool EnEjecucion { get; private set; }

    public void Iniciar()
    {
        try
        {
            _oyente.Start();
        }
        catch (SocketException ex)
        {
            Log($"No se pudo abrir el puerto {Puerto}: {ex.Message} (Â¿ya hay otro servidor corriendo?).");
            return;
        }
        EnEjecucion = true;
        Log($"Servidor Â«{NombreServidor}Â» escuchando en el puerto {Puerto}. Mundos en memoria: {MaxMundos} mÃ¡x., {MaxJugadoresPorMundo} jugadores por mundo.");
        _ = AceptarCicloAsync(_cts.Token);
        _ = CicloPosicionesAsync(_cts.Token);
        _ = CicloMobsAsync(_cts.Token);
        _ = CicloMundoAsync(_cts.Token);
    }

    /// <summary>
    /// Carga `mobs.config.json` (junto al ejecutable) para ajustar el comportamiento
    /// de los mobs sin recompilar: velocidad, radio de agresion y daño por tipo.
    /// </summary>
    void CargarConfigMobs()
    {
        try
        {
            var ruta = Path.Combine(AppContext.BaseDirectory, "mobs.config.json");
            if (!File.Exists(ruta)) return;
            var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ruta));
            var cfg = new Dictionary<TipoMob, InfoMob>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!Enum.TryParse<TipoMob>(prop.Name, true, out var tipo)) continue;
                var e = prop.Value;
                cfg[tipo] = new InfoMob(
                    e.TryGetProperty("Ancho", out var a) ? (float)a.GetDouble() : 0f,
                    e.TryGetProperty("Alto", out var al) ? (float)al.GetDouble() : 0f,
                    e.TryGetProperty("Hostil", out var h) ? h.GetBoolean() : false,
                    e.TryGetProperty("Velocidad", out var v) ? (float)v.GetDouble() : 0f,
                    e.TryGetProperty("AreaAgresion", out var aa) ? (float)aa.GetDouble() : -1f,
                    e.TryGetProperty("Danio", out var d) ? (float)d.GetDouble() : 0f);
            }
            MobsInfo.AplicarConfig(cfg);
            Log($"Config de mobs cargada: {cfg.Count} tipos ajustados desde mobs.config.json.");
        }
        catch (Exception ex)
        {
            Log($"No se pudo leer mobs.config.json: {ex.Message}");
        }
    }

    public async Task DetenerAsync()
    {
        _cts.Cancel();
        _oyente.Stop();
        foreach (var c in _conexiones.Values.ToList()) c.Cerrar();
        EnEjecucion = false;
        await Task.Delay(50);
        Log("Servidor detenido.");
    }

    public ValueTask DisposeAsync() => new(DetenerAsync());

    void Log(string msg) => AlRegistrar?.Invoke(msg);

    // ------------------------------------------------------------------ conexiones

    sealed class ConexionJugador
    {
        public int Id;
        public string Nombre = "";
        public TcpClient Tcp = null!;
        public NetworkStream Flujo = null!;
        public string? MundoId;
        public Vector3 Pos;
        public float Ry, Pitch;
        public bool EnMundo;
        public int Slot;              // slot seleccionado de la hotbar (0-8)
        public ushort Mano;           // material que el jugador tiene en la mano (validado)
        public int Salud = 20;         // vida del jugador (max 20)
        public float TiempoGolpe;      // cooldown para recibir dano de mobs
        public readonly List<SlotInventario> Inventario = new();

        public void Cerrar()
        {
            try { Tcp.Close(); } catch { }
        }
    }

    sealed class MundoServidor
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Nombre = "";
        public string Pin = "";
        public bool Abierto = true;
        public Mundo Mundo = null!;
        public int IdDueno;
        public string NombreDueno = "";
        public readonly Dictionary<int, ConexionJugador> Jugadores = new();
        public readonly List<Mob> Mobs = new();
        public int SiguienteMobId;
        public readonly List<Drop> Drops = new();
        public int SiguienteDropId;
        public float Hora = 8f;       // ciclo dia/noche: 0-24h (empieza de manana)
        public readonly List<(int x, int y, int z, float t)> Tnts = new();
        public int Conteo => Jugadores.Count;
    }

    async Task AceptarCicloAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;
            try { tcp = await _oyente.AcceptTcpClientAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
            _ = AtenderClienteAsync(tcp, ct);
        }
    }

    async Task AtenderClienteAsync(TcpClient tcp, CancellationToken ct)
    {
        tcp.NoDelay = true;
        var conn = new ConexionJugador { Id = Interlocked.Increment(ref _siguienteId), Tcp = tcp };
        try
        {
            conn.Flujo = tcp.GetStream();
            _conexiones[conn.Id] = conn;
            while (!ct.IsCancellationRequested)
            {
                var msg = await Frames.LeerAsync(conn.Flujo, ct);
                if (msg == null) break;
                try { Procesar(conn, msg); }
                catch (Exception ex) { Log($"Error procesando mensaje de {conn.Nombre}: {ex.Message}"); }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException) { }
        finally
        {
            Desconectar(conn);
        }
    }

    void Desconectar(ConexionJugador c)
    {
        _conexiones.TryRemove(c.Id, out _);
        SalirDelMundo(c, notificar: true);
        c.Cerrar();
        if (!string.IsNullOrEmpty(c.Nombre)) Log($"{c.Nombre} se desconectÃ³.");
    }

    // ------------------------------------------------------------------ mensajes

    void Procesar(ConexionJugador c, Mensaje m)
    {
        switch (m)
        {
            case Hola h:
                var nombre = (h.Nombre ?? "").Trim();
                c.Nombre = nombre.Length == 0 ? "Jugador" + c.Id : nombre[..Math.Min(20, nombre.Length)];
                Enviar(c, new Bienvenido { IdJugador = c.Id, NombreServidor = NombreServidor });
                Enviar(c, ListaMundosActual());
                Log($"{c.Nombre} se conectÃ³ ({c.Tcp.Client.RemoteEndPoint}).");
                break;

            case ListarMundos:
                Enviar(c, ListaMundosActual());
                break;

            case CrearMundo cm:
                CrearMundo(c, cm);
                break;

            case Unirse u:
                UnirseMundo(c, u);
                break;

            case Salir:
                if (c.EnMundo) SalirDelMundo(c, notificar: true);
                break;

            case Posicion p:
                if (c.EnMundo)
                    lock (_cerrojo) { c.Pos = new Vector3(p.Px, p.Py, p.Pz); c.Ry = p.Ry; c.Pitch = p.Pitch; }
                break;

            case RomperBloque rb:
                Romper(c, rb);
                break;

            case ColocarBloque cb:
                Colocar(c, cb);
                break;

            case SoltarItem si:
                SoltarItem(c, si);
                break;

            case UsarBloque ub:
                Usar(c, ub);
                break;

            case SeleccionarSlot ss:
                c.Slot = Math.Clamp(ss.Slot, 0, 8);
                c.Mano = Contar(c, ss.Material) > 0 ? ss.Material : (ushort)0;
                break;

            case Chat ch:
                if (c.EnMundo)
                {
                    var texto = (ch.Texto ?? "").Trim();
                    if (texto.Length == 0) return;
                    if (texto.Length > 200) texto = texto[..200];
                    Broadcast(c.MundoId!, new Chat { Nombre = c.Nombre, Texto = texto });
                }
                break;

            case BorrarMundo bm:
                BorrarMundo(c, bm);
                break;

            case GolpearMob gm:
                GolpearMob(c, gm);
                break;

            case Craftear cr:
                Craftear(c, cr);
                break;

            case Cocinar co:
                Cocinar(c, co);
                break;
        }
    }

    ListaMundos ListaMundosActual()
    {
        lock (_cerrojo)
        {
            return new ListaMundos
            {
                Mundos = _mundos.Values.Select(mm => new InfoMundo
                {
                    Id = mm.Id,
                    Nombre = mm.Nombre,
                    Dueno = mm.NombreDueno,
                    IdDueno = mm.IdDueno,
                    Abierto = mm.Abierto,
                    Jugadores = mm.Conteo,
                    MaxJugadores = MaxJugadoresPorMundo,
                }).ToList()
            };
        }
    }

    void NotificarListas()
    {
        var lista = ListaMundosActual();
        foreach (var c in _conexiones.Values) Enviar(c, lista);
    }

    // ------------------------------------------------------------------ mundos

    void CrearMundo(ConexionJugador c, CrearMundo cm)
    {
        lock (_cerrojo)
        {
            if (_mundos.Count >= MaxMundos)
            {
                Enviar(c, new ErrorServidor { Codigo = "LIMITE_MUNDOS", Mensaje = "El servidor llegÃ³ al lÃ­mite de mundos." });
                return;
            }
            var nombre = (cm.Nombre ?? "").Trim();
            if (nombre.Length == 0)
            {
                Enviar(c, new ErrorServidor { Codigo = "NOMBRE_VACIO", Mensaje = "El nombre del mundo no puede estar vacÃ­o." });
                return;
            }
            nombre = nombre[..Math.Min(24, nombre.Length)];
            string pin = "";
            if (!cm.Abierto)
            {
                pin = (cm.Pin ?? "").Trim();
                if (pin.Length != 4 || !pin.All(char.IsAsciiDigit))
                {
                    Enviar(c, new ErrorServidor { Codigo = "PIN_INVALIDO", Mensaje = "La clave debe tener exactamente 4 dÃ­gitos." });
                    return;
                }
            }
            var mundo = new MundoServidor
            {
                Nombre = nombre,
                Pin = pin,
                Abierto = cm.Abierto,
                IdDueno = c.Id,
                NombreDueno = c.Nombre,
                Mundo = Mundo.Generar(cm.Semilla != 0 ? cm.Semilla : (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF)),
            };
            GenerarMobs(mundo);
            _mundos[mundo.Id] = mundo;
            Log($"{c.Nombre} creÃ³ el mundo Â«{nombre}Â» ({(cm.Abierto ? "pÃºblico" : "privado")}).");
            Enviar(c, new MundoCreado { Id = mundo.Id });
            UnirseInterno(c, mundo);
            NotificarListas();
        }
    }

    void UnirseMundo(ConexionJugador c, Unirse u)
    {
        lock (_cerrojo)
        {
            if (!_mundos.TryGetValue(u.Id, out var mundo))
            {
                Enviar(c, new ErrorServidor { Codigo = "NO_EXISTE", Mensaje = "El mundo ya no existe." });
                return;
            }
            if (mundo.Jugadores.ContainsKey(c.Id)) return;
            if (mundo.Conteo >= MaxJugadoresPorMundo)
            {
                Enviar(c, new ErrorServidor { Codigo = "LLENO", Mensaje = "El mundo estÃ¡ lleno." });
                return;
            }
            if (!mundo.Abierto && (u.Pin ?? "") != mundo.Pin)
            {
                Enviar(c, new ErrorServidor { Codigo = "PIN_INCORRECTO", Mensaje = "Clave incorrecta." });
                return;
            }
            if (c.EnMundo) SalirDelMundo(c, notificar: true);
            UnirseInterno(c, mundo);
            NotificarListas();
        }
    }

    void UnirseInterno(ConexionJugador c, MundoServidor mundo)
    {
        var aparicion = mundo.Mundo.ObtenerPuntoAparicion();
        c.MundoId = mundo.Id;
        c.EnMundo = true;
        c.Pos = aparicion;
        c.Ry = 0; c.Pitch = 0;
        mundo.Jugadores[c.Id] = c;
        Enviar(c, new Unido
        {
            Id = mundo.Id,
            Nombre = mundo.Nombre,
            Dueno = mundo.NombreDueno,
            IdDueno = mundo.IdDueno,
            MundoComprimido = Mundo.Comprimir(mundo.Mundo.Serializar()),
            Ax = aparicion.X, Ay = aparicion.Y, Az = aparicion.Z,
        });
        // Kit de inicio la primera vez que el jugador entra a un mundo
        if (c.Inventario.Count == 0)
        {
            AgregarInventario(c, Bloques.Madera, 10);
            AgregarInventario(c, Bloques.Tierra, 10);
            AgregarInventario(c, Bloques.Piedra, 5);
            AgregarInventario(c, Bloques.Arena, 5);
            AgregarInventario(c, (ushort)ItemId.Palo, 8);
            AgregarInventario(c, Bloques.Antorcha, 2);
            AgregarInventario(c, (ushort)ItemId.SemillasTrigo, 4);
            AgregarInventario(c, (ushort)ItemId.Mechero, 1);
            Enviar(c, InventarioActual(c));
        }
        Broadcast(mundo.Id, new JugadorEntro { Id = c.Id, Nombre = c.Nombre, Px = aparicion.X, Py = aparicion.Y, Pz = aparicion.Z });
        Log($"{c.Nombre} entrÃ³ al mundo Â«{mundo.Nombre}Â».");
    }

    void SalirDelMundo(ConexionJugador c, bool notificar)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo || c.MundoId == null) return;
            var id = c.MundoId;
            c.MundoId = null;
            c.EnMundo = false;
            if (_mundos.TryGetValue(id, out var mundo))
            {
                mundo.Jugadores.Remove(c.Id);
                if (notificar) Broadcast(id, new JugadorSalio { Id = c.Id, Nombre = c.Nombre });
                // El mundo se mantiene en memoria aunque quede vacÃ­o: se puede volver a entrar despuÃ©s.
                Log($"{c.Nombre} saliÃ³ del mundo Â«{mundo.Nombre}Â» (quedan {mundo.Conteo}).");
            }
        }
    }

    void BorrarMundo(ConexionJugador c, BorrarMundo bm)
    {
        lock (_cerrojo)
        {
            if (!_mundos.TryGetValue(bm.Id, out var mundo)) return;
            if (mundo.IdDueno != c.Id)
            {
                Enviar(c, new ErrorServidor { Codigo = "NO_DUENO", Mensaje = "Solo el creador del mundo puede borrarlo." });
                return;
            }
            var jugadores = mundo.Jugadores.Values.ToList();
            _mundos.Remove(bm.Id);
            foreach (var j in jugadores)
            {
                j.MundoId = null;
                j.EnMundo = false;
                Enviar(j, new ErrorServidor { Codigo = "MUNDO_BORRADO", Mensaje = "El mundo fue borrado por su creador." });
            }
            Log($"{c.Nombre} borrÃ³ el mundo Â«{mundo.Nombre}Â».");
            NotificarListas();
        }
    }

    // ------------------------------------------------------------------ bloques

    void Romper(ConexionJugador c, RomperBloque rb)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo || c.MundoId == null || !_mundos.TryGetValue(c.MundoId, out var mundo)) return;
            var m = mundo.Mundo;
            if (!m.Dentro(rb.X, rb.Y, rb.Z)) return;
            if (Vector3.Distance(c.Pos, new Vector3(rb.X + 0.5f, rb.Y + 0.5f, rb.Z + 0.5f)) > 7f) return;
            var actual = m.Obtener(rb.X, rb.Y, rb.Z);
            if (!Bloques.EsRompible(actual)) return;
            m.Poner(rb.X, rb.Y, rb.Z, Bloques.Aire);

            // La herramienta en la mano decide que cae (pico para piedra/menas, etc.)
            bool conPico = Objetos.EsPico(ItemEnMano(c));
            var rnd = Random.Shared;
            var drops = Objetos.DropAlRomper(actual, conPico, rnd);
            bool algo = false;
            foreach (var (material, cantidad) in drops)
            {
                AgregarInventario(c, material, cantidad);
                algo = true;
            }
            if (algo) Enviar(c, InventarioActual(c));
            Broadcast(mundo.Id, new BloqueCambio { X = rb.X, Y = rb.Y, Z = rb.Z, Bloque = Bloques.Aire });
        }
    }

    void Colocar(ConexionJugador c, ColocarBloque cb)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo || c.MundoId == null || !_mundos.TryGetValue(c.MundoId, out var mundo)) return;
            var m = mundo.Mundo;
            if (!m.Dentro(cb.X, cb.Y, cb.Z) || cb.Y <= 0) { Log($"DEBUG colocar: fuera ({cb.X},{cb.Y},{cb.Z})"); return; }
            if (!Bloques.EsColocable(cb.Bloque)) { Log($"DEBUG colocar: no colocable {cb.Bloque}"); return; }
            if (m.Obtener(cb.X, cb.Y, cb.Z) != Bloques.Aire) { Log($"DEBUG colocar: ocupado ({cb.X},{cb.Y},{cb.Z}) con {m.Obtener(cb.X, cb.Y, cb.Z)}"); return; }
            if (Vector3.Distance(c.Pos, new Vector3(cb.X + 0.5f, cb.Y + 0.5f, cb.Z + 0.5f)) > 7f) { Log($"DEBUG colocar: lejos pos=({c.Pos.X:F1},{c.Pos.Y:F1},{c.Pos.Z:F1})"); return; }
            // No colocar un bloque encima de otro jugador
            foreach (var j in mundo.Jugadores.Values)
            {
                if (MathF.Abs(j.Pos.X - (cb.X + 0.5f)) < 0.7f &&
                    MathF.Abs(j.Pos.Z - (cb.Z + 0.5f)) < 0.7f &&
                    j.Pos.Y + 1.8f > cb.Y && j.Pos.Y < cb.Y + 1f)
                    return;
            }
            m.Poner(cb.X, cb.Y, cb.Z, cb.Bloque);
            Broadcast(mundo.Id, new BloqueCambio { X = cb.X, Y = cb.Y, Z = cb.Z, Bloque = cb.Bloque });
        }
    }

    // ------------------------------------------------------------------ usar, soltar y mundo

    /// <summary>Item que el jugador tiene en la mano (lo que selecciono en la hotbar).</summary>
    static ushort ItemEnMano(ConexionJugador c)
    {
        if (c.Mano != 0 && Contar(c, c.Mano) > 0) return c.Mano;
        // Si no selecciono nada (o ya no tiene el item), usar la mejor herramienta disponible
        foreach (var s in c.Inventario)
            if (Objetos.EsPico(s.Material) || Objetos.EsAzada(s.Material) || Objetos.EsHacha(s.Material) || Objetos.EsEspada(s.Material))
                return s.Material;
        return 0;
    }
    void SoltarItem(ConexionJugador c, SoltarItem si)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo || c.MundoId == null || !_mundos.TryGetValue(c.MundoId, out var mundo)) return;
            if (si.Slot < 0 || si.Slot >= c.Inventario.Count) return;
            var s = c.Inventario[si.Slot];
            if (s.Cantidad <= 0) return;
            Quitar(c, s.Material, 1);
            Enviar(c, InventarioActual(c));
            // El item cae frente al jugador y se puede recoger
            float ya = c.Ry;
            var pos = c.Pos + new Vector3(MathF.Sin(ya) * 1.4f, 0.4f, -MathF.Cos(ya) * 1.4f);
            mundo.Drops.Add(new Drop { Id = ++mundo.SiguienteDropId, Material = s.Material, Px = pos.X, Py = pos.Y, Pz = pos.Z });
        }
    }

    void Usar(ConexionJugador c, UsarBloque ub)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo || c.MundoId == null || !_mundos.TryGetValue(c.MundoId, out var mundo)) return;
            var m = mundo.Mundo;
            if (!m.Dentro(ub.X, ub.Y, ub.Z)) return;
            if (Vector3.Distance(c.Pos, new Vector3(ub.X + 0.5f, ub.Y + 0.5f, ub.Z + 0.5f)) > 7f) return;
            var mano = ItemEnMano(c);
            var bloque = m.Obtener(ub.X, ub.Y, ub.Z);

            // Azada: labra la tierra / cesped
            if (Objetos.EsAzada(mano) && (bloque == Bloques.Tierra || bloque == Bloques.Cesped))
            {
                m.Poner(ub.X, ub.Y, ub.Z, Bloques.TierraLabrada);
                Broadcast(mundo.Id, new BloqueCambio { X = ub.X, Y = ub.Y, Z = ub.Z, Bloque = Bloques.TierraLabrada });
                return;
            }
            // Semillas: se plantan en tierra labrada
            if (Objetos.EsSemilla(mano) && bloque == Bloques.TierraLabrada)
            {
                if (m.Obtener(ub.X, ub.Y + 1, ub.Z) != Bloques.Aire) return;
                if (!Quitar(c, (ushort)ItemId.SemillasTrigo, 1)) return;
                Enviar(c, InventarioActual(c));
                m.Poner(ub.X, ub.Y + 1, ub.Z, Bloques.Trigo0);
                Broadcast(mundo.Id, new BloqueCambio { X = ub.X, Y = ub.Y + 1, Z = ub.Z, Bloque = Bloques.Trigo0 });
                return;
            }
            // Planton: se planta en tierra/cesped/tierra labrada
            if (Objetos.EsPlanton(mano) && (bloque == Bloques.Tierra || bloque == Bloques.Cesped || bloque == Bloques.TierraLabrada))
            {
                if (m.Obtener(ub.X, ub.Y + 1, ub.Z) != Bloques.Aire) return;
                if (!Quitar(c, Bloques.Planton, 1)) return;
                Enviar(c, InventarioActual(c));
                m.Poner(ub.X, ub.Y + 1, ub.Z, Bloques.Planton);
                Broadcast(mundo.Id, new BloqueCambio { X = ub.X, Y = ub.Y + 1, Z = ub.Z, Bloque = Bloques.Planton });
                return;
            }
            // Mechero: enciende la TNT
            if (Objetos.EsMechero(mano) && bloque == Bloques.Tnt)
            {
                if (mundo.Tnts.Any(t => t.x == ub.X && t.y == ub.Y && t.z == ub.Z)) return;
                mundo.Tnts.Add((ub.X, ub.Y, ub.Z, 3f));
                Log($"DEBUG tnt: encendida en ({ub.X},{ub.Y},{ub.Z}) mano={mano}");
                return;
            }
        }
    }

    /// <summary>Ciclo del mundo: dia/noche, crecimiento de plantas y TNT (2 Hz).</summary>
    async Task CicloMundoAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(500, ct); }
            catch (OperationCanceledException) { break; }
            lock (_cerrojo)
            {
                foreach (var mundo in _mundos.Values)
                {
                    if (mundo.Conteo == 0) continue;
                    // Ciclo dia/noche: 24 h en ~5 minutos reales
                    mundo.Hora = (mundo.Hora + 0.04f) % 24f;
                    foreach (var j in mundo.Jugadores.Values)
                        Enviar(j, new TiempoMundo { Hora = mundo.Hora });
                    CrecerPlantas(mundo);
                    ActualizarTnt(mundo);
                }
            }
        }
    }

    void CrecerPlantas(MundoServidor mundo)
    {
        var m = mundo.Mundo;
        var rnd = Random.Shared;
        for (int x = 0; x < m.Ancho; x++)
        {
            for (int z = 0; z < m.Profundo; z++)
            {
                for (int y = 1; y < m.Alto - 2; y++)
                {
                    var b = m.Obtener(x, y, z);
                    if (b >= Bloques.Trigo0 && b < Bloques.Trigo3 && rnd.NextDouble() < 0.5)
                    {
                        m.Poner(x, y, z, (ushort)(b + 1));
                        Broadcast(mundo.Id, new BloqueCambio { X = x, Y = y, Z = z, Bloque = (ushort)(b + 1) });
                    }
                    else if (b == Bloques.Planton && m.Obtener(x, y - 1, z) != Bloques.Aire &&
                             m.Obtener(x, y + 1, z) == Bloques.Aire && m.Obtener(x, y + 2, z) == Bloques.Aire &&
                             rnd.NextDouble() < 0.25)
                    {
                        Mundo.PonerArbol(m, x, y, z, rnd);
                        Broadcast(mundo.Id, new BloqueCambio { X = x, Y = y, Z = z, Bloque = Bloques.Madera });
                    }
                }
            }
        }
    }

    void ActualizarTnt(MundoServidor mundo)
    {
        for (int i = mundo.Tnts.Count - 1; i >= 0; i--)
        {
            var (x, y, z, t) = mundo.Tnts[i];
            if (t > 0) { mundo.Tnts[i] = (x, y, z, t - 0.5f); continue; }
            mundo.Tnts.RemoveAt(i);
            Log($"DEBUG tnt: explotando ({x},{y},{z})");
            Explotar(mundo, x, y, z);
        }
    }

    void Explotar(MundoServidor mundo, int cx, int cy, int cz)
    {
        var m = mundo.Mundo;
        const float radio = 3.5f;
        var rnd = Random.Shared;
        int r = (int)MathF.Ceiling(radio);
        for (int x = cx - r; x <= cx + r; x++)
            for (int y = cy - r; y <= cy + r; y++)
                for (int z = cz - r; z <= cz + r; z++)
                {
                    if (!m.Dentro(x, y, z)) continue;
                    float d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy) + (z - cz) * (z - cz));
                    if (d > radio) continue;
                    var b = m.Obtener(x, y, z);
                    if (b == Bloques.Aire || b == Bloques.Lecho || b == Bloques.Agua) continue;
                    if (b == Bloques.Tnt)
                    {
                        // La TNT se consume: la central ya no esta en la lista (la quito
                        // ActualizarTnt antes de llamar Explotar) y las vecinas encendidas
                        // explotan con ella en vez de quedar como bloque.
                        mundo.Tnts.RemoveAll(t2 => t2.x == x && t2.y == y && t2.z == z);
                        m.Poner(x, y, z, Bloques.Aire);
                        Broadcast(mundo.Id, new BloqueCambio { X = x, Y = y, Z = z, Bloque = Bloques.Aire });
                        continue;
                    }
                    m.Poner(x, y, z, Bloques.Aire);
                    if (rnd.NextDouble() < 0.3)
                    {
                        foreach (var (mat, cant) in Objetos.DropAlRomper(b, true, rnd))
                            mundo.Drops.Add(new Drop { Id = ++mundo.SiguienteDropId, Material = mat, Px = x + 0.5f, Py = y + 0.5f, Pz = z + 0.5f });
                    }
                    Broadcast(mundo.Id, new BloqueCambio { X = x, Y = y, Z = z, Bloque = Bloques.Aire });
                }
        foreach (var j in mundo.Jugadores.Values)
        {
            float d = Vector3.Distance(j.Pos, new Vector3(cx + 0.5f, cy + 0.5f, cz + 0.5f));
            if (d < radio + 1.5f)
            {
                j.Salud = Math.Max(0, j.Salud - 10);
                Enviar(j, new JugadorSalud { Salud = j.Salud, MaxSalud = 20 });
                if (j.Salud <= 0) Reaparecer(j, mundo);
            }
        }
        foreach (var mob in mundo.Mobs.ToList())
        {
            float d = Vector3.Distance(new Vector3(mob.Px, mob.Py, mob.Pz), new Vector3(cx + 0.5f, cy + 0.5f, cz + 0.5f));
            if (d < radio + 1.5f) mob.Salud -= 15;
        }
        mundo.Mobs.RemoveAll(mob => mob.Salud <= 0);
    }

    void Reaparecer(ConexionJugador j, MundoServidor mundo)
    {
        var p = mundo.Mundo.ObtenerPuntoAparicion();
        j.Pos = p;
        j.Salud = 20;
        Enviar(j, new JugadorSalud { Salud = 20, MaxSalud = 20 });
        Enviar(j, new Respawn { Px = p.X, Py = p.Y, Pz = p.Z });
    }

    // ------------------------------------------------------------------ posiciones (10 Hz)

    async Task CicloPosicionesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(100, ct); }
            catch (OperationCanceledException) { break; }
            Dictionary<string, List<Posicion>> instantanea;
            lock (_cerrojo)
            {
                instantanea = new();
                foreach (var mundo in _mundos.Values)
                {
                    if (mundo.Conteo < 2) continue;
                    var lista = new List<Posicion>(mundo.Conteo);
                    foreach (var j in mundo.Jugadores.Values)
                        lista.Add(new Posicion { Id = j.Id, Px = j.Pos.X, Py = j.Pos.Y, Pz = j.Pos.Z, Ry = j.Ry, Pitch = j.Pitch });
                    instantanea[mundo.Id] = lista;
                }
            }
            foreach (var (mundoId, lista) in instantanea)
            {
                var msg = new Posiciones { Jugadores = lista };
                lock (_cerrojo)
                {
                    if (_mundos.TryGetValue(mundoId, out var mundo))
                        foreach (var j in mundo.Jugadores.Values)
                            Enviar(j, msg);
                }
            }
        }
    }

    // ------------------------------------------------------------------ mobs

    async Task CicloMobsAsync(CancellationToken ct)
    {
        const float dt = 0.25f;
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(250, ct); }
            catch (OperationCanceledException) { break; }
            lock (_cerrojo)
            {
                foreach (var mundo in _mundos.Values)
                {
                    if (mundo.Conteo == 0) continue; // no simular mundos vacios
                    ActualizarMobs(mundo, dt);
                    var msg = new Mobs
                    {
                        Lista = mundo.Mobs.Select(m => new MobEstado
                        {
                            Id = m.Id,
                            Tipo = (byte)m.Tipo,
                            Px = m.Px, Py = m.Py, Pz = m.Pz, Ry = m.Ry,
                            Salud = (int)MathF.Round(m.Salud),
                            MaxSalud = MobsInfo.SaludMaxima(m.Tipo),
                        }).ToList(),
                    };
                    foreach (var j in mundo.Jugadores.Values) Enviar(j, msg);

                    // Recoger drops: se lo lleva el jugador más cercano
                    var avisados = new List<ConexionJugador>();
                    for (int i = mundo.Drops.Count - 1; i >= 0; i--)
                    {
                        var drop = mundo.Drops[i];
                        ConexionJugador? masCercano = null;
                        float mejorD = 2.5f;
                        foreach (var j in mundo.Jugadores.Values)
                        {
                            float d = Vector3.Distance(j.Pos, new Vector3(drop.Px, drop.Py, drop.Pz));
                            if (d < mejorD) { mejorD = d; masCercano = j; }
                        }
                        if (masCercano != null)
                        {
                            AgregarInventario(masCercano, drop.Material, 1);
                            if (!avisados.Contains(masCercano)) avisados.Add(masCercano);
                            mundo.Drops.RemoveAt(i);
                        }
                    }
                    // Un solo Inventario por jugador tras recoger todo (evita que el
                    // cliente lea un inventario intermedio sin todos los drops)
                    foreach (var av in avisados) Enviar(av, InventarioActual(av));

                    // Difundir drops restantes (incluye lista vacia para sincronizar el cliente)
                    var dmsg = new Drops
                    {
                        Lista = mundo.Drops.Select(d => new DropEstado { Id = d.Id, Material = d.Material, Px = d.Px, Py = d.Py, Pz = d.Pz }).ToList(),
                    };
                    foreach (var j in mundo.Jugadores.Values) Enviar(j, dmsg);
                }
            }
        }
    }

    void GenerarMobs(MundoServidor mundo)
    {
        var m = mundo.Mundo;
        var rnd = new Random(m.Semilla ^ 0x51A7);
        int n = 9;
        int cx = m.Ancho / 2, cz = m.Profundo / 2;
        for (int i = 0; i < n; i++)
        {
            int x = Math.Clamp(cx + rnd.Next(-14, 15), 1, m.Ancho - 2);
            int z = Math.Clamp(cz + rnd.Next(-14, 15), 1, m.Profundo - 2);
            int y = m.Superficie(x, z);
            var tipo = (TipoMob)rnd.Next(6);
            mundo.Mobs.Add(new Mob
            {
                Id = ++mundo.SiguienteMobId,
                Tipo = tipo,
                Px = x + 0.5f, Py = y, Pz = z + 0.5f,
                Ry = 0,
                TiempoCambio = (float)rnd.NextDouble() * 3f,
                Salud = MobsInfo.SaludMaxima(tipo),
            });
        }
    }

    void ActualizarMobs(MundoServidor mundo, float dt)
    {
        var m = mundo.Mundo;
        foreach (var mob in mundo.Mobs)
        {
            var info = MobsInfo.Datos(mob.Tipo);
            mob.TiempoCambio -= dt;

            Vector3? objetivo = null;
            if (info.Hostil)
                objetivo = JugadorMasCercano(mundo, mob.Px, mob.Pz, info.AreaAgresion);

            if (objetivo is { } o)
            {
                var dx = o.X - mob.Px;
                var dz = o.Z - mob.Pz;
                float dist = MathF.Sqrt(dx * dx + dz * dz);
                if (dist > 0.7f) { mob.VelX = dx / dist * info.Velocidad; mob.VelZ = dz / dist * info.Velocidad; }
                else { mob.VelX = 0; mob.VelZ = 0; }
                mob.TiempoCambio = 0.3f;
            }
            else if (mob.TiempoCambio <= 0)
            {
                var rnd = Random.Shared;
                if (rnd.NextDouble() < 0.3f) { mob.VelX = 0; mob.VelZ = 0; }
                else
                {
                    float ang = (float)(rnd.NextDouble() * Math.PI * 2);
                    mob.VelX = MathF.Cos(ang) * info.Velocidad;
                    mob.VelZ = MathF.Sin(ang) * info.Velocidad;
                }
                mob.TiempoCambio = 2f + (float)rnd.NextDouble() * 4f;
            }

            float nx = mob.Px + mob.VelX * dt;
            float nz = mob.Pz + mob.VelZ * dt;
            if (PuedeMoverse(m, nx, nz, info, out int ySup))
            {
                mob.Px = nx; mob.Pz = nz; mob.Py = ySup;
                if (mob.VelX != 0 || mob.VelZ != 0) mob.Ry = MathF.Atan2(mob.VelX, -mob.VelZ);
            }
            else
            {
                mob.VelX = -mob.VelX; mob.VelZ = -mob.VelZ; mob.TiempoCambio = 0.5f;
            }
        }

        // Los mobs hostiles golpean a los jugadores que estan a su alcance
        foreach (var mob in mundo.Mobs)
        {
            var info = MobsInfo.Datos(mob.Tipo);
            if (!info.Hostil || info.Danio <= 0) continue;
            foreach (var j in mundo.Jugadores.Values)
            {
                if (j.TiempoGolpe > 0) continue;
                if (Vector3.Distance(j.Pos, new Vector3(mob.Px, mob.Py, mob.Pz)) < 1.6f)
                {
                    j.Salud = Math.Max(0, j.Salud - (int)MathF.Round(info.Danio));
                    j.TiempoGolpe = 1f;
                    Enviar(j, new JugadorSalud { Salud = j.Salud, MaxSalud = 20 });
                    if (j.Salud <= 0) Reaparecer(j, mundo);
                }
            }
        }
        foreach (var j in mundo.Jugadores.Values)
            if (j.TiempoGolpe > 0) j.TiempoGolpe -= dt;
    }

    Vector3? JugadorMasCercano(MundoServidor mundo, float x, float z, float area)
    {
        Vector3? mejor = null;
        float mejorD = area * area;
        foreach (var j in mundo.Jugadores.Values)
        {
            var dx = j.Pos.X - x;
            var dz = j.Pos.Z - z;
            float d = dx * dx + dz * dz;
            if (d < mejorD) { mejorD = d; mejor = j.Pos; }
        }
        return mejor;
    }

    bool PuedeMoverse(Mundo m, float x, float z, InfoMob info, out int ySup)
    {
        int bx = (int)MathF.Floor(x), bz = (int)MathF.Floor(z);
        ySup = m.Superficie(bx, bz);
        int alto = Math.Max(1, (int)MathF.Ceiling(info.Alto));
        for (int yy = ySup; yy < ySup + alto && yy < m.Alto; yy++)
            if (Bloques.EsSolido(m.Obtener(bx, yy, bz))) return false;
        return true;
    }

    // ------------------------------------------------------------------ inventario y drops

    void GolpearMob(ConexionJugador c, GolpearMob gm)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo || c.MundoId == null || !_mundos.TryGetValue(c.MundoId, out var mundo)) return;
            var mob = mundo.Mobs.FirstOrDefault(m => m.Id == gm.Id);
            if (mob == null) return;
            if (Vector3.Distance(c.Pos, new Vector3(mob.Px, mob.Py, mob.Pz)) > 5f) return;
            mob.Salud -= 5 + MejorDanioEspada(c);
            if (mob.Salud <= 0)
            {
                mundo.Mobs.Remove(mob);
                var rnd = Random.Shared;
                foreach (var (material, min, max) in Objetos.Loot(mob.Tipo))
                {
                    int n = rnd.Next(min, max + 1);
                    if (n <= 0) continue;
                    mundo.Drops.Add(new Drop { Id = ++mundo.SiguienteDropId, Material = material, Px = mob.Px, Py = mob.Py, Pz = mob.Pz });
                }
            }
        }
    }

    void Craftear(ConexionJugador c, Craftear cr)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo) return;
            if (cr.Receta < 0 || cr.Receta >= Objetos.RecetasCrafteo.Length) return;
            var r = Objetos.RecetasCrafteo[cr.Receta];
            var ings = r.Ingredientes();
            foreach (var ing in ings)
                if (Contar(c, ing.Material) < ing.Cantidad) return;
            foreach (var ing in ings)
                Quitar(c, ing.Material, ing.Cantidad);
            AgregarInventario(c, r.Salida, r.SalidaCantidad);
            Enviar(c, InventarioActual(c));
        }
    }

    void Cocinar(ConexionJugador c, Cocinar co)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo || c.MundoId == null || !_mundos.TryGetValue(c.MundoId, out var mundo)) return;
            if (co.Receta < 0 || co.Receta >= Objetos.RecetasCocina.Length) return;
            Log($"DEBUG cocinar: receta={co.Receta} pos=({c.Pos.X:F1},{c.Pos.Y:F1},{c.Pos.Z:F1}) hornoCerca={HayHornoCerca(mundo.Mundo, c.Pos)}");
            if (!HayHornoCerca(mundo.Mundo, c.Pos))
            {
                Enviar(c, new ErrorServidor { Codigo = "SIN_HORNO", Mensaje = "Coloca un horno cerca para cocinar." });
                return;
            }
            var r = Objetos.RecetasCocina[co.Receta];
            // Fundir minerales requiere carbon como combustible
            if (Objetos.EsFundicion(r) && Contar(c, (ushort)ItemId.CarbonItem) < 1)
            {
                Enviar(c, new ErrorServidor { Codigo = "SIN_CARBON", Mensaje = "El horno necesita carbon como combustible." });
                return;
            }
            var ings = r.Ingredientes();
            Log($"DEBUG cocinar: ings={string.Join(",", ings.Select(i => i.Material + "x" + i.Cantidad))} carbon={Contar(c, (ushort)ItemId.CarbonItem)} esFundicion={Objetos.EsFundicion(r)}");
            foreach (var ing in ings)
                if (Contar(c, ing.Material) < ing.Cantidad) return;
            foreach (var ing in ings)
                Quitar(c, ing.Material, ing.Cantidad);
            if (Objetos.EsFundicion(r)) Quitar(c, (ushort)ItemId.CarbonItem, 1);
            AgregarInventario(c, r.Salida, r.SalidaCantidad);
            Enviar(c, InventarioActual(c));
        }
    }

    bool HayHornoCerca(Mundo m, Vector3 pos)
    {
        int r = 5;
        for (int x = (int)pos.X - r; x <= (int)pos.X + r; x++)
            for (int y = (int)pos.Y - r; y <= (int)pos.Y + r; y++)
                for (int z = (int)pos.Z - r; z <= (int)pos.Z + r; z++)
                    if (m.Obtener(x, y, z) == Bloques.Horno) return true;
        return false;
    }

    static bool TienePico(ConexionJugador c) => c.Inventario.Any(s => Objetos.EsPico(s.Material));

    static int MejorDanioEspada(ConexionJugador c)
    {
        int mejor = 0;
        foreach (var s in c.Inventario) mejor = Math.Max(mejor, Objetos.DanioEspada(s.Material));
        return mejor;
    }

    static void AgregarInventario(ConexionJugador c, ushort material, int cantidad)
    {
        if (cantidad <= 0) return;
        for (int i = 0; i < c.Inventario.Count; i++)
        {
            if (c.Inventario[i].Material == material)
            {
                c.Inventario[i] = c.Inventario[i] with { Cantidad = c.Inventario[i].Cantidad + cantidad };
                return;
            }
        }
        c.Inventario.Add(new SlotInventario(material, cantidad));
    }

    static int Contar(ConexionJugador c, ushort material)
    {
        int n = 0;
        foreach (var s in c.Inventario) if (s.Material == material) n += s.Cantidad;
        return n;
    }

    static bool Quitar(ConexionJugador c, ushort material, int cantidad)
    {
        if (Contar(c, material) < cantidad) return false;
        int restante = cantidad;
        for (int i = c.Inventario.Count - 1; i >= 0 && restante > 0; i--)
        {
            var s = c.Inventario[i];
            if (s.Material != material) continue;
            int quitar = Math.Min(restante, s.Cantidad);
            restante -= quitar;
            int nuevo = s.Cantidad - quitar;
            if (nuevo <= 0) c.Inventario.RemoveAt(i);
            else c.Inventario[i] = s with { Cantidad = nuevo };
        }
        return restante == 0;
    }

    static Inventario InventarioActual(ConexionJugador c) => new()
    {
        Slots = c.Inventario.Select(s => new SlotEstado { Material = s.Material, Cantidad = s.Cantidad }).ToList(),
    };

    // ------------------------------------------------------------------ envÃ­o

    void Broadcast(string mundoId, Mensaje m)
    {
        lock (_cerrojo)
        {
            if (_mundos.TryGetValue(mundoId, out var mundo))
                foreach (var j in mundo.Jugadores.Values)
                    Enviar(j, m);
        }
    }

    static void Enviar(ConexionJugador c, Mensaje m)
    {
        try
        {
            var datos = Protocolo.Codificar(m);
            lock (c) c.Flujo.Write(datos);
        }
        catch { /* el cierre se gestiona en Desconectar */ }
    }
}

