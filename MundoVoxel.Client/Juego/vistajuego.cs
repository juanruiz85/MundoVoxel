using System.Numerics;
using Microsoft.Maui.Graphics;
using MundoVoxel.Client.Servicios;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Juego;

/// <summary>
/// Vista de la partida: cámara, física, raycast de bloques, jugadores remotos,
/// entrada táctil (joystick + arrastrar) y HUD (mira, barra de bloques, joystick).
/// El render pesado se hace en segundo plano (Rasterizador) y aquí solo se blitea.
/// </summary>
public sealed class VistaJuego : IDrawable
{
    public Mundo Mundo = null!;
    public readonly RenderizadorVoxel Renderizador = new();
    public readonly Rasterizador Raster = new();
    public readonly Camara Cam = new();
    public readonly ControladorJugador Jugador = new();

    // Hotbar: primeros 9 slots del inventario (se rellena desde la pagina)
    public (ushort Material, int Cantidad)[] Hotbar = new (ushort, int)[9];

    public int Slot { get; set; }
    public ushort BloqueSeleccionado => Hotbar[Math.Clamp(Slot, 0, Hotbar.Length - 1)].Material;
    public ushort ItemEnMano => BloqueSeleccionado;

    // Vida del jugador (la envia el servidor)
    public int Salud = 20, MaxSalud = 20;

    // Oxigeno (lo envia el servidor; se agota bajo el agua)
    public float Oxigeno = 15f, MaxOxigeno = 15f;

    // Entrada
    public bool BotonSaltar, BotonBajar;
    public bool Volando { get; set; }
    public bool Espectador { get; set; } // modo espectador: vuela y atraviesa bloques

    // Jugadores remotos
    public sealed record JugadorRemoto(int Id, string Nombre, Vector3 Pos, float Ry, float Pitch, Color Color);
    public readonly Dictionary<int, JugadorRemoto> Remotos = new();

    // Mobs remotos (estado autoritativo del servidor)
    public sealed record MobRemoto(TipoMob Tipo, Vector3 Pos, float Ry, int Salud, int MaxSalud, bool Quemando);
    public readonly Dictionary<int, MobRemoto> Mobs = new();

    // Drops en el suelo (ítems que se recogen al pasar)
    public sealed record DropRemoto(ushort Material, Vector3 Pos);
    public readonly Dictionary<int, DropRemoto> Drops = new();

    public static Color ColorMaterial(ushort m)
    {
        var (r, g, b) = Objetos.Color(m);
        return Color.FromRgb(r, g, b);
    }

    public static Color ColorMob(TipoMob t) => t switch
    {
        TipoMob.Cerdo => Color.FromArgb("#e79a9a"),
        TipoMob.Vaca => Color.FromArgb("#8b5a2b"),
        TipoMob.Oveja => Color.FromArgb("#e6e6e6"),
        TipoMob.Zombi => Color.FromArgb("#4e9a4e"),
        TipoMob.Creeper => Color.FromArgb("#5fbf5f"),
        _ => Color.FromArgb("#3a3a3a"),
    };

    public static string NombreMob(TipoMob t) => t switch
    {
        TipoMob.Cerdo => "Cerdo",
        TipoMob.Vaca => "Vaca",
        TipoMob.Oveja => "Oveja",
        TipoMob.Zombi => "Zombi",
        TipoMob.Creeper => "Creeper",
        _ => "Esqueleto",
    };

    /// <summary>
    /// Anade la figura voxel de la herramienta en mano (esquina inferior derecha),
    /// con la cabeza del material y el mango de madera.
    /// </summary>
    public void AgregarHerramientaMano(List<CajaJugador> cajas, ushort item, Camara cam)
    {
        var tipo = Objetos.TipoDe(item);
        if (tipo == TipoHerramienta.Ninguna) return;
        var (cr, cg, cb) = Objetos.Color(item);
        var colorMat = Color.FromRgb(cr / 255f, cg / 255f, cb / 255f);
        var colorPalo = Color.FromArgb("#9a6a3a");

        (int x, int y, int z, Color c)[] celdas = tipo switch
        {
            TipoHerramienta.Espada => new[]
            {
                (0, 1, 0, colorMat), (0, 2, 0, colorMat), (0, 3, 0, colorMat),
                (-1, 0, 0, colorPalo), (0, 0, 0, colorPalo), (1, 0, 0, colorPalo),
                (0, -1, 0, colorPalo), (0, -2, 0, colorPalo),
            },
            TipoHerramienta.Pico => new[]
            {
                (-1, 1, 0, colorMat), (0, 1, 0, colorMat), (1, 1, 0, colorMat),
                (0, 0, 0, colorPalo), (0, -1, 0, colorPalo), (0, -2, 0, colorPalo),
            },
            TipoHerramienta.Hacha => new[]
            {
                (-1, 1, 0, colorMat), (0, 1, 0, colorMat), (0, 0, 0, colorMat),
                (0, -1, 0, colorPalo), (0, -2, 0, colorPalo),
            },
            TipoHerramienta.Pala => new[]
            {
                (-1, 1, 0, colorMat), (0, 1, 0, colorMat),
                (0, 0, 0, colorPalo), (0, -1, 0, colorPalo), (0, -2, 0, colorPalo),
            },
            _ => new[]
            {
                (-1, 1, 0, colorMat), (0, 1, 0, colorMat), (1, 1, 0, colorMat), (0, 0, 0, colorMat),
                (0, -1, 0, colorPalo), (0, -2, 0, colorPalo),
            },
        };

        const float s = 0.13f;
        var fwd = cam.Adelante;
        var der = cam.Derecha;
        var arriba = Vector3.Cross(der, fwd);
        var origen = cam.Pos + fwd * 0.85f + der * 0.60f - arriba * 0.42f;
        foreach (var cel in celdas)
        {
            var p = origen + der * (cel.x * s) + arriba * (cel.y * s) + fwd * (cel.z * s);
            cajas.Add(new CajaJugador(p - new Vector3(s / 2f), p + new Vector3(s / 2f), cel.c));
        }
    }

    /// <summary>Caja (AABB + color) de un jugador/mob para rasterizar en segundo plano.</summary>
    public readonly record struct CajaJugador(Vector3 Min, Vector3 Max, Color Color);

    /// <summary>
    /// Convierte el diseno voxel de un mob en cajas de mundo (una por celda),
    /// escaladas al tamano del mob y rotadas segun su orientacion (Ry).
    /// </summary>
    public static void AgregarMobFigura(List<CajaJugador> cajas, TipoMob tipo, Vector3 pos, float ry)
    {
        var d = MobsInfo.Diseno(tipo);
        var info = MobsInfo.Datos(tipo);
        float esc = info.Ancho / d.AnchoCeldas;
        float escY = info.Alto / d.AltoCeldas;
        float ca = MathF.Cos(-ry), sa = MathF.Sin(-ry);
        float cx = d.AnchoCeldas * esc / 2f;
        float cz = d.ProfCeldas * esc / 2f;
        foreach (var cel in d.Celdas)
        {
            float ox = cel.X * esc - cx;
            float oz = cel.Z * esc - cz;
            float rx = ox * ca - oz * sa + cx;
            float rz = ox * sa + oz * ca + cz;
            var min = new Vector3(pos.X + rx - esc / 2f, pos.Y + cel.Y * escY, pos.Z + rz - esc / 2f);
            var max = new Vector3(pos.X + rx + esc / 2f, pos.Y + (cel.Y + 1) * escY, pos.Z + rz + esc / 2f);
            cajas.Add(new CajaJugador(min, max, Color.FromRgb(cel.R / 255f, cel.G / 255f, cel.B / 255f)));
        }
    }

    // Acciones pendientes de enviar (las consume la página)
    public GolpeRayo GolpeActual;
    bool _romperPendiente, _colocarPendiente;
    // Auto-golpe con clic sostenido: solo funciona con BLOQUES (a los mobs se
    // les golpea una vez por clic). Mientras el bloque bajo la mira sea el mismo
    // y no sea aire, se siguen enviando golpes cada ~0.25 s hasta romperlo.
    bool _romperSostenido;
    (int x, int y, int z)? _bloqueSostenido;
    float _tiempoGolpeSostenido;

    public bool ConsumirRomper() { if (!_romperPendiente) return false; _romperPendiente = false; return GolpeActual.Impacto; }
    public bool ConsumirColocar() { if (!_colocarPendiente) return false; _colocarPendiente = false; return GolpeActual.Impacto; }
    public void PedirRomper() => _romperPendiente = true;
    public void PedirColocar() => _colocarPendiente = true;

    /// <summary>Empieza el auto-golpe sobre el bloque que esta bajo la mira.</summary>
    public void IniciarRomperSostenido()
    {
        if (!GolpeActual.Impacto) return;
        _bloqueSostenido = (GolpeActual.X, GolpeActual.Y, GolpeActual.Z);
        _romperSostenido = true;
        _tiempoGolpeSostenido = 0f;
    }

    public void DetenerRomperSostenido()
    {
        _romperSostenido = false;
        _bloqueSostenido = null;
    }

    /// <summary>
    /// Devuelve true cuando hay que enviar otro golpe por mantener pulsado el
    /// boton izquierdo sobre el MISMO bloque (se detiene al romperse o al
    /// apuntar a otra cosa; los mobs NO se repiten, se golpean una sola vez).
    /// </summary>
    public bool ConsumirRomperSostenido(float dt)
    {
        if (!_romperSostenido || _bloqueSostenido is not var (bx, by, bz)) return false;
        // Si la mira cambio de bloque (o ya no hay impacto), se detiene
        if (!GolpeActual.Impacto || GolpeActual.X != bx || GolpeActual.Y != by || GolpeActual.Z != bz)
        {
            _romperSostenido = false;
            _bloqueSostenido = null;
            return false;
        }
        _tiempoGolpeSostenido -= dt;
        if (_tiempoGolpeSostenido > 0f) return false;
        _tiempoGolpeSostenido = 0.25f;
        return true;
    }

    // Estado táctil
    bool _joystickActivo;
    PointF _joystickOrigen;
    Vector2 _joystick;
    public Vector2 Joystick => _joystick;

    /// <summary>Sensibilidad del ratA3n (multiplica el giro por pA-xel arrastrado).</summary>
    public float Sensibilidad = 1f;

    /// <summary>True si la A-ltima interacciA3n vino de un puntero de ratA3n (Windows).</summary>
    public bool PunteroRaton;

    PointF _ultimoPunto;
    bool _arrastrando;
    float _distanciaTotal;
    DateTime _inicioToque;
    DateTime _ultimoToque;
    PointF _puntoUltimoToque;

    public void IniciarInteraccion(PointF p, bool esMovil)
    {
        _ultimoPunto = p;
        _distanciaTotal = 0;
        _inicioToque = DateTime.UtcNow;
        if (esMovil && p.X < 400)
        {
            _joystickActivo = true;
            _joystickOrigen = p;
            _joystick = Vector2.Zero;
        }
        else
        {
            _arrastrando = true;
        }
    }

    /// <summary>
    /// Rota la camara con el movimiento del raton capturado (modo crosshair FPS):
    /// el puntero queda clavado en el centro y cada desplazamiento se convierte
    /// en giro. El raton se recentra en la pagina (Windows) para poder girar
    /// sin limites.
    /// </summary>
    public void MoverRaton(float dx, float dy)
    {
        Jugador.Yaw += dx * 0.005f * Sensibilidad;
        float lim = Ajustes.Actual.LimitePitch;
        Jugador.Pitch = Math.Clamp(Jugador.Pitch + dy * 0.005f * Sensibilidad, -lim, lim);
    }

    public void ArrastrarInteraccion(PointF p)
    {
        if (_joystickActivo)
        {
            var dx = p.X - _joystickOrigen.X;
            var dy = p.Y - _joystickOrigen.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            float radio = 55f;
            if (len > radio)
            {
                dx = dx / len * radio;
                dy = dy / len * radio;
                len = radio;
            }
            _joystick = new Vector2(dx / radio, dy / radio);
            return;
        }
        if (!_arrastrando) return;
        var ox = p.X - _ultimoPunto.X;
        var oy = p.Y - _ultimoPunto.Y;
        _distanciaTotal += MathF.Abs(ox) + MathF.Abs(oy);
        Jugador.Yaw += ox * 0.005f * Sensibilidad;
        float lim = Ajustes.Actual.LimitePitch;
        Jugador.Pitch = Math.Clamp(Jugador.Pitch + oy * 0.005f * Sensibilidad, -lim, lim);
        _ultimoPunto = p;
    }

    public void TerminarInteraccion(PointF p)
    {
        bool fueJoystick = _joystickActivo;
        _joystickActivo = false;
        _arrastrando = false;
        if (fueJoystick)
        {
            _joystick = Vector2.Zero;
            return;
        }
        // Con ratA3n el clic ya hizo su acciA3n (izquierdo rompe, derecho coloca);
        // no convertir el clic en tap para no duplicar la acciA3n.
        if (PunteroRaton)
        {
            PunteroRaton = false;
            return;
        }
        var duracion = (DateTime.UtcNow - _inicioToque).TotalMilliseconds;
        bool tap = duracion < 400 && _distanciaTotal < 18f;
        if (!tap) return;

        bool doble = (DateTime.UtcNow - _ultimoToque).TotalMilliseconds < 350 &&
                     MathF.Abs(p.X - _puntoUltimoToque.X) < 56 &&
                     MathF.Abs(p.Y - _puntoUltimoToque.Y) < 56;
        _ultimoToque = DateTime.UtcNow;
        _puntoUltimoToque = p;

        if (doble) PedirRomper();
        else PedirColocar();
    }

    /// <summary>Actualiza física y golpe; se llama desde la página (hilo UI).</summary>
    public void Tick(float dt, Func<int, bool> estaPulsada, bool esMovil)
    {
        bool w = estaPulsada(Teclas.W), s = estaPulsada(Teclas.S), a = estaPulsada(Teclas.A), d = estaPulsada(Teclas.D);
        var dir = Vector2.Zero;
        if (w) dir.Y += 1;
        if (s) dir.Y -= 1;
        if (a) dir.X -= 1;
        if (d) dir.X += 1;
        if (dir.LengthSquared() > 0) dir = Vector2.Normalize(dir);
        if (esMovil && _joystickActivo) dir = _joystick;

        bool saltar = estaPulsada(Teclas.Espacio) || BotonSaltar;
        bool bajar = estaPulsada(Teclas.Mayus) || BotonBajar;

        Jugador.Espectador = Espectador;
        Jugador.Actualizar(Mundo, dt, dir, saltar, bajar, Volando || Espectador);
        if (Espectador) Jugador.EnSuelo = false;
        Jugador.CaerAlVacio(Mundo);

        Cam.Pos = Jugador.Ojo;
        Cam.Yaw = Jugador.Yaw;
        Cam.Pitch = Jugador.Pitch;
        GolpeActual = Rayos.Lanzar(Mundo, Jugador.Ojo, Cam.Adelante, 6f);
    }

    /// <summary>
    /// Renderiza un frame completo en el rasterizador y lo devuelve como BMP.
    /// Recibe una instantánea de la cámara y de las cajas para poder correr en segundo plano.
    /// </summary>
    public byte[] RenderFrame(int w, int h, Vector3 camPos, float yaw, float pitch, CajaJugador[] cajas)
    {
        var cam = new Camara { Pos = camPos, Yaw = yaw, Pitch = pitch };
        Raster.Inicializar(w, h);
        Renderizador.Rasterizar(Raster, Mundo, cam, w, h);
        foreach (var caja in cajas)
            Renderizador.RasterizarCaja(Raster, cam, caja.Min, caja.Max, caja.Color);
        return Raster.ABmp();
    }

    /// <summary>Devuelve el id del mob que está bajo la mira, o -1 si no apunta a ninguno.</summary>
    public int BuscarMobApuntado()
    {
        var o = Cam.Pos;
        var d = Cam.Adelante;
        int mejor = -1;
        float mejorT = 6f;
        foreach (var (id, m) in Mobs)
        {
            var info = MobsInfo.Datos(m.Tipo);
            float a = info.Ancho * 0.5f;
            var min = m.Pos - new Vector3(a, 0, a);
            var max = m.Pos + new Vector3(a, info.Alto, a);
            if (IntersectaAabb(o, d, min, max, out float t) && t < mejorT)
            {
                mejorT = t;
                mejor = id;
            }
        }
        return mejor;
    }

    static bool IntersectaAabb(Vector3 o, Vector3 d, Vector3 min, Vector3 max, out float t)
    {
        t = 0f;
        float tmin = 0f, tmax = 6f;
        if (MathF.Abs(d.X) < 1e-6f) { if (o.X < min.X || o.X > max.X) return false; }
        else { float t1 = (min.X - o.X) / d.X, t2 = (max.X - o.X) / d.X; if (t1 > t2) (t1, t2) = (t2, t1); tmin = MathF.Max(tmin, t1); tmax = MathF.Min(tmax, t2); if (tmin > tmax) return false; }
        if (MathF.Abs(d.Y) < 1e-6f) { if (o.Y < min.Y || o.Y > max.Y) return false; }
        else { float t1 = (min.Y - o.Y) / d.Y, t2 = (max.Y - o.Y) / d.Y; if (t1 > t2) (t1, t2) = (t2, t1); tmin = MathF.Max(tmin, t1); tmax = MathF.Min(tmax, t2); if (tmin > tmax) return false; }
        if (MathF.Abs(d.Z) < 1e-6f) { if (o.Z < min.Z || o.Z > max.Z) return false; }
        else { float t1 = (min.Z - o.Z) / d.Z, t2 = (max.Z - o.Z) / d.Z; if (t1 > t2) (t1, t2) = (t2, t1); tmin = MathF.Max(tmin, t1); tmax = MathF.Min(tmax, t2); if (tmin > tmax) return false; }
        t = tmin;
        return tmin >= 0f;
    }

    public void Draw(ICanvas c, RectF dirty)
    {
        int w = (int)dirty.Width, h = (int)dirty.Height;
        if (w <= 0 || h <= 0) return;

        foreach (var j in Remotos.Values)
        {
            var pos = j.Pos;
            DibujarNombre(c, w, h, pos + new Vector3(0, 2.1f, 0), j.Nombre);
        }

        foreach (var m in Mobs.Values)
        {
            var alto = MobsInfo.Datos(m.Tipo).Alto;
            DibujarNombre(c, w, h, m.Pos + new Vector3(0, alto + 0.3f, 0), NombreMob(m.Tipo));
            DibujarBarraVida(c, w, h, m.Pos + new Vector3(0, alto + 0.22f, 0), m.Salud, m.MaxSalud);
            if (m.Quemando) DibujarFuego(c, w, h, m.Pos, alto);
        }

        DibujarAntorchas(c, w, h);

        DibujarHud(c, w, h);
    }

    /// <summary>Particulas de fuego animadas sobre las antorchas cercanas: llamas
    /// naranjas/amarillas que suben y parpadean (solo visual, no queman). Se buscan
    /// antorchas en un radio alrededor de la camara y se reutiliza DibujarFuego.</summary>
    void DibujarAntorchas(ICanvas c, int w, int h)
    {
        const int radio = 12;
        int x0 = (int)Cam.Pos.X - radio, x1 = (int)Cam.Pos.X + radio;
        int y0 = Math.Max(0, (int)Cam.Pos.Y - 6), y1 = Math.Min(Mundo.Alto - 1, (int)Cam.Pos.Y + 12);
        int z0 = (int)Cam.Pos.Z - radio, z1 = (int)Cam.Pos.Z + radio;
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                    if (Mundo.Dentro(x, y, z) && Mundo.Obtener(x, y, z) == Bloques.Antorcha)
                        DibujarFuego(c, w, h, new Vector3(x + 0.5f, y + 0.58f, z + 0.5f), 0.30f);
    }

    void DibujarBarraVida(ICanvas c, int w, int h, Vector3 posMundo, int salud, int maxSalud)
    {
        var d = posMundo - Cam.Pos;
        float xc = Vector3.Dot(d, Cam.Derecha);
        float yc = Vector3.Dot(d, Vector3.Cross(Cam.Derecha, Cam.Adelante));
        float zc = Vector3.Dot(d, Cam.Adelante);
        if (zc < 0.12f) return;
        float f = (h / 2f) / MathF.Tan(75f * MathF.PI / 180f / 2f);
        float sx = w / 2f + xc * f / zc;
        float sy = h / 2f - yc * f / zc;
        const float ancho = 36, altoBarra = 4;
        float x0 = sx - ancho / 2f, y0 = sy;
        c.FillColor = new Color(0, 0, 0, 0.6f);
        c.FillRoundedRectangle(x0 - 1, y0 - 1, ancho + 2, altoBarra + 2, 2);
        float pct = maxSalud > 0 ? Math.Clamp(salud / (float)maxSalud, 0f, 1f) : 0f;
        c.FillColor = pct > 0.3f ? new Color(0.35f, 0.85f, 0.35f) : new Color(0.9f, 0.3f, 0.3f);
        c.FillRoundedRectangle(x0, y0, ancho * pct, altoBarra, 2);
    }

    void DibujarNombre(ICanvas c, int w, int h, Vector3 posMundo, string nombre)
    {
        var d = posMundo - Cam.Pos;
        float xc = Vector3.Dot(d, Cam.Derecha);
        float yc = Vector3.Dot(d, Vector3.Cross(Cam.Derecha, Cam.Adelante));
        float zc = Vector3.Dot(d, Cam.Adelante);
        if (zc < 0.12f) return;
        float f = (h / 2f) / MathF.Tan(75f * MathF.PI / 180f / 2f);
        float sx = w / 2f + xc * f / zc;
        float sy = h / 2f - yc * f / zc;
        c.FontSize = 11;
        c.FontColor = Colors.White;
        c.DrawString(nombre, sx - 70, sy - 24, 140, 18, HorizontalAlignment.Center, VerticalAlignment.Top);
    }

    /// <summary>
    /// Particulas de fuego sobre un mob que se esta quemando con el sol de dia
    /// (zombis y esqueletos). Pequenas llamas naranjas/amarillas que suben y
    /// parpadean, proyectadas como el nombre del mob.
    /// </summary>
    void DibujarFuego(ICanvas c, int w, int h, Vector3 posMundo, float alto)
    {
        long t = Environment.TickCount64;
        var basePos = posMundo + new Vector3(0, alto + 0.15f, 0);
        for (int i = 0; i < 7; i++)
        {
            // Fase distinta por particula; suben y se apagan ciclicamente
            float fase = (t / 90f + i * 1.7f) % 9f;
            float subida = fase * 0.06f;
            float deriva = MathF.Sin((t / 130f) + i * 2.1f) * 0.22f;
            float vida = 1f - fase / 9f; // se desvanecen al subir
            if (vida <= 0.15f) continue;
            var p = basePos + new Vector3(deriva, subida, MathF.Cos((t / 110f) + i * 1.3f) * 0.18f);
            var d = p - Cam.Pos;
            float xc = Vector3.Dot(d, Cam.Derecha);
            float yc = Vector3.Dot(d, Vector3.Cross(Cam.Derecha, Cam.Adelante));
            float zc = Vector3.Dot(d, Cam.Adelante);
            if (zc < 0.12f) continue;
            float f = (h / 2f) / MathF.Tan(75f * MathF.PI / 180f / 2f);
            float sx = w / 2f + xc * f / zc;
            float sy = h / 2f - yc * f / zc;
            float tam = 2.5f + vida * 2.5f;
            // Naranja en la base, amarillo en la punta (parpadeo)
            bool parpadea = (t / 60 + i) % 3 != 0;
            c.FillColor = parpadea
                ? new Color(1f, 0.72f, 0.25f, 0.75f * vida)
                : new Color(0.95f, 0.45f, 0.12f, 0.7f * vida);
            c.FillEllipse(sx - tam / 2f, sy - tam / 2f, tam, tam);
        }
    }

    void DibujarHud(ICanvas c, int w, int h)
    {
        float cx = w / 2f, cy = h / 2f;

        c.FillColor = new Color(1, 1, 1, 0.85f);
        c.FillRectangle(cx - 1, cy - 9, 2, 18);
        c.FillRectangle(cx - 9, cy - 1, 18, 2);

        int n = Hotbar.Length;
        const float slot = 40, gap = 4;
        float total = n * slot + (n - 1) * gap;
        float x0 = (w - total) / 2f, y0 = h - slot - 14;
        for (int i = 0; i < n; i++)
        {
            float x = x0 + i * (slot + gap);
            var r = new RectF(x, y0, slot, slot);
            c.FillColor = new Color(0, 0, 0, 0.45f);
            c.FillRoundedRectangle(r, 6);
            if (i == Slot)
            {
                c.StrokeColor = Colors.White;
                c.StrokeSize = 2;
                c.DrawRoundedRectangle(r, 6);
            }
            var (mat, cant) = Hotbar[i];
            if (mat != 0 && cant > 0)
            {
                var (cr, cg, cb) = Objetos.Color(mat);
                c.FillColor = new Color(cr / 255f, cg / 255f, cb / 255f);
                c.FillRoundedRectangle(x + 5, y0 + 5, slot - 10, slot - 10, 4);
                if (cant > 1)
                {
                    c.FontSize = 12;
                    c.FontColor = Colors.White;
                    c.DrawString(cant.ToString(), x + 2, y0 + slot - 16, slot - 6, 14,
                        HorizontalAlignment.Right, VerticalAlignment.Center);
                }
            }
        }

        // Corazones (vida del jugador)
        float cyH = y0 - 26;
        float cx0 = (w - 20 * 10) / 2f;
        for (int i = 0; i < 10; i++)
        {
            bool lleno = Salud >= (i + 1) * 2;
            bool medio = !lleno && Salud == i * 2 + 1;
            c.FontSize = 15;
            c.FontColor = lleno || medio ? new Color(0.85f, 0.22f, 0.22f) : new Color(0.22f, 0.22f, 0.22f);
            c.DrawString("♥", cx0 + i * 20, cyH - 8, 20, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        // Burbujas de oxigeno (se agotan bajo el agua)
        float cyO = cyH - 22;
        float oxPct = MaxOxigeno > 0 ? Math.Clamp(Oxigeno / MaxOxigeno, 0f, 1f) : 0f;
        for (int i = 0; i < 10; i++)
        {
            bool llena = oxPct >= (i + 1) / 10f;
            c.FontSize = 13;
            c.FontColor = llena ? new Color(0.45f, 0.65f, 0.95f) : new Color(0.22f, 0.25f, 0.3f);
            c.DrawString("●", cx0 + i * 20, cyO - 6, 20, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        // Pantalla roja al ahogarse (oxigeno agotado): pulsa suave
        if (oxPct <= 0.01f)
        {
            float pulso = 0.25f + 0.15f * MathF.Sin((float)DateTime.UtcNow.TimeOfDay.TotalSeconds * 6f);
            c.FillColor = new Color(0.5f, 0, 0, pulso);
            c.FillRectangle(0, 0, w, h);
        }

        if (_joystickActivo)
        {
            c.FillColor = new Color(1, 1, 1, 0.22f);
            c.FillCircle(_joystickOrigen.X, _joystickOrigen.Y, 55);
            c.FillColor = new Color(1, 1, 1, 0.45f);
            c.FillCircle(_joystickOrigen.X + _joystick.X * 55, _joystickOrigen.Y + _joystick.Y * 55, 22);
        }
    }
}
