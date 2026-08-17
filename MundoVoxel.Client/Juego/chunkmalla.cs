using System.Numerics;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Juego;

/// <summary>Cara de un chunk. ColorArgb >= 0 anula el color de la paleta del bloque
/// (color base al que el renderizador aplica sombra y niebla); Emisivo=true ignora
/// sombra/niebla (llamas de antorcha: siempre brillan).</summary>
public readonly record struct Cara(Vector3 A, Vector3 B, Vector3 C, Vector3 D, ushort Bloque, byte Dir, int ColorArgb = -1, bool Emisivo = false);

/// <summary>Malla de un chunk: solo las caras expuestas (vecino transparente o aire).</summary>
public sealed class ChunkMalla
{
    public const int Tam = 16;
    public readonly int Cx, Cz;
    public List<Cara> Caras { get; private set; } = new();

    // Colores base (ARGB) para las formas especiales
    const int C_Tierra = 0x6E4E2D;      // 110,78,45
    const int C_Poste = 0x966E46;       // 150,110,70 (antorcha)
    const int C_Llama1 = 0xFF9632;      // 255,150,50
    const int C_Llama2 = 0xFFDC5A;      // 255,220,90
    const int C_Cofre = 0x8C6437;       // 140,100,55
    const int C_CofreTapa = 0x73502D;   // 115,80,45
    const int C_CofreMetal = 0xDCC650;  // 220,198,80
    const int C_Mesa = 0x966E3C;        // 150,110,60
    const int C_MesaPata = 0x78542C;    // 120,84,44

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
                    if (b == Bloques.Antorcha) { AgregarAntorcha(caras, x, y, z); continue; }
                    if (b == Bloques.Cofre) { AgregarCofre(caras, x, y, z, mundo); continue; }
                    if (b == Bloques.Mesa) { AgregarMesa(caras, x, y, z, mundo); continue; }
                    if (EsVisible(b, mundo.Obtener(x + 1, y, z))) Agregar(caras, x, y, z, 0, b, ColorCara(b, 0));
                    if (EsVisible(b, mundo.Obtener(x - 1, y, z))) Agregar(caras, x, y, z, 1, b, ColorCara(b, 1));
                    if (EsVisible(b, mundo.Obtener(x, y + 1, z))) Agregar(caras, x, y, z, 2, b, ColorCara(b, 2));
                    if (EsVisible(b, mundo.Obtener(x, y - 1, z))) Agregar(caras, x, y, z, 3, b, ColorCara(b, 3));
                    if (EsVisible(b, mundo.Obtener(x, y, z + 1))) Agregar(caras, x, y, z, 4, b, ColorCara(b, 4));
                    if (EsVisible(b, mundo.Obtener(x, y, z - 1))) Agregar(caras, x, y, z, 5, b, ColorCara(b, 5));
                }
            }
        }
        Caras = caras;
    }

    static bool EsVisible(ushort bloque, ushort vecino)
    {
        if (vecino == Bloques.Aire) return true;
        // Un bloque opaco genera cara cuando el vecino es transparente (aire,
        // agua, lava, hojas, cristal...). Sin esto, la interfaz agua-piedra no
        // tenia NINGUNA cara: desde dentro del agua se veia a traves de la
        // piedra ("todo transparente" / parecia que atravesabas los bloques).
        if (!Bloques.EsTransparente(bloque)) return Bloques.EsTransparente(vecino);
        if (vecino == bloque) return false;                // agua-agua, hoja-hoja
        return Bloques.EsTransparente(vecino);             // transparente frente a otro transparente
    }

    static bool EsOpaco(Mundo mundo, int x, int y, int z)
        => mundo.Dentro(x, y, z) && !Bloques.EsTransparente(mundo.Obtener(x, y, z));

    /// <summary>Color base propio de la cara (o -1 para usar la paleta del bloque).
    /// El cesped tiene los lados y la cara inferior de tierra; la superior verde.</summary>
    static int ColorCara(ushort b, int dir)
        => b == Bloques.Cesped && dir != 2 ? C_Tierra : -1;

    static readonly (Vector3 a, Vector3 b, Vector3 c, Vector3 d)[] Esquinas =
    {
        (new(1,0,0), new(1,1,0), new(1,1,1), new(1,0,1)), // +X
        (new(0,0,1), new(0,1,1), new(0,1,0), new(0,0,0)), // -X
        (new(0,1,0), new(1,1,0), new(1,1,1), new(0,1,1)), // +Y (arriba)
        (new(0,0,1), new(1,0,1), new(1,0,0), new(0,0,0)), // -Y (abajo)
        (new(0,0,1), new(1,0,1), new(1,1,1), new(0,1,1)), // +Z
        (new(1,0,0), new(0,0,0), new(0,1,0), new(1,1,0)), // -Z
    };

    static void Agregar(List<Cara> caras, int x, int y, int z, int dir, ushort bloque, int colorArgb = -1)
    {
        var (a, b, c, d) = Esquinas[dir];
        var o = new Vector3(x, y, z);
        caras.Add(new Cara(a + o, b + o, c + o, d + o, bloque, (byte)dir, colorArgb));
    }

    static void AgregarCara(List<Cara> caras, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        ushort bloque, byte dir, int colorArgb = -1, bool emisivo = false)
        => caras.Add(new Cara(a, b, c, d, bloque, dir, colorArgb, emisivo));

    /// <summary>La antorcha no es un cubo: un poste delgado de madera con una llama
    /// (dos planos cruzados) que siempre brilla. Las particulas de fuego animadas
    /// se dibujan aparte en la vista (VistaJuego.DibujarAntorchas).</summary>
    static void AgregarAntorcha(List<Cara> caras, int x, int y, int z)
    {
        const float p = 0.38f, q = 0.62f, y0 = 0.10f, y1 = 0.60f;
        // Poste (4 lados + tope)
        AgregarCara(caras, new(x + q, y + y0, z + p), new(x + q, y + y1, z + p), new(x + q, y + y1, z + q), new(x + q, y + y0, z + q), Bloques.Antorcha, 0, C_Poste);
        AgregarCara(caras, new(x + p, y + y0, z + q), new(x + p, y + y1, z + q), new(x + p, y + y1, z + p), new(x + p, y + y0, z + p), Bloques.Antorcha, 1, C_Poste);
        AgregarCara(caras, new(x + p, y + y0, z + q), new(x + q, y + y0, z + q), new(x + q, y + y1, z + q), new(x + p, y + y1, z + q), Bloques.Antorcha, 4, C_Poste);
        AgregarCara(caras, new(x + q, y + y0, z + p), new(x + p, y + y0, z + p), new(x + p, y + y1, z + p), new(x + q, y + y1, z + p), Bloques.Antorcha, 5, C_Poste);
        AgregarCara(caras, new(x + p, y + y1, z + p), new(x + q, y + y1, z + p), new(x + q, y + y1, z + q), new(x + p, y + y1, z + q), Bloques.Antorcha, 2, C_Poste);
        // Llama: dos planos verticales cruzados, naranja abajo y amarillo arriba
        const float ly1 = 0.92f, ly2 = 1.04f;
        AgregarCara(caras, new(x + 0.44f, y + y1, z + 0.50f), new(x + 0.56f, y + y1, z + 0.50f), new(x + 0.56f, y + ly1, z + 0.50f), new(x + 0.44f, y + ly1, z + 0.50f), Bloques.Antorcha, 0, C_Llama1, emisivo: true);
        AgregarCara(caras, new(x + 0.44f, y + ly1, z + 0.50f), new(x + 0.56f, y + ly1, z + 0.50f), new(x + 0.56f, y + ly2, z + 0.50f), new(x + 0.44f, y + ly2, z + 0.50f), Bloques.Antorcha, 0, C_Llama2, emisivo: true);
        AgregarCara(caras, new(x + 0.50f, y + y1, z + 0.44f), new(x + 0.50f, y + y1, z + 0.56f), new(x + 0.50f, y + ly1, z + 0.56f), new(x + 0.50f, y + ly1, z + 0.44f), Bloques.Antorcha, 4, C_Llama1, emisivo: true);
        AgregarCara(caras, new(x + 0.50f, y + ly1, z + 0.44f), new(x + 0.50f, y + ly1, z + 0.56f), new(x + 0.50f, y + ly2, z + 0.56f), new(x + 0.50f, y + ly2, z + 0.44f), Bloques.Antorcha, 4, C_Llama2, emisivo: true);
    }

    /// <summary>Cofre: caja con tapa sobresaliente y cerradura metalica al frente (+Z).</summary>
    static void AgregarCofre(List<Cara> caras, int x, int y, int z, Mundo mundo)
    {
        // Base
        if (!EsOpaco(mundo, x + 1, y, z)) AgregarCara(caras, new(x + 0.94f, y, z + 0.06f), new(x + 0.94f, y + 0.55f, z + 0.06f), new(x + 0.94f, y + 0.55f, z + 0.94f), new(x + 0.94f, y, z + 0.94f), Bloques.Cofre, 0, C_Cofre);
        if (!EsOpaco(mundo, x - 1, y, z)) AgregarCara(caras, new(x + 0.06f, y, z + 0.94f), new(x + 0.06f, y + 0.55f, z + 0.94f), new(x + 0.06f, y + 0.55f, z + 0.06f), new(x + 0.06f, y, z + 0.06f), Bloques.Cofre, 1, C_Cofre);
        if (!EsOpaco(mundo, x, y, z + 1)) AgregarCara(caras, new(x + 0.06f, y, z + 0.94f), new(x + 0.94f, y, z + 0.94f), new(x + 0.94f, y + 0.55f, z + 0.94f), new(x + 0.06f, y + 0.55f, z + 0.94f), Bloques.Cofre, 4, C_Cofre);
        if (!EsOpaco(mundo, x, y, z - 1)) AgregarCara(caras, new(x + 0.94f, y, z + 0.06f), new(x + 0.06f, y, z + 0.06f), new(x + 0.06f, y + 0.55f, z + 0.06f), new(x + 0.94f, y + 0.55f, z + 0.06f), Bloques.Cofre, 5, C_Cofre);
        if (!EsOpaco(mundo, x, y + 1, z)) AgregarCara(caras, new(x + 0.06f, y + 0.55f, z + 0.06f), new(x + 0.94f, y + 0.55f, z + 0.06f), new(x + 0.94f, y + 0.55f, z + 0.94f), new(x + 0.06f, y + 0.55f, z + 0.94f), Bloques.Cofre, 2, C_Cofre);
        // Cerradura metalica en la cara frontal (+Z)
        if (!EsOpaco(mundo, x, y, z + 1)) AgregarCara(caras, new(x + 0.44f, y + 0.22f, z + 0.94f), new(x + 0.56f, y + 0.22f, z + 0.94f), new(x + 0.56f, y + 0.34f, z + 0.94f), new(x + 0.44f, y + 0.34f, z + 0.94f), Bloques.Cofre, 4, C_CofreMetal);
        // Tapa (sobresale un poco)
        if (!EsOpaco(mundo, x + 1, y, z)) AgregarCara(caras, new(x + 0.98f, y + 0.55f, z + 0.02f), new(x + 0.98f, y + 0.80f, z + 0.02f), new(x + 0.98f, y + 0.80f, z + 0.98f), new(x + 0.98f, y + 0.55f, z + 0.98f), Bloques.Cofre, 0, C_CofreTapa);
        if (!EsOpaco(mundo, x - 1, y, z)) AgregarCara(caras, new(x + 0.02f, y + 0.55f, z + 0.98f), new(x + 0.02f, y + 0.80f, z + 0.98f), new(x + 0.02f, y + 0.80f, z + 0.02f), new(x + 0.02f, y + 0.55f, z + 0.02f), Bloques.Cofre, 1, C_CofreTapa);
        if (!EsOpaco(mundo, x, y, z + 1)) AgregarCara(caras, new(x + 0.02f, y + 0.55f, z + 0.98f), new(x + 0.98f, y + 0.55f, z + 0.98f), new(x + 0.98f, y + 0.80f, z + 0.98f), new(x + 0.02f, y + 0.80f, z + 0.98f), Bloques.Cofre, 4, C_CofreTapa);
        if (!EsOpaco(mundo, x, y, z - 1)) AgregarCara(caras, new(x + 0.98f, y + 0.55f, z + 0.02f), new(x + 0.02f, y + 0.55f, z + 0.02f), new(x + 0.02f, y + 0.80f, z + 0.02f), new(x + 0.98f, y + 0.80f, z + 0.02f), Bloques.Cofre, 5, C_CofreTapa);
        if (!EsOpaco(mundo, x, y + 1, z)) AgregarCara(caras, new(x + 0.02f, y + 0.80f, z + 0.02f), new(x + 0.98f, y + 0.80f, z + 0.02f), new(x + 0.98f, y + 0.80f, z + 0.98f), new(x + 0.02f, y + 0.80f, z + 0.98f), Bloques.Cofre, 2, C_CofreTapa);
    }

    /// <summary>Mesa de crafteo: tablero grueso con 4 patas.</summary>
    static void AgregarMesa(List<Cara> caras, int x, int y, int z, Mundo mundo)
    {
        // Tablero (y+0.82..0.96)
        if (!EsOpaco(mundo, x + 1, y, z)) AgregarCara(caras, new(x + 0.96f, y + 0.82f, z + 0.04f), new(x + 0.96f, y + 0.96f, z + 0.04f), new(x + 0.96f, y + 0.96f, z + 0.96f), new(x + 0.96f, y + 0.82f, z + 0.96f), Bloques.Mesa, 0, C_Mesa);
        if (!EsOpaco(mundo, x - 1, y, z)) AgregarCara(caras, new(x + 0.04f, y + 0.82f, z + 0.96f), new(x + 0.04f, y + 0.96f, z + 0.96f), new(x + 0.04f, y + 0.96f, z + 0.04f), new(x + 0.04f, y + 0.82f, z + 0.04f), Bloques.Mesa, 1, C_Mesa);
        if (!EsOpaco(mundo, x, y, z + 1)) AgregarCara(caras, new(x + 0.04f, y + 0.82f, z + 0.96f), new(x + 0.96f, y + 0.82f, z + 0.96f), new(x + 0.96f, y + 0.96f, z + 0.96f), new(x + 0.04f, y + 0.96f, z + 0.96f), Bloques.Mesa, 4, C_Mesa);
        if (!EsOpaco(mundo, x, y, z - 1)) AgregarCara(caras, new(x + 0.96f, y + 0.82f, z + 0.04f), new(x + 0.04f, y + 0.82f, z + 0.04f), new(x + 0.04f, y + 0.96f, z + 0.04f), new(x + 0.96f, y + 0.96f, z + 0.04f), Bloques.Mesa, 5, C_Mesa);
        if (!EsOpaco(mundo, x, y + 1, z)) AgregarCara(caras, new(x + 0.04f, y + 0.96f, z + 0.04f), new(x + 0.96f, y + 0.96f, z + 0.04f), new(x + 0.96f, y + 0.96f, z + 0.96f), new(x + 0.04f, y + 0.96f, z + 0.96f), Bloques.Mesa, 2, C_Mesa);
        // 4 patas (y+0..0.82)
        AgregarPata(caras, x, y, z, 0.08f, 0.22f, 0.08f, 0.22f, mundo);
        AgregarPata(caras, x, y, z, 0.78f, 0.92f, 0.08f, 0.22f, mundo);
        AgregarPata(caras, x, y, z, 0.08f, 0.22f, 0.78f, 0.92f, mundo);
        AgregarPata(caras, x, y, z, 0.78f, 0.92f, 0.78f, 0.92f, mundo);
    }

    static void AgregarPata(List<Cara> caras, int x, int y, int z, float xa, float xb, float za, float zb, Mundo mundo)
    {
        AgregarCara(caras, new(x + xb, y, z + za), new(x + xb, y + 0.82f, z + za), new(x + xb, y + 0.82f, z + zb), new(x + xb, y, z + zb), Bloques.Mesa, 0, C_MesaPata);
        AgregarCara(caras, new(x + xa, y, z + zb), new(x + xa, y + 0.82f, z + zb), new(x + xa, y + 0.82f, z + za), new(x + xa, y, z + za), Bloques.Mesa, 1, C_MesaPata);
        AgregarCara(caras, new(x + xa, y, z + zb), new(x + xb, y, z + zb), new(x + xb, y + 0.82f, z + zb), new(x + xa, y + 0.82f, z + zb), Bloques.Mesa, 4, C_MesaPata);
        AgregarCara(caras, new(x + xb, y, z + za), new(x + xa, y, z + za), new(x + xa, y + 0.82f, z + za), new(x + xb, y + 0.82f, z + za), Bloques.Mesa, 5, C_MesaPata);
    }
}
