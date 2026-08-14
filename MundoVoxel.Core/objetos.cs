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

/// <summary>Receta de crafteo o cocina: ingredientes → salida.</summary>
public sealed record Receta(Ingrediente[] Ingredientes, ushort Salida, int SalidaCantidad, string Nombre);

/// <summary>Tablas de ítems, recetas (estilo Minecraft), botín de mobs y nombres.</summary>
public static class Objetos
{
    public static bool EsBloque(ushort material) => material < 1000;

    static Ingrediente Ing(ushort m, int c) => new(m, c);

    /// <summary>
    /// Recetas de crafteo con las combinaciones clásicas de Minecraft (Indev),
    /// adaptadas a los materiales disponibles.
    /// </summary>
    public static readonly Receta[] RecetasCrafteo =
    {
        // Madera -> tablones (1 tronco = 4 tablones)
        new(new[]{ Ing(Bloques.Madera, 1) }, Bloques.Tablones, 4, "Madera -> 4 tablones"),
        // Palos (2 tablones en vertical = 4 palos)
        new(new[]{ Ing(Bloques.Tablones, 2) }, (ushort)ItemId.Palo, 4, "2 tablones -> 4 palos"),
        // Mesa de trabajo (2x2 tablones)
        new(new[]{ Ing(Bloques.Tablones, 4) }, Bloques.Mesa, 1, "4 tablones -> mesa de trabajo"),
        // Horno (8 piedra en anillo)
        new(new[]{ Ing(Bloques.Piedra, 8) }, Bloques.Horno, 1, "8 piedra -> horno"),
        // Arenisca (2x2 arena)
        new(new[]{ Ing(Bloques.Arena, 4) }, Bloques.Arenisca, 1, "4 arena -> arenisca"),
        // --- Herramientas de madera ---
        new(new[]{ Ing(Bloques.Tablones, 3), Ing((ushort)ItemId.Palo, 2) }, (ushort)ItemId.PicoMadera, 1, "Pico de madera (3 tablones + 2 palos)"),
        new(new[]{ Ing(Bloques.Tablones, 2), Ing((ushort)ItemId.Palo, 1) }, (ushort)ItemId.EspadaMadera, 1, "Espada de madera (2 tablones + 1 palo)"),
        new(new[]{ Ing(Bloques.Tablones, 3), Ing((ushort)ItemId.Palo, 2) }, (ushort)ItemId.HachaMadera, 1, "Hacha de madera (3 tablones + 2 palos)"),
        new(new[]{ Ing(Bloques.Tablones, 1), Ing((ushort)ItemId.Palo, 2) }, (ushort)ItemId.PalaMadera, 1, "Pala de madera (1 tablón + 2 palos)"),
        new(new[]{ Ing(Bloques.Tablones, 2), Ing((ushort)ItemId.Palo, 2) }, (ushort)ItemId.AzadaMadera, 1, "Azada de madera (2 tablones + 2 palos)"),
        // --- Herramientas de piedra ---
        new(new[]{ Ing(Bloques.Piedra, 3), Ing((ushort)ItemId.Palo, 2) }, (ushort)ItemId.PicoPiedra, 1, "Pico de piedra (3 piedra + 2 palos)"),
        new(new[]{ Ing(Bloques.Piedra, 2), Ing((ushort)ItemId.Palo, 1) }, (ushort)ItemId.EspadaPiedra, 1, "Espada de piedra (2 piedra + 1 palo)"),
        new(new[]{ Ing(Bloques.Piedra, 3), Ing((ushort)ItemId.Palo, 2) }, (ushort)ItemId.HachaPiedra, 1, "Hacha de piedra (3 piedra + 2 palos)"),
        new(new[]{ Ing(Bloques.Piedra, 1), Ing((ushort)ItemId.Palo, 2) }, (ushort)ItemId.PalaPiedra, 1, "Pala de piedra (1 piedra + 2 palos)"),
        new(new[]{ Ing(Bloques.Piedra, 2), Ing((ushort)ItemId.Palo, 2) }, (ushort)ItemId.AzadaPiedra, 1, "Azada de piedra (2 piedra + 2 palos)"),
    };

    /// <summary>Cocina (fundición): carne cruda → cocinada.</summary>
    public static readonly Receta[] RecetasCocina =
    {
        new(new[]{ Ing((ushort)ItemId.CarneCrudaCerdo, 1) }, (ushort)ItemId.CarneCocinadaCerdo, 1, "Cerdo crudo -> cerdo cocinado"),
        new(new[]{ Ing((ushort)ItemId.CarneCrudaVaca, 1) }, (ushort)ItemId.CarneCocinadaVaca, 1, "Vaca cruda -> vaca cocinada"),
        new(new[]{ Ing((ushort)ItemId.CarneCrudaOveja, 1) }, (ushort)ItemId.CarneCocinadaOveja, 1, "Oveja cruda -> oveja cocinada"),
    };

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
