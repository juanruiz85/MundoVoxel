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
}

/// <summary>Un hueco del inventario: material (bloque o ítem) + cantidad.</summary>
public sealed record SlotInventario(ushort Material, int Cantidad);

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

    /// <summary>
    /// Recetas de crafteo con los patrones clásicos de Minecraft (Indev),
    /// adaptadas a los materiales disponibles. 0 = hueco vacío.
    /// </summary>
    public static readonly Receta[] RecetasCrafteo =
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
        // --- Herramientas de madera ---
        new(new[]{ T, T, T, V, P, V, V, P, V }, 3, 3, (ushort)ItemId.PicoMadera, 1, "Pico de madera"),
        new(new[]{ T, T, P }, 1, 3, (ushort)ItemId.EspadaMadera, 1, "Espada de madera"),
        new(new[]{ T, T, T, P, V, P }, 2, 3, (ushort)ItemId.HachaMadera, 1, "Hacha de madera"),
        new(new[]{ T, P, P }, 1, 3, (ushort)ItemId.PalaMadera, 1, "Pala de madera"),
        new(new[]{ T, T, V, P, V, P }, 2, 3, (ushort)ItemId.AzadaMadera, 1, "Azada de madera"),
        // --- Herramientas de piedra ---
        new(new[]{ S, S, S, V, P, V, V, P, V }, 3, 3, (ushort)ItemId.PicoPiedra, 1, "Pico de piedra"),
        new(new[]{ S, S, P }, 1, 3, (ushort)ItemId.EspadaPiedra, 1, "Espada de piedra"),
        new(new[]{ S, S, S, P, V, P }, 2, 3, (ushort)ItemId.HachaPiedra, 1, "Hacha de piedra"),
        new(new[]{ S, P, P }, 1, 3, (ushort)ItemId.PalaPiedra, 1, "Pala de piedra"),
        new(new[]{ S, S, V, P, V, P }, 2, 3, (ushort)ItemId.AzadaPiedra, 1, "Azada de piedra"),
    };

    /// <summary>Cocina (fundición): carne cruda → cocinada. Sin forma (1x1).</summary>
    public static readonly Receta[] RecetasCocina =
    {
        new(new[]{ (ushort)ItemId.CarneCrudaCerdo }, 1, 1, (ushort)ItemId.CarneCocinadaCerdo, 1, "Cerdo cocinado"),
        new(new[]{ (ushort)ItemId.CarneCrudaVaca }, 1, 1, (ushort)ItemId.CarneCocinadaVaca, 1, "Vaca cocinada"),
        new(new[]{ (ushort)ItemId.CarneCrudaOveja }, 1, 1, (ushort)ItemId.CarneCocinadaOveja, 1, "Oveja cocinada"),
    };

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
        TipoMob.Oveja => new (ushort, int, int)[] { ((ushort)ItemId.CarneCrudaOveja, 1, 2) },
        TipoMob.Zombi => new (ushort, int, int)[] { ((ushort)ItemId.CarnePodrida, 0, 2), ((ushort)ItemId.LingoteHierro, 0, 1), ((ushort)ItemId.Zanahoria, 0, 1) },
        TipoMob.Creeper => new (ushort, int, int)[] { ((ushort)ItemId.Polvora, 0, 2) },
        _ => new (ushort, int, int)[] { ((ushort)ItemId.CarnePodrida, 0, 2), ((ushort)ItemId.Hueso, 0, 2) },
    };

    /// <summary>Daño extra al golpear mobs con una espada.</summary>
    public static int DanioEspada(ushort item) => item switch
    {
        (ushort)ItemId.EspadaMadera => 2,
        (ushort)ItemId.EspadaPiedra => 4,
        _ => 0,
    };

    /// <summary>Indica si el ítem es un pico (necesario para picar piedra).</summary>
    public static bool EsPico(ushort item) => item == (ushort)ItemId.PicoMadera || item == (ushort)ItemId.PicoPiedra;

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
        Bloques.Carbon => "Carbon",
        Bloques.Hierro => "Hierro",
        Bloques.Oro => "Oro",
        Bloques.Diamante => "Diamante",
        (ushort)ItemId.Palo => "Palo",
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
        (ushort)ItemId.PicoMadera => "Pico de madera",
        (ushort)ItemId.PicoPiedra => "Pico de piedra",
        (ushort)ItemId.EspadaMadera => "Espada de madera",
        (ushort)ItemId.EspadaPiedra => "Espada de piedra",
        (ushort)ItemId.HachaMadera => "Hacha de madera",
        (ushort)ItemId.HachaPiedra => "Hacha de piedra",
        (ushort)ItemId.PalaMadera => "Pala de madera",
        (ushort)ItemId.PalaPiedra => "Pala de piedra",
        (ushort)ItemId.AzadaMadera => "Azada de madera",
        (ushort)ItemId.AzadaPiedra => "Azada de piedra",
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
        (ushort)ItemId.Palo => (150, 110, 60),
        (ushort)ItemId.CarneCrudaCerdo or (ushort)ItemId.CarneCrudaVaca or (ushort)ItemId.CarneCrudaOveja => (210, 90, 80),
        (ushort)ItemId.CarneCocinadaCerdo or (ushort)ItemId.CarneCocinadaVaca or (ushort)ItemId.CarneCocinadaOveja => (150, 80, 40),
        (ushort)ItemId.CarnePodrida => (120, 140, 60),
        (ushort)ItemId.Polvora => (140, 140, 140),
        (ushort)ItemId.Cuero => (150, 105, 60),
        (ushort)ItemId.Hueso => (230, 225, 210),
        (ushort)ItemId.LingoteHierro => (210, 210, 220),
        (ushort)ItemId.Zanahoria => (230, 140, 40),
        (ushort)ItemId.PicoMadera or (ushort)ItemId.HachaMadera or (ushort)ItemId.PalaMadera or (ushort)ItemId.AzadaMadera or (ushort)ItemId.EspadaMadera => (150, 105, 60),
        (ushort)ItemId.PicoPiedra or (ushort)ItemId.HachaPiedra or (ushort)ItemId.PalaPiedra or (ushort)ItemId.AzadaPiedra or (ushort)ItemId.EspadaPiedra => (150, 150, 155),
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
