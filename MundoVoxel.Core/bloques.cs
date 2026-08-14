namespace MundoVoxel.Core;

/// <summary>Definición de todos los bloques del juego.</summary>
public static class Bloques
{
    public const ushort Aire = 0;
    public const ushort Tierra = 1;
    public const ushort Piedra = 2;
    public const ushort Madera = 3;
    public const ushort Arena = 4;
    public const ushort Agua = 5;
    public const ushort Cesped = 6;
    public const ushort Grava = 7;
    public const ushort Ladrillo = 8;
    public const ushort Hoja = 9;
    public const ushort Cristal = 10;
    public const ushort Lecho = 11;

    public sealed record InfoBloque(string ClaveLang, bool Solido, bool Transparente, bool Liquido);

    /// <summary>ClaveLang es la clave usada en el archivo .lang para el nombre del bloque.</summary>
    public static readonly InfoBloque[] Info =
    {
        new("bloque.aire",    false, true,  false), // 0
        new("bloque.tierra",  true,  false, false), // 1
        new("bloque.piedra",  true,  false, false), // 2
        new("bloque.madera",  true,  false, false), // 3
        new("bloque.arena",   true,  false, false), // 4
        new("bloque.agua",    false, true,  true ), // 5
        new("bloque.cesped",  true,  false, false), // 6
        new("bloque.grava",   true,  false, false), // 7
        new("bloque.ladrillo",true,  false, false), // 8
        new("bloque.hoja",    true,  true,  false), // 9
        new("bloque.cristal", true,  true,  false), // 10
        new("bloque.lecho",   true,  false, false), // 11
    };

    public static bool EsSolido(ushort b) => b < Info.Length && Info[b].Solido;
    public static bool EsTransparente(ushort b) => b < Info.Length && Info[b].Transparente;
    public static bool EsLiquido(ushort b) => b < Info.Length && Info[b].Liquido;
    public static bool EsColocable(ushort b) => b > Aire && b != Lecho && b != Agua;
    public static bool EsRompible(ushort b) => b != Aire && b != Lecho;
}
