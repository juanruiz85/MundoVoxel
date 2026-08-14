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
}

/// <summary>Un hueco del inventario: material (bloque o ítem) + cantidad.</summary>
public sealed record SlotInventario(ushort Material, int Cantidad);

/// <summary>Receta de crafteo o cocina (sin forma): entrada → salida.</summary>
public sealed record Receta(ushort Entrada, int EntradaCantidad, ushort Salida, int SalidaCantidad);

/// <summary>Tablas de ítems, recetas, botín de mobs y nombres (adaptación del JS).</summary>
public static class Objetos
{
    public static bool EsBloque(ushort material) => material < 1000;

    /// <summary>Crafteo: madera → tablones, tablones → palos/horno.</summary>
    public static readonly Receta[] RecetasCrafteo =
    {
        new(Bloques.Madera, 1, Bloques.Tablones, 4),
        new(Bloques.Tablones, 2, (ushort)ItemId.Palo, 4),
        new(Bloques.Tablones, 4, Bloques.Horno, 1),
    };

    /// <summary>Cocina (fundición): carne cruda → cocinada.</summary>
    public static readonly Receta[] RecetasCocina =
    {
        new((ushort)ItemId.CarneCrudaCerdo, 1, (ushort)ItemId.CarneCocinadaCerdo, 1),
        new((ushort)ItemId.CarneCrudaVaca, 1, (ushort)ItemId.CarneCocinadaVaca, 1),
        new((ushort)ItemId.CarneCrudaOveja, 1, (ushort)ItemId.CarneCocinadaOveja, 1),
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
        (ushort)ItemId.Palo => (150, 110, 60),
        (ushort)ItemId.CarneCrudaCerdo or (ushort)ItemId.CarneCrudaVaca or (ushort)ItemId.CarneCrudaOveja => (210, 90, 80),
        (ushort)ItemId.CarneCocinadaCerdo or (ushort)ItemId.CarneCocinadaVaca or (ushort)ItemId.CarneCocinadaOveja => (150, 80, 40),
        (ushort)ItemId.CarnePodrida => (120, 140, 60),
        (ushort)ItemId.Polvora => (140, 140, 140),
        (ushort)ItemId.Cuero => (150, 105, 60),
        (ushort)ItemId.Hueso => (230, 225, 210),
        (ushort)ItemId.LingoteHierro => (210, 210, 220),
        (ushort)ItemId.Zanahoria => (230, 140, 40),
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
