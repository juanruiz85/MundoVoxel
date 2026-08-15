using System.Numerics;

namespace MundoVoxel.Core;

/// <summary>Ítems sueltos (no bloques). Los bloques usan su id de <see cref="Bloques"/> (&lt; 1000).</summary>
public enum ItemId : ushort
{
    Palo = 1000,
    CarneCrudaCerdo = 1001,
    CarneCocinadaCerdo = 1002,
    CarneCrudaVaca = 1003,
    CarneCocinadaVaca = 1004,
    CarneCrudaOveja = 1005,
    CarneCocinadaOveja = 1006,
    CarnePodrida = 1007,
    Polvora = 1008,
    Cuero = 1009,
    Hueso = 1010,
    LingoteHierro = 1011,
    Zanahoria = 1012,

    // Herramientas (como en Minecraft clásico/Indev)
    PicoMadera = 1013, PicoPiedra = 1014,
    EspadaMadera = 1015, EspadaPiedra = 1016,
    HachaMadera = 1017, HachaPiedra = 1018,
    PalaMadera = 1019, PalaPiedra = 1020,
    AzadaMadera = 1021, AzadaPiedra = 1022,

    // Alimentos y cultivos
    Manzana = 1023,
    SemillasTrigo = 1024,
    Trigo = 1025,

    // Minerales en bruto (se funden en el horno)
    OroBruto = 1026,
    CobreBruto = 1027,
    HierroBruto = 1028,
    DiamanteBruto = 1029,
    CarbonItem = 1030,

    // Productos fundidos
    LingoteOro = 1031,
    LingoteCobre = 1032,
    Diamante = 1033,
    Lana = 1034,

    // Utilidades
    Mechero = 1035,

    // Herramientas de cobre
    PicoCobre = 1036, EspadaCobre = 1037, HachaCobre = 1038, PalaCobre = 1039, AzadaCobre = 1040,
    // Herramientas de hierro
    PicoHierro = 1041, EspadaHierro = 1042, HachaHierro = 1043, PalaHierro = 1044, AzadaHierro = 1045,
    // Herramientas de oro
    PicoOro = 1046, EspadaOro = 1047, HachaOro = 1048, PalaOro = 1049, AzadaOro = 1050,
    // Herramientas de diamante
    PicoDiamante = 1051, EspadaDiamante = 1052, HachaDiamante = 1053, PalaDiamante = 1054, AzadaDiamante = 1055,
}

/// <summary>Un hueco del inventario: material (bloque o ítem) + cantidad.</summary>
public sealed record SlotInventario(ushort Material, int Cantidad);

/// <summary>Tipo de herramienta (para el render en mano y las mecanicas).</summary>
public enum TipoHerramienta { Ninguna = 0, Pico, Espada, Hacha, Pala, Azada }

/// <summary>Un ingrediente de una receta.</summary>
public sealed record Ingrediente(ushort Material, int Cantidad);

/// <summary>
/// Receta con patrón (forma) estilo Minecraft. El patrón es fila-mayor y 0 = vacío.
/// Los ingredientes se derivan del patrón (conteo de materiales no vacíos).
/// </summary>
public sealed class Receta
{
    public ushort[] Patron;
    public int Ancho, Alto;
    public ushort Salida;
    public int SalidaCantidad;
    public string Nombre;

    public Receta(ushort[] patron, int ancho, int alto, ushort salida, int salidaCantidad, string nombre)
    {
        Patron = patron;
        Ancho = ancho;
        Alto = alto;
        Salida = salida;
        SalidaCantidad = salidaCantidad;
        Nombre = nombre;
    }

    /// <summary>Ingredientes derivados del patrón (conteo de cada material no vacío).</summary>
    public Ingrediente[] Ingredientes()
    {
        var d = new Dictionary<ushort, int>();
        foreach (var m in Patron)
            if (m != 0) d[m] = d.GetValueOrDefault(m) + 1;
        return d.Select(kv => new Ingrediente(kv.Key, kv.Value)).ToArray();
    }
}

/// <summary>Tablas de ítems, recetas (estilo Minecraft), botín de mobs y nombres.</summary>
public static class Objetos
{
    public static bool EsBloque(ushort material) => material < 1000;

    // Materiales abreviados para las recetas
    static ushort T => Bloques.Tablones;   // tablones
    static ushort P => (ushort)ItemId.Palo; // palos
    static ushort S => Bloques.Piedra;      // piedra
    static ushort A => Bloques.Arena;       // arena
    static ushort V => 0;                   // hueco vacío

    /// <summary>Materiales con los que se fabrican herramientas (y su nombre).</summary>
    static readonly (ushort material, string nombre)[] MaterialesHerramienta =
    {
        (Bloques.Tablones, "de madera"),
        (Bloques.Piedra, "de piedra"),
        ((ushort)ItemId.LingoteCobre, "de cobre"),
        ((ushort)ItemId.LingoteHierro, "de hierro"),
        ((ushort)ItemId.LingoteOro, "de oro"),
        ((ushort)ItemId.Diamante, "de diamante"),
    };

    /// <summary>Receta de cada herramienta para un material (pico, espada, hacha, pala, azada).</summary>
    static Receta[] RecetasDeMaterial(ushort mat, string sufijo, ushort pico, ushort espada, ushort hacha, ushort pala, ushort azada)
    {
        return new[]
        {
            new Receta(new[]{ mat, mat, mat, V, P, V, V, P, V }, 3, 3, pico, 1, $"Pico {sufijo}"),
            new Receta(new[]{ mat, mat, P }, 1, 3, espada, 1, $"Espada {sufijo}"),
            new Receta(new[]{ mat, mat, mat, P, V, P }, 2, 3, hacha, 1, $"Hacha {sufijo}"),
            new Receta(new[]{ mat, P, P }, 1, 3, pala, 1, $"Pala {sufijo}"),
            new Receta(new[]{ mat, mat, V, P, V, P }, 2, 3, azada, 1, $"Azada {sufijo}"),
        };
    }

    /// <summary>
    /// Recetas de crafteo con los patrones clásicos de Minecraft (Indev).
    /// 0-14: madera/piedra (base) + arena; 15-34: cobre, hierro, oro y diamante.
    /// </summary>
    public static readonly Receta[] RecetasCrafteo = ConstruirRecetas();

    static Receta[] ConstruirRecetas()
    {
        var lista = new List<Receta>
        {
            // Madera -> 4 tablones (1 tronco)
            new(new[]{ Bloques.Madera }, 1, 1, Bloques.Tablones, 4, "Tablones"),
            // Palos (2 tablones en vertical)
            new(new[]{ T, T }, 1, 2, (ushort)ItemId.Palo, 4, "Palos"),
            // Mesa de trabajo (2x2 tablones)
            new(new[]{ T, T, T, T }, 2, 2, Bloques.Mesa, 1, "Mesa de trabajo"),
            // Horno (8 piedra en anillo)
            new(new[]{ S, S, S, S, V, S, S, S, S }, 3, 3, Bloques.Horno, 1, "Horno"),
            // Arenisca (2x2 arena)
            new(new[]{ A, A, A, A }, 2, 2, Bloques.Arenisca, 1, "Arenisca"),
            // Antorcha (palo + carbon)
            new(new[]{ (ushort)ItemId.CarbonItem, P }, 1, 2, Bloques.Antorcha, 1, "Antorcha"),
            // TNT (4 polvora + 4 arena, anillo)
            new(new[]{ A, A, A, A, (ushort)ItemId.Polvora, A, A, A, A }, 3, 3, Bloques.Tnt, 1, "TNT"),
            // Mechero (lingote de hierro + piedra)
            new(new[]{ (ushort)ItemId.LingoteHierro, S }, 1, 2, (ushort)ItemId.Mechero, 1, "Mechero"),
        };
        // Herramientas de madera y piedra (5-14)
        lista.AddRange(RecetasDeMaterial(Bloques.Tablones, "de madera",
            (ushort)ItemId.PicoMadera, (ushort)ItemId.EspadaMadera, (ushort)ItemId.HachaMadera, (ushort)ItemId.PalaMadera, (ushort)ItemId.AzadaMadera));
        lista.AddRange(RecetasDeMaterial(Bloques.Piedra, "de piedra",
            (ushort)ItemId.PicoPiedra, (ushort)ItemId.EspadaPiedra, (ushort)ItemId.HachaPiedra, (ushort)ItemId.PalaPiedra, (ushort)ItemId.AzadaPiedra));
        // Herramientas de cobre, hierro, oro y diamante (15-34)
        lista.AddRange(RecetasDeMaterial((ushort)ItemId.LingoteCobre, "de cobre",
            (ushort)ItemId.PicoCobre, (ushort)ItemId.EspadaCobre, (ushort)ItemId.HachaCobre, (ushort)ItemId.PalaCobre, (ushort)ItemId.AzadaCobre));
        lista.AddRange(RecetasDeMaterial((ushort)ItemId.LingoteHierro, "de hierro",
            (ushort)ItemId.PicoHierro, (ushort)ItemId.EspadaHierro, (ushort)ItemId.HachaHierro, (ushort)ItemId.PalaHierro, (ushort)ItemId.AzadaHierro));
        lista.AddRange(RecetasDeMaterial((ushort)ItemId.LingoteOro, "de oro",
            (ushort)ItemId.PicoOro, (ushort)ItemId.EspadaOro, (ushort)ItemId.HachaOro, (ushort)ItemId.PalaOro, (ushort)ItemId.AzadaOro));
        lista.AddRange(RecetasDeMaterial((ushort)ItemId.Diamante, "de diamante",
            (ushort)ItemId.PicoDiamante, (ushort)ItemId.EspadaDiamante, (ushort)ItemId.HachaDiamante, (ushort)ItemId.PalaDiamante, (ushort)ItemId.AzadaDiamante));
        return lista.ToArray();
    }

    /// <summary>Cocina/fundición (horno): carne cruda → cocinada y minerales → lingotes. Sin forma (1x1).</summary>
    public static readonly Receta[] RecetasCocina =
    {
        new(new[]{ (ushort)ItemId.CarneCrudaCerdo }, 1, 1, (ushort)ItemId.CarneCocinadaCerdo, 1, "Cerdo cocinado"),
        new(new[]{ (ushort)ItemId.CarneCrudaVaca }, 1, 1, (ushort)ItemId.CarneCocinadaVaca, 1, "Vaca cocinada"),
        new(new[]{ (ushort)ItemId.CarneCrudaOveja }, 1, 1, (ushort)ItemId.CarneCocinadaOveja, 1, "Oveja cocinada"),
        new(new[]{ (ushort)ItemId.OroBruto }, 1, 1, (ushort)ItemId.LingoteOro, 1, "Fundir oro"),
        new(new[]{ (ushort)ItemId.HierroBruto }, 1, 1, (ushort)ItemId.LingoteHierro, 1, "Fundir hierro"),
        new(new[]{ (ushort)ItemId.CobreBruto }, 1, 1, (ushort)ItemId.LingoteCobre, 1, "Fundir cobre"),
        new(new[]{ (ushort)ItemId.DiamanteBruto }, 1, 1, (ushort)ItemId.Diamante, 1, "Fundir diamante"),
        new(new[]{ Bloques.Arena }, 1, 1, Bloques.Cristal, 1, "Fundir arena"),
    };

    /// <summary>Índices de las recetas de fundición (requieren carbón como combustible).</summary>
    public static bool EsFundicion(Receta r)
    {
        var ing = r.Ingredientes();
        if (ing.Length == 0) return false;
        var m = ing[0].Material;
        return m == (ushort)ItemId.OroBruto || m == (ushort)ItemId.HierroBruto
            || m == (ushort)ItemId.CobreBruto || m == (ushort)ItemId.DiamanteBruto
            || m == Bloques.Arena;
    }

    /// <summary>
    /// Empareja una cuadrícula (fila-mayor, 0 = vacío) contra las recetas de crafteo.
    /// Devuelve el índice de la receta que coincide (con desplazamiento permitido), o -1.
    /// </summary>
    public static int CoincidirReceta(ushort[] grid, int gw, int gh)
    {
        for (int i = 0; i < RecetasCrafteo.Length; i++)
            if (Coincide(RecetasCrafteo[i], grid, gw, gh)) return i;
        return -1;
    }

    static bool Coincide(Receta r, ushort[] grid, int gw, int gh)
    {
        if (r.Ancho > gw || r.Alto > gh) return false;
        for (int oy = 0; oy + r.Alto <= gh; oy++)
            for (int ox = 0; ox + r.Ancho <= gw; ox++)
                if (CoincideEn(r, grid, gw, gh, ox, oy)) return true;
        return false;
    }

    static bool CoincideEn(Receta r, ushort[] grid, int gw, int gh, int ox, int oy)
    {
        for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
            {
                bool enPatron = x >= ox && x < ox + r.Ancho && y >= oy && y < oy + r.Alto;
                ushort esperado = enPatron ? r.Patron[(y - oy) * r.Ancho + (x - ox)] : (ushort)0;
                if (grid[y * gw + x] != esperado) return false;
            }
        return true;
    }

    /// <summary>Botín por mob (material, mínimo, máximo).</summary>
    public static (ushort material, int min, int max)[] Loot(TipoMob t) => t switch
    {
        TipoMob.Cerdo => new (ushort, int, int)[] { ((ushort)ItemId.CarneCrudaCerdo, 1, 2) },
        TipoMob.Vaca => new (ushort, int, int)[] { ((ushort)ItemId.CarneCrudaVaca, 1, 2), ((ushort)ItemId.Cuero, 0, 2) },
        TipoMob.Oveja => new (ushort, int, int)[] { ((ushort)ItemId.CarneCrudaOveja, 1, 2), ((ushort)ItemId.Lana, 1, 2) },
        TipoMob.Zombi => new (ushort, int, int)[] { ((ushort)ItemId.CarnePodrida, 0, 2), ((ushort)ItemId.LingoteHierro, 0, 1), ((ushort)ItemId.Zanahoria, 0, 1) },
        TipoMob.Creeper => new (ushort, int, int)[] { ((ushort)ItemId.Polvora, 0, 2) },
        _ => new (ushort, int, int)[] { ((ushort)ItemId.CarnePodrida, 0, 2), ((ushort)ItemId.Hueso, 0, 2) },
    };

    /// <summary>
    /// Lo que suelta un bloque al romperse. Devuelve los materiales (y cantidades)
    /// que caen al suelo. `conPico` indica si el jugador lleva un pico en la mano.
    /// </summary>
    public static (ushort material, int cantidad)[] DropAlRomper(ushort bloque, bool conPico, Random rnd)
    {
        switch (bloque)
        {
            case Bloques.Hoja:
                var l = new List<(ushort, int)>();
                if (rnd.NextDouble() < 0.10) l.Add((Bloques.Planton, 1));
                if (rnd.NextDouble() < 0.06) l.Add(((ushort)ItemId.Manzana, 1));
                if (rnd.NextDouble() < 0.12) l.Add(((ushort)ItemId.Palo, 1));
                return l.ToArray();
            case Bloques.Cesped:
                return rnd.NextDouble() < 0.15 ? new[] { ((ushort)ItemId.SemillasTrigo, 1) } : Array.Empty<(ushort, int)>();
            case Bloques.Piedra:
                return conPico ? new[] { (Bloques.Piedra, 1) } : Array.Empty<(ushort, int)>();
            case Bloques.Carbon:
                return conPico ? new[] { ((ushort)ItemId.CarbonItem, 1) } : Array.Empty<(ushort, int)>();
            case Bloques.Hierro:
                return conPico ? new[] { ((ushort)ItemId.HierroBruto, 1) } : Array.Empty<(ushort, int)>();
            case Bloques.Oro:
                return conPico ? new[] { ((ushort)ItemId.OroBruto, 1) } : Array.Empty<(ushort, int)>();
            case Bloques.Cobre:
                return conPico ? new[] { ((ushort)ItemId.CobreBruto, 1) } : Array.Empty<(ushort, int)>();
            case Bloques.Diamante:
                return conPico ? new[] { ((ushort)ItemId.DiamanteBruto, 1) } : Array.Empty<(ushort, int)>();
            case Bloques.Trigo3:
                return new[] { ((ushort)ItemId.Trigo, 1), ((ushort)ItemId.SemillasTrigo, rnd.Next(1, 3)) };
            case Bloques.Trigo0:
            case Bloques.Trigo1:
            case Bloques.Trigo2:
                return new[] { ((ushort)ItemId.SemillasTrigo, 1) };
            default:
                return new[] { (bloque, 1) };
        }
    }

    /// <summary>Daño extra al golpear mobs con una espada.</summary>
    public static int DanioEspada(ushort item) => item switch
    {
        (ushort)ItemId.EspadaMadera => 2,
        (ushort)ItemId.EspadaPiedra => 4,
        (ushort)ItemId.EspadaCobre => 5,
        (ushort)ItemId.EspadaHierro => 6,
        (ushort)ItemId.EspadaOro => 3,
        (ushort)ItemId.EspadaDiamante => 8,
        _ => 0,
    };

    /// <summary>Indica si el ítem es un pico (necesario para picar piedra y menas).</summary>
    public static bool EsPico(ushort item) => item is
        (ushort)ItemId.PicoMadera or (ushort)ItemId.PicoPiedra or (ushort)ItemId.PicoCobre
        or (ushort)ItemId.PicoHierro or (ushort)ItemId.PicoOro or (ushort)ItemId.PicoDiamante;

    /// <summary>Indica si el ítem es una azada (para labrar la tierra).</summary>
    public static bool EsAzada(ushort item) => item is
        (ushort)ItemId.AzadaMadera or (ushort)ItemId.AzadaPiedra or (ushort)ItemId.AzadaCobre
        or (ushort)ItemId.AzadaHierro or (ushort)ItemId.AzadaOro or (ushort)ItemId.AzadaDiamante;

    /// <summary>Indica si el ítem es un hacha.</summary>
    public static bool EsHacha(ushort item) => item is
        (ushort)ItemId.HachaMadera or (ushort)ItemId.HachaPiedra or (ushort)ItemId.HachaCobre
        or (ushort)ItemId.HachaHierro or (ushort)ItemId.HachaOro or (ushort)ItemId.HachaDiamante;

    /// <summary>Indica si el ítem es una espada (aumenta el daño a mobs).</summary>
    public static bool EsEspada(ushort item) => DanioEspada(item) > 0;

    /// <summary>Tipo de herramienta de un ítem (o Ninguna).</summary>
    public static TipoHerramienta TipoDe(ushort item)
    {
        if (item >= (ushort)ItemId.PicoMadera && item <= (ushort)ItemId.AzadaPiedra)
            return (TipoHerramienta)((item - (ushort)ItemId.PicoMadera) / 2 + 1);
        if (item >= (ushort)ItemId.PicoCobre && item <= (ushort)ItemId.AzadaDiamante)
            return (TipoHerramienta)((item - (ushort)ItemId.PicoCobre) % 5 + 1);
        return TipoHerramienta.Ninguna;
    }

    /// <summary>Indica si el ítem es un mechero (enciende TNT).</summary>
    public static bool EsMechero(ushort item) => item == (ushort)ItemId.Mechero;

    /// <summary>Indica si el ítem es un plantón (se planta y crece un árbol).</summary>
    public static bool EsPlanton(ushort item) => item == Bloques.Planton;

    /// <summary>Indica si el ítem son semillas de trigo.</summary>
    public static bool EsSemilla(ushort item) => item == (ushort)ItemId.SemillasTrigo;

    /// <summary>Bloques que solo sueltan su bloque si se rompen con un pico (piedra y menas).</summary>
    public static bool RequierePico(ushort bloque) => bloque == Bloques.Piedra || Bloques.EsMineral(bloque);

    /// <summary>Nombre legible de un material (bloque o ítem).</summary>
    public static string Nombre(ushort material) => material switch
    {
        Bloques.Tierra => "Tierra",
        Bloques.Piedra => "Piedra",
        Bloques.Madera => "Madera",
        Bloques.Arena => "Arena",
        Bloques.Cesped => "Césped",
        Bloques.Ladrillo => "Ladrillo",
        Bloques.Hoja => "Hojas",
        Bloques.Grava => "Grava",
        Bloques.Cristal => "Cristal",
        Bloques.Tablones => "Tablones",
        Bloques.Horno => "Horno",
        Bloques.Mesa => "Mesa de trabajo",
        Bloques.Arenisca => "Arenisca",
        Bloques.Carbon => "Mineral de carbón",
        Bloques.Hierro => "Mineral de hierro",
        Bloques.Oro => "Mineral de oro",
        Bloques.Diamante => "Mineral de diamante",
        Bloques.Cobre => "Mineral de cobre",
        Bloques.TierraLabrada => "Tierra labrada",
        Bloques.Trigo0 or Bloques.Trigo1 or Bloques.Trigo2 => "Trigo creciendo",
        Bloques.Trigo3 => "Trigo maduro",
        Bloques.Planton => "Plantón",
        Bloques.Tnt => "TNT",
        Bloques.Antorcha => "Antorcha",
        (ushort)ItemId.Palo => "Palo",
        (ushort)ItemId.Manzana => "Manzana",
        (ushort)ItemId.SemillasTrigo => "Semillas de trigo",
        (ushort)ItemId.Trigo => "Trigo",
        (ushort)ItemId.OroBruto => "Oro en bruto",
        (ushort)ItemId.CobreBruto => "Cobre en bruto",
        (ushort)ItemId.HierroBruto => "Hierro en bruto",
        (ushort)ItemId.DiamanteBruto => "Diamante en bruto",
        (ushort)ItemId.CarbonItem => "Carbón",
        (ushort)ItemId.LingoteOro => "Lingote de oro",
        (ushort)ItemId.LingoteCobre => "Lingote de cobre",
        (ushort)ItemId.Diamante => "Diamante",
        (ushort)ItemId.Lana => "Lana",
        (ushort)ItemId.Mechero => "Mechero",
        (ushort)ItemId.CarneCrudaCerdo => "Cerdo crudo",
        (ushort)ItemId.CarneCocinadaCerdo => "Cerdo cocinado",
        (ushort)ItemId.CarneCrudaVaca => "Vaca cruda",
        (ushort)ItemId.CarneCocinadaVaca => "Vaca cocinada",
        (ushort)ItemId.CarneCrudaOveja => "Oveja cruda",
        (ushort)ItemId.CarneCocinadaOveja => "Oveja cocinada",
        (ushort)ItemId.CarnePodrida => "Carne podrida",
        (ushort)ItemId.Polvora => "Pólvora",
        (ushort)ItemId.Cuero => "Cuero",
        (ushort)ItemId.Hueso => "Hueso",
        (ushort)ItemId.LingoteHierro => "Lingote de hierro",
        (ushort)ItemId.Zanahoria => "Zanahoria",
        _ => NombreHerramienta(material),
    };

    static string NombreHerramienta(ushort material) => material switch
    {
        (ushort)ItemId.PicoMadera => "Pico de madera",
        (ushort)ItemId.PicoPiedra => "Pico de piedra",
        (ushort)ItemId.PicoCobre => "Pico de cobre",
        (ushort)ItemId.PicoHierro => "Pico de hierro",
        (ushort)ItemId.PicoOro => "Pico de oro",
        (ushort)ItemId.PicoDiamante => "Pico de diamante",
        (ushort)ItemId.EspadaMadera => "Espada de madera",
        (ushort)ItemId.EspadaPiedra => "Espada de piedra",
        (ushort)ItemId.EspadaCobre => "Espada de cobre",
        (ushort)ItemId.EspadaHierro => "Espada de hierro",
        (ushort)ItemId.EspadaOro => "Espada de oro",
        (ushort)ItemId.EspadaDiamante => "Espada de diamante",
        (ushort)ItemId.HachaMadera => "Hacha de madera",
        (ushort)ItemId.HachaPiedra => "Hacha de piedra",
        (ushort)ItemId.HachaCobre => "Hacha de cobre",
        (ushort)ItemId.HachaHierro => "Hacha de hierro",
        (ushort)ItemId.HachaOro => "Hacha de oro",
        (ushort)ItemId.HachaDiamante => "Hacha de diamante",
        (ushort)ItemId.PalaMadera => "Pala de madera",
        (ushort)ItemId.PalaPiedra => "Pala de piedra",
        (ushort)ItemId.PalaCobre => "Pala de cobre",
        (ushort)ItemId.PalaHierro => "Pala de hierro",
        (ushort)ItemId.PalaOro => "Pala de oro",
        (ushort)ItemId.PalaDiamante => "Pala de diamante",
        (ushort)ItemId.AzadaMadera => "Azada de madera",
        (ushort)ItemId.AzadaPiedra => "Azada de piedra",
        (ushort)ItemId.AzadaCobre => "Azada de cobre",
        (ushort)ItemId.AzadaHierro => "Azada de hierro",
        (ushort)ItemId.AzadaOro => "Azada de oro",
        (ushort)ItemId.AzadaDiamante => "Azada de diamante",
        _ => "Ítem",
    };

    /// <summary>Color representativo de un material para mostrarlo en el inventario/UI.</summary>
    public static (byte r, byte g, byte b) Color(ushort material) => material switch
    {
        Bloques.Tierra => (122, 92, 60),
        Bloques.Piedra => (128, 128, 128),
        Bloques.Madera => (110, 78, 45),
        Bloques.Arena => (226, 208, 160),
        Bloques.Cesped => (96, 160, 52),
        Bloques.Ladrillo => (170, 90, 70),
        Bloques.Hoja => (70, 120, 60),
        Bloques.Grava => (136, 128, 120),
        Bloques.Cristal => (200, 220, 230),
        Bloques.Tablones => (176, 140, 84),
        Bloques.Horno => (90, 90, 96),
        Bloques.Mesa => (160, 130, 80),
        Bloques.Arenisca => (216, 204, 160),
        Bloques.Carbon => (70, 70, 72),
        Bloques.Hierro => (168, 138, 120),
        Bloques.Oro => (212, 178, 72),
        Bloques.Diamante => (96, 200, 196),
        Bloques.Cobre => (200, 120, 90),
        Bloques.TierraLabrada => (110, 80, 52),
        Bloques.Trigo0 or Bloques.Trigo1 or Bloques.Trigo2 => (120, 170, 60),
        Bloques.Trigo3 => (200, 180, 70),
        Bloques.Planton => (80, 150, 70),
        Bloques.Tnt => (200, 60, 50),
        Bloques.Antorcha => (255, 170, 60),
        (ushort)ItemId.Palo => (150, 110, 60),
        (ushort)ItemId.Manzana => (210, 60, 50),
        (ushort)ItemId.SemillasTrigo => (160, 140, 70),
        (ushort)ItemId.Trigo => (210, 190, 90),
        (ushort)ItemId.OroBruto => (230, 200, 90),
        (ushort)ItemId.CobreBruto => (210, 130, 95),
        (ushort)ItemId.HierroBruto => (185, 150, 130),
        (ushort)ItemId.DiamanteBruto => (110, 205, 200),
        (ushort)ItemId.CarbonItem => (40, 40, 42),
        (ushort)ItemId.LingoteOro => (249, 236, 78),
        (ushort)ItemId.LingoteCobre => (210, 130, 95),
        (ushort)ItemId.Diamante => (97, 219, 213),
        (ushort)ItemId.Lana => (235, 235, 235),
        (ushort)ItemId.Mechero => (180, 160, 130),
        (ushort)ItemId.CarneCrudaCerdo or (ushort)ItemId.CarneCrudaVaca or (ushort)ItemId.CarneCrudaOveja => (210, 90, 80),
        (ushort)ItemId.CarneCocinadaCerdo or (ushort)ItemId.CarneCocinadaVaca or (ushort)ItemId.CarneCocinadaOveja => (150, 80, 40),
        (ushort)ItemId.CarnePodrida => (120, 140, 60),
        (ushort)ItemId.Polvora => (140, 140, 140),
        (ushort)ItemId.Cuero => (150, 105, 60),
        (ushort)ItemId.Hueso => (230, 225, 210),
        (ushort)ItemId.LingoteHierro => (210, 210, 220),
        (ushort)ItemId.Zanahoria => (230, 140, 40),
        _ => ColorHerramienta(material),
    };

    static (byte r, byte g, byte b) ColorHerramienta(ushort material) => material switch
    {
        (ushort)ItemId.PicoMadera or (ushort)ItemId.HachaMadera or (ushort)ItemId.PalaMadera or (ushort)ItemId.AzadaMadera or (ushort)ItemId.EspadaMadera => (150, 105, 60),
        (ushort)ItemId.PicoPiedra or (ushort)ItemId.HachaPiedra or (ushort)ItemId.PalaPiedra or (ushort)ItemId.AzadaPiedra or (ushort)ItemId.EspadaPiedra => (150, 150, 155),
        (ushort)ItemId.PicoCobre or (ushort)ItemId.HachaCobre or (ushort)ItemId.PalaCobre or (ushort)ItemId.AzadaCobre or (ushort)ItemId.EspadaCobre => (210, 130, 95),
        (ushort)ItemId.PicoHierro or (ushort)ItemId.HachaHierro or (ushort)ItemId.PalaHierro or (ushort)ItemId.AzadaHierro or (ushort)ItemId.EspadaHierro => (205, 205, 210),
        (ushort)ItemId.PicoOro or (ushort)ItemId.HachaOro or (ushort)ItemId.PalaOro or (ushort)ItemId.AzadaOro or (ushort)ItemId.EspadaOro => (249, 236, 78),
        (ushort)ItemId.PicoDiamante or (ushort)ItemId.HachaDiamante or (ushort)ItemId.PalaDiamante or (ushort)ItemId.AzadaDiamante or (ushort)ItemId.EspadaDiamante => (97, 219, 213),
        _ => (200, 200, 200),
    };
}

/// <summary>Estado de un drop en el suelo (entidad que el jugador recoge al pasar).</summary>
public sealed class Drop
{
    public int Id;
    public ushort Material;
    public float Px, Py, Pz;
}
