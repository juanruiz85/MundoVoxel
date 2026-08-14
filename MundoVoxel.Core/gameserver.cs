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
                Mundo = Mundo.Generar((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF)),
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
            Broadcast(mundo.Id, new BloqueCambio { X = rb.X, Y = rb.Y, Z = rb.Z, Bloque = Bloques.Aire });
        }
    }

    void Colocar(ConexionJugador c, ColocarBloque cb)
    {
        lock (_cerrojo)
        {
            if (!c.EnMundo || c.MundoId == null || !_mundos.TryGetValue(c.MundoId, out var mundo)) return;
            var m = mundo.Mundo;
            if (!m.Dentro(cb.X, cb.Y, cb.Z) || cb.Y <= 0) return;
            if (!Bloques.EsColocable(cb.Bloque)) return;
            if (m.Obtener(cb.X, cb.Y, cb.Z) != Bloques.Aire) return;
            if (Vector3.Distance(c.Pos, new Vector3(cb.X + 0.5f, cb.Y + 0.5f, cb.Z + 0.5f)) > 7f) return;
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
                    if (mundo.Mobs.Count == 0) continue;
                    var msg = new Mobs
                    {
                        Lista = mundo.Mobs.Select(m => new MobEstado
                        {
                            Id = m.Id,
                            Tipo = (byte)m.Tipo,
                            Px = m.Px, Py = m.Py, Pz = m.Pz, Ry = m.Ry,
                        }).ToList(),
                    };
                    foreach (var j in mundo.Jugadores.Values) Enviar(j, msg);
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
                Salud = 20,
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

