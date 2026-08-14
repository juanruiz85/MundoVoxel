using System.Numerics;

namespace MundoVoxel.Core;

public readonly record struct GolpeRayo(Vector3 Posicion, Vector3 Normal, int X, int Y, int Z, bool Impacto);

/// <summary>Lanzamiento de rayos por voxeles (algoritmo de Amanatides &amp; Woo).</summary>
public static class Rayos
{
    public static GolpeRayo Lanzar(Mundo mundo, Vector3 origen, Vector3 dir, float maxDist)
    {
        int x = (int)MathF.Floor(origen.X), y = (int)MathF.Floor(origen.Y), z = (int)MathF.Floor(origen.Z);
        int stepX = Math.Sign(dir.X), stepY = Math.Sign(dir.Y), stepZ = Math.Sign(dir.Z);
        float tDeltaX = stepX != 0 ? MathF.Abs(1f / dir.X) : float.MaxValue;
        float tDeltaY = stepY != 0 ? MathF.Abs(1f / dir.Y) : float.MaxValue;
        float tDeltaZ = stepZ != 0 ? MathF.Abs(1f / dir.Z) : float.MaxValue;
        float tMaxX = stepX != 0 ? (stepX > 0 ? x + 1 - origen.X : origen.X - x) * tDeltaX : float.MaxValue;
        float tMaxY = stepY != 0 ? (stepY > 0 ? y + 1 - origen.Y : origen.Y - y) * tDeltaY : float.MaxValue;
        float tMaxZ = stepZ != 0 ? (stepZ > 0 ? z + 1 - origen.Z : origen.Z - z) * tDeltaZ : float.MaxValue;
        int nx = 0, ny = 0, nz = 0;
        float t = 0;
        while (t <= maxDist)
        {
            var b = mundo.Obtener(x, y, z);
            if (b != Bloques.Aire && b != Bloques.Agua)
                return new GolpeRayo(origen + dir * t, new Vector3(nx, ny, nz), x, y, z, true);

            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                x += stepX; t = tMaxX; tMaxX += tDeltaX; nx = -stepX; ny = 0; nz = 0;
            }
            else if (tMaxY < tMaxZ)
            {
                y += stepY; t = tMaxY; tMaxY += tDeltaY; nx = 0; ny = -stepY; nz = 0;
            }
            else
            {
                z += stepZ; t = tMaxZ; tMaxZ += tDeltaZ; nx = 0; ny = 0; nz = -stepZ;
            }
        }
        return new GolpeRayo(origen + dir * maxDist, Vector3.Zero, 0, 0, 0, false);
    }
}
