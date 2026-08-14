using System.Numerics;
using Microsoft.Maui.Graphics;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Juego;

/// <summary>
/// Vista de la partida: cámara, física, raycast de bloques, jugadores remotos,
/// entrada táctil (joystick + arrastrar) y HUD (mira, barra de bloques, joystick).
/// </summary>
public sealed class VistaJuego : IDrawable
{
    public Mundo Mundo = null!;
    public readonly RenderizadorVoxel Renderizador = new();
    public readonly Camara Cam = new();
    public readonly ControladorJugador Jugador = new();

    // Barra de bloques seleccionables (índice 0-8)
    public static readonly ushort[] BarraBloques =
    {
        Bloques.Tierra, Bloques.Piedra, Bloques.Madera, Bloques.Arena, Bloques.Cesped,
        Bloques.Ladrillo, Bloques.Hoja, Bloques.Grava, Bloques.Cristal,
    };

    public int Slot { get; set; }
    public ushort BloqueSeleccionado => BarraBloques[Math.Clamp(Slot, 0, BarraBloques.Length - 1)];

    // Entrada
    public bool BotonSaltar, BotonBajar;
    public bool Volando { get; set; }

    // Jugadores remotos
    public sealed record JugadorRemoto(int Id, string Nombre, Vector3 Pos, float Ry, float Pitch, Color Color);
    public readonly Dictionary<int, JugadorRemoto> Remotos = new();
    static readonly Color[] ColoresJugador =
    {
        Colors.Red, Colors.Yellow, Colors.Lime, Colors.Cyan, Colors.Magenta, Colors.Orange,
        Colors.Purple, Colors.Pink, Colors.Teal, Colors.Gold, Colors.Silver, Colors.DeepSkyBlue,
    };

    // Acciones pendientes de enviar (las consume la página)
    public GolpeRayo GolpeActual;
    bool _romperPendiente, _colocarPendiente;
    public bool ConsumirRomper() { if (!_romperPendiente) return false; _romperPendiente = false; return GolpeActual.Impacto; }
    public bool ConsumirColocar() { if (!_colocarPendiente) return false; _colocarPendiente = false; return GolpeActual.Impacto; }
    public void PedirRomper() => _romperPendiente = true;
    public void PedirColocar() => _colocarPendiente = true;

    // Estado táctil
    bool _joystickActivo;
    PointF _joystickOrigen;
    Vector2 _joystick;
    public Vector2 Joystick => _joystick;

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
        if (esMovil && p.X < 400) // joystick en la mitad izquierda (móvil)
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
        Jugador.Yaw += ox * 0.005f;
        Jugador.Pitch = Math.Clamp(Jugador.Pitch + oy * 0.005f, -1.45f, 1.45f);
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
        // ¿Fue un toque sin arrastrar? → colocar; doble toque → romper
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

    /// <summary>Actualiza física y golpe; se llama una vez por frame desde la página.</summary>
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

        Jugador.Actualizar(Mundo, dt, dir, saltar, bajar, Volando);
        Jugador.CaerAlVacio(Mundo);

        Cam.Pos = Jugador.Ojo;
        Cam.Yaw = Jugador.Yaw;
        Cam.Pitch = Jugador.Pitch;
        GolpeActual = Rayos.Lanzar(Mundo, Jugador.Ojo, Cam.Adelante, 6f);
    }

    public void Draw(ICanvas c, RectF dirty)
    {
        int w = (int)dirty.Width, h = (int)dirty.Height;
        if (w <= 0 || h <= 0) return;
        Renderizador.Dibujar(c, w, h, Cam, Mundo);

        foreach (var j in Remotos.Values)
        {
            var pos = j.Pos;
            Renderizador.DibujarCaja(c, w, h, Cam,
                new Vector3(pos.X - 0.3f, pos.Y, pos.Z - 0.3f),
                new Vector3(pos.X + 0.3f, pos.Y + 1.8f, pos.Z + 0.3f), j.Color);
            DibujarNombre(c, w, h, pos + new Vector3(0, 2.1f, 0), j.Nombre);
        }

        DibujarHud(c, w, h);
    }

    void DibujarNombre(ICanvas c, int w, int h, Vector3 posMundo, string nombre)
    {
        // Proyecta la posición del nombre
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

    void DibujarHud(ICanvas c, int w, int h)
    {
        float cx = w / 2f, cy = h / 2f;

        // Mira
        c.FillColor = new Color(1, 1, 1, 0.85f);
        c.FillRectangle(cx - 1, cy - 9, 2, 18);
        c.FillRectangle(cx - 9, cy - 1, 18, 2);

        // Barra de bloques
        int n = BarraBloques.Length;
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
            c.FillColor = Renderizador.ColorBloque(BarraBloques[i]);
            c.FillRectangle(x + 6, y0 + 6, slot - 12, slot - 12);
        }

        // Joystick visual (móvil)
        if (_joystickActivo)
        {
            c.FillColor = new Color(1, 1, 1, 0.22f);
            c.FillCircle(_joystickOrigen.X, _joystickOrigen.Y, 55);
            c.FillColor = new Color(1, 1, 1, 0.45f);
            c.FillCircle(_joystickOrigen.X + _joystick.X * 55, _joystickOrigen.Y + _joystick.Y * 55, 22);
        }
    }
}
