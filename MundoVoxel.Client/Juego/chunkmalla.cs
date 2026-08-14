using System.Numerics;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Juego;

public readonly record struct Cara(Vector3 A, Vector3 B, Vector3 C, Vector3 D, ushort Bloque, byte Dir);

/// <summary>Malla de un chunk: solo las caras expuestas (vecino transparente o aire).</summary>
public sealed class ChunkMalla
{
    public const int Tam = 16;
    public readonly int Cx, Cz;
    public List<Cara> Caras { get; private set; } = new();

    public ChunkMalla(int cx, int cz)
    {
        Cx = cx;
        Cz = cz;
    }

    public void Reconstruir(Mundo mundo)
    {
        var caras = new List<Cara>(4096);
        int x0 = Cx * Tam, z0 = Cz * Tam;
        int x1 = Math.Min(x0 + Tam, mundo.Ancho), z1 = Math.Min(z0 + Tam, mundo.Profundo);
        for (int x = x0; x < x1; x++)
        {
            for (int z = z0; z < z1; z++)
            {
                for (int y = 0; y < mundo.Alto; y++)
                {
                    ushort b = mundo.Obtener(x, y, z);
                    if (b == Bloques.Aire) continue;
                    if (EsVisible(b, mundo.Obtener(x + 1, y, z))) Agregar(caras, x, y, z, 0, b);
                    if (EsVisible(b, mundo.Obtener(x - 1, y, z))) Agregar(caras, x, y, z, 1, b);
                    if (EsVisible(b, mundo.Obtener(x, y + 1, z))) Agregar(caras, x, y, z, 2, b);
                    if (EsVisible(b, mundo.Obtener(x, y - 1, z))) Agregar(caras, x, y, z, 3, b);
                    if (EsVisible(b, mundo.Obtener(x, y, z + 1))) Agregar(caras, x, y, z, 4, b);
                    if (EsVisible(b, mundo.Obtener(x, y, z - 1))) Agregar(caras, x, y, z, 5, b);
                }
            }
        }
        Caras = caras;
    }

    static bool EsVisible(ushort bloque, ushort vecino)
    {
        if (vecino == Bloques.Aire) return true;
        if (!Bloques.EsTransparente(bloque)) return false; // opaco: oculto tras cualquier vecino no aire
        if (vecino == bloque) return false;                // agua-agua, hoja-hoja
        return Bloques.EsTransparente(vecino);             // transparente frente a otro transparente
    }

    static readonly (Vector3 a, Vector3 b, Vector3 c, Vector3 d)[] Esquinas =
    {
        (new(1,0,0), new(1,1,0), new(1,1,1), new(1,0,1)), // +X
        (new(0,0,1), new(0,1,1), new(0,1,0), new(0,0,0)), // -X
        (new(0,1,0), new(1,1,0), new(1,1,1), new(0,1,1)), // +Y (arriba)
        (new(0,0,1), new(1,0,1), new(1,0,0), new(0,0,0)), // -Y (abajo)
        (new(0,0,1), new(1,0,1), new(1,1,1), new(0,1,1)), // +Z
        (new(1,0,0), new(0,0,0), new(0,1,0), new(1,1,0)), // -Z
    };

    static void Agregar(List<Cara> caras, int x, int y, int z, int dir, ushort bloque)
    {
        var (a, b, c, d) = Esquinas[dir];
        var o = new Vector3(x, y, z);
        caras.Add(new Cara(a + o, b + o, c + o, d + o, bloque, (byte)dir));
    }
}
