namespace MundoVoxel.Core;

/// <summary>
/// Sistema de mobs extensible: cada mob se define con un diseno voxel
/// (celdas con color) + datos de comportamiento (tamano, hostilidad,
/// velocidad, area de agresion). Para anadir un mob nuevo basta con:
///   1. Anadir un valor al enum <see cref="TipoMob"/>.
///   2. Anadir su diseno aqui (capas ASCII + paleta).
///   3. Anadir su fila en <see cref="MobsInfo.Datos"/> (comportamiento).
///   4. Anadir su botin en <see cref="Objetos.Loot"/>.
/// El servidor lo genera solo y el cliente lo dibuja solo.
/// </summary>
public sealed class CeldaDiseno
{
    public readonly sbyte X, Y, Z;
    public readonly byte R, G, B;

    public CeldaDiseno(sbyte x, sbyte y, sbyte z, byte r, byte g, byte b)
    {
        X = x; Y = y; Z = z; R = r; G = g; B = b;
    }
}

/// <summary>Figura voxel de un mob (celdas en una cuadricula pequena).</summary>
public sealed class DisenoMob
{
    public readonly int AnchoCeldas, AltoCeldas, ProfCeldas;
    public readonly CeldaDiseno[] Celdas;

    public DisenoMob(int anchoCeldas, int altoCeldas, int profCeldas, CeldaDiseno[] celdas)
    {
        AnchoCeldas = anchoCeldas;
        AltoCeldas = altoCeldas;
        ProfCeldas = profCeldas;
        Celdas = celdas;
    }

    /// <summary>
    /// Construye un diseno desde capas ASCII (de abajo hacia arriba).
    /// Cada capa es una lista de filas (eje Z); cada fila es un string (eje X).
    /// '.' o caracteres sin paleta = celda vacia.
    /// </summary>
    public static DisenoMob DesdeCapas(string[][] capas, IReadOnlyDictionary<char, (byte r, byte g, byte b)> paleta)
    {
        int alto = capas.Length;
        int prof = capas[0].Length;
        int ancho = capas[0][0].Length;
        var celdas = new List<CeldaDiseno>();
        for (int y = 0; y < alto; y++)
        {
            for (int z = 0; z < prof; z++)
            {
                var fila = capas[y][z];
                for (int x = 0; x < ancho && x < fila.Length; x++)
                {
                    char ch = fila[x];
                    if (ch == '.' || !paleta.TryGetValue(ch, out var rgb)) continue;
                    celdas.Add(new CeldaDiseno((sbyte)x, (sbyte)y, (sbyte)z, rgb.r, rgb.g, rgb.b));
                }
            }
        }
        return new DisenoMob(ancho, alto, prof, celdas.ToArray());
    }
}

/// <summary>Disenos voxel de los mobs (estilo voxel-art de Minecraft).</summary>
public static partial class MobsInfo
{
    public static DisenoMob Diseno(TipoMob t) => t switch
    {
        TipoMob.Cerdo => _cerdo,
        TipoMob.Vaca => _vaca,
        TipoMob.Oveja => _oveja,
        TipoMob.Zombi => _zombi,
        TipoMob.Creeper => _creeper,
        _ => _esqueleto,
    };

    static readonly DisenoMob _cerdo = DisenoMob.DesdeCapas(new[]
    {
        // y0: patas
        new[] { ".p..p.", "......", "......", ".p..p." },
        // y1: cuerpo
        new[] { "......", ".cccc.", ".cccc.", "......" },
        // y2: cuerpo + cabeza
        new[] { "......", ".cccc.", ".cccc.", "..cn.." },
        // y3: cabeza
        new[] { "......", "......", "......", "..cc.." },
        // y4: orejas
        new[] { "......", "......", "......", ".o..o." },
    }, new Dictionary<char, (byte, byte, byte)>
    {
        ['c'] = (231, 154, 154),
        ['n'] = (198, 108, 108),
        ['p'] = (222, 140, 140),
        ['o'] = (198, 108, 108),
    });

    static readonly DisenoMob _vaca = DisenoMob.DesdeCapas(new[]
    {
        new[] { ".p..p.", "......", "......", ".p..p." },
        new[] { "......", ".ccwc.", ".cwcc.", "......" },
        new[] { "......", ".cccc.", ".cccc.", "..nn.." },
        new[] { "......", "......", "......", "..cc.." },
        new[] { "......", "......", "......", ".h..h." },
    }, new Dictionary<char, (byte, byte, byte)>
    {
        ['c'] = (139, 90, 43),
        ['w'] = (235, 235, 235),
        ['n'] = (110, 70, 35),
        ['p'] = (120, 80, 40),
        ['h'] = (210, 205, 190),
    });

    static readonly DisenoMob _oveja = DisenoMob.DesdeCapas(new[]
    {
        new[] { ".p..p.", "......", "......", ".p..p." },
        new[] { "......", ".cccc.", ".cccc.", "......" },
        new[] { "......", ".cccc.", ".cccc.", "..cn.." },
        new[] { "......", "......", "......", "..cc.." },
        new[] { "......", "......", "......", ".o..o." },
    }, new Dictionary<char, (byte, byte, byte)>
    {
        ['c'] = (232, 232, 232),
        ['n'] = (205, 195, 185),
        ['p'] = (160, 150, 140),
        ['o'] = (200, 190, 180),
    });

    static readonly DisenoMob _zombi = DisenoMob.DesdeCapas(new[]
    {
        new[] { "....", ".pp.", ".pp.", "...." },
        new[] { "....", ".pp.", ".pp.", "...." },
        new[] { "....", ".cc.", ".cc.", "...." },
        new[] { "....", "sccs", "sccs", "...." },
        new[] { "....", "ssss", "ssss", "...." },
        new[] { "....", ".ss.", ".ss.", "...." },
    }, new Dictionary<char, (byte, byte, byte)>
    {
        ['s'] = (78, 154, 78),
        ['c'] = (70, 110, 150),
        ['p'] = (60, 70, 110),
    });

    static readonly DisenoMob _creeper = DisenoMob.DesdeCapas(new[]
    {
        new[] { "p..p", "....", "....", "p..p" },
        new[] { "....", ".cc.", ".cc.", "...." },
        new[] { "....", ".cc.", ".cc.", "...." },
        new[] { "....", ".cd.", ".dd.", "...." },
        new[] { "....", ".cc.", ".cc.", "...." },
    }, new Dictionary<char, (byte, byte, byte)>
    {
        ['c'] = (95, 160, 95),
        ['d'] = (55, 110, 55),
        ['p'] = (70, 130, 70),
    });

    static readonly DisenoMob _esqueleto = DisenoMob.DesdeCapas(new[]
    {
        new[] { "....", ".bb.", ".bb.", "...." },
        new[] { "....", ".bb.", ".bb.", "...." },
        new[] { "....", ".ww.", ".ww.", "...." },
        new[] { "....", "wwww", "wwww", "...." },
        new[] { "....", ".ww.", ".ww.", "...." },
        new[] { "....", ".ww.", ".ww.", "...." },
    }, new Dictionary<char, (byte, byte, byte)>
    {
        ['w'] = (232, 232, 235),
        ['b'] = (190, 190, 195),
    });
}
