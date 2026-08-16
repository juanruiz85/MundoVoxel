using System.Numerics;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Juego;

/// <summary>Física simple del jugador: gravedad, salto, vuelo y colisiones AABB contra bloques.</summary>
public sealed class ControladorJugador
{
    public Vector3 Pos;
    public Vector3 Vel;
    public float Yaw, Pitch;
    public bool EnSuelo;
    public bool Volando;
    public bool Espectador;   // modo espectador: atraviesa bloques y vuela

    const float Gravedad = -24f;
    const float Velocidad = 6f;
    const float VelocidadVuelo = 10f;
    const float Salto = 8.5f;
    const float Radio = 0.3f;
    const float Altura = 1.8f;

    public Vector3 Ojo => Pos + new Vector3(0, 1.62f, 0);

    public void Actualizar(Mundo mundo, float dt, Vector2 entradaMov, bool saltar, bool bajar, bool volar)
    {
        Volando = volar;
        bool estabaEnSuelo = EnSuelo;
        EnSuelo = false;
        var fwd = new Vector3(MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));
        var der = new Vector3(MathF.Cos(Yaw), 0, MathF.Sin(Yaw));
        var dir = fwd * entradaMov.Y + der * entradaMov.X;
        if (dir.LengthSquared() > 1f) dir = Vector3.Normalize(dir);

        var velObjetivo = dir * (Volando || Espectador ? VelocidadVuelo : Velocidad);
        Vel.X = velObjetivo.X;
        Vel.Z = velObjetivo.Z;

        if (Espectador)
        {
            // Volar libre: subir/bajar directo, sin gravedad ni colisiones
            Vel.Y = saltar ? 8f : bajar ? -8f : 0f;
            Pos += Vel * dt;
            return;
        }

        if (Volando)
        {
            Vel.Y = saltar ? 8f : bajar ? -8f : 0f;
        }
        else if (EnAgua(mundo, out _))
        {
            // Nadar: gravedad reducida, subir con espacio, hundirse con shift,
            // y flotar: si no tocas nada, te hundes poco a poco (como Minecraft)
            Vel.Y += Gravedad * 0.3f * dt;
            if (saltar) Vel.Y = 5f;
            if (bajar) Vel.Y = -3f;
            Vel.X *= 0.92f;
            Vel.Z *= 0.92f;
        }
        else
        {
            Vel.Y += Gravedad * dt;
            if (saltar && estabaEnSuelo) Vel.Y = Salto;
        }

        Mover(mundo, dt);
    }

    /// <summary>True si el jugador esta dentro del agua (pecho o pies en agua).</summary>
    public bool EnAgua(Mundo mundo, out bool cabezaEnAgua)
    {
        int bx = (int)MathF.Floor(Pos.X), bz = (int)MathF.Floor(Pos.Z);
        int yPies = (int)MathF.Floor(Pos.Y + 0.2f);
        int yPecho = (int)MathF.Floor(Pos.Y + 1.0f);
        int yCabeza = (int)MathF.Floor(Pos.Y + 1.6f);
        cabezaEnAgua = mundo.Obtener(bx, yCabeza, bz) == Bloques.Agua;
        return mundo.Obtener(bx, yPies, bz) == Bloques.Agua || mundo.Obtener(bx, yPecho, bz) == Bloques.Agua;
    }

    /// <summary>True si la cabeza esta en lava (para el HUD).</summary>
    public bool EnLava(Mundo mundo)
    {
        int bx = (int)MathF.Floor(Pos.X), bz = (int)MathF.Floor(Pos.Z);
        int yCabeza = (int)MathF.Floor(Pos.Y + 1.6f);
        return mundo.Obtener(bx, yCabeza, bz) == Bloques.Lava;
    }

    void Mover(Mundo mundo, float dt)
    {
        // Sub-pasos: si el frame es lento, dividir el movimiento en pasos cortos
        // para no atravesar bloques (el "noclip" accidental al romper hacia abajo
        // ocurria porque un delta grande saltaba la pared de un solo frame).
        var delta = Vel * dt;
        float maxPaso = 0.25f;
        int pasos = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(delta.X), MathF.Max(MathF.Abs(delta.Y), MathF.Abs(delta.Z))) / maxPaso));
        var paso = delta / pasos;
        for (int p = 0; p < pasos; p++)
        {
            for (int eje = 0; eje < 3; eje++)
            {
                switch (eje)
                {
                    case 0: Pos.X += paso.X; break;
                    case 1: Pos.Y += paso.Y; break;
                    default: Pos.Z += paso.Z; break;
                }
                ResolverEje(mundo, eje, paso);
            }
        }
    }

    void ResolverEje(Mundo mundo, int eje, Vector3 delta)
    {
        float d = eje switch { 0 => delta.X, 1 => delta.Y, _ => delta.Z };
        if (d == 0) return;

        var min = Pos - new Vector3(Radio, 0, Radio);
        var max = Pos + new Vector3(Radio, Altura, Radio);
        int x0 = (int)MathF.Floor(min.X), x1 = (int)MathF.Floor(max.X);
        int y0 = (int)MathF.Floor(min.Y), y1 = (int)MathF.Floor(max.Y);
        int z0 = (int)MathF.Floor(min.Z), z1 = (int)MathF.Floor(max.Z);

        bool colision = false;
        for (int x = x0; x <= x1 && !colision; x++)
            for (int y = y0; y <= y1 && !colision; y++)
                for (int z = z0; z <= z1 && !colision; z++)
                    if (Bloques.EsSolido(mundo.Obtener(x, y, z)))
                        colision = true;
        if (!colision) return;

        switch (eje)
        {
            case 0:
                Pos.X = d > 0 ? x0 - Radio - 0.001f : x1 + 1f + Radio + 0.001f;
                Vel.X = 0;
                break;
            case 1:
                if (d > 0)
                {
                    // Golpear el techo: la cabeza (y1) queda justo debajo del bloque
                    Pos.Y = y1 - Altura - 0.001f;
                    Vel.Y = 0;
                }
                else
                {
                    // Aterrizar: el pie (y0) se apoya encima del bloque solido
                    Pos.Y = y0 + 1f + 0.001f;
                    Vel.Y = 0;
                    EnSuelo = true;
                }
                break;
            default:
                Pos.Z = d > 0 ? z0 - Radio - 0.001f : z1 + 1f + Radio + 0.001f;
                Vel.Z = 0;
                break;
        }
    }

    public void CaerAlVacio(Mundo mundo)
    {
        // Si el jugador se queda fuera del mundo (o cae por un agujero), reaparece arriba.
        if (Pos.Y < -16f || Pos.Y > mundo.Alto + 16f)
        {
            var p = mundo.ObtenerPuntoAparicion();
            Pos = p;
            Vel = Vector3.Zero;
        }
    }
}
