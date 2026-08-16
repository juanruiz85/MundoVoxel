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
    public const ushort Tablones = 12;
    public const ushort Horno = 13;
    public const ushort Mesa = 14;
    public const ushort Arenisca = 15;
    public const ushort Carbon = 16;
    public const ushort Hierro = 17;
    public const ushort Oro = 18;
    public const ushort Diamante = 19;
    public const ushort TierraLabrada = 20;
    public const ushort Trigo0 = 21;
    public const ushort Trigo1 = 22;
    public const ushort Trigo2 = 23;
    public const ushort Trigo3 = 24;
    public const ushort Planton = 25;
    public const ushort Tnt = 26;
    public const ushort Antorcha = 27;
    public const ushort Cobre = 28;
    public const ushort Cofre = 29;
    public const ushort Lava = 30;

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
        new("bloque.tablones", true,  false, false), // 12
        new("bloque.horno",    true,  false, false), // 13
        new("bloque.mesa",     true,  false, false), // 14
        new("bloque.arenisca", true,  false, false), // 15
        new("bloque.carbon",   true,  false, false), // 16
        new("bloque.hierro",   true,  false, false), // 17
        new("bloque.oro",      true,  false, false), // 18
        new("bloque.diamante", true,  false, false), // 19
        new("bloque.tierra_labrada", true, false, false), // 20
        new("bloque.trigo",    false, true,  false), // 21
        new("bloque.trigo",    false, true,  false), // 22
        new("bloque.trigo",    false, true,  false), // 23
        new("bloque.trigo",    false, true,  false), // 24
        new("bloque.planton",  false, true,  false), // 25
        new("bloque.tnt",      true,  false, false), // 26
        new("bloque.antorcha", false, true,  false), // 27
        new("bloque.cobre",    true,  false, false), // 28
        new("bloque.cofre",    true,  false, false), // 29
        new("bloque.lava",     false, true,  true ), // 30
    };

    public static bool EsSolido(ushort b) => b < Info.Length && Info[b].Solido;
    public static bool EsTransparente(ushort b) => b < Info.Length && Info[b].Transparente;
    public static bool EsLiquido(ushort b) => b < Info.Length && Info[b].Liquido;
    public static bool EsColocable(ushort b) => b > Aire && b != Lecho && b != Agua && b != Lava && !EsCultivo(b) && b != TierraLabrada && b != Planton;
    public static bool EsRompible(ushort b) => b != Aire && b != Lecho && b != Agua && b != Lava;
    public static bool EsMineral(ushort b) => b == Carbon || b == Hierro || b == Oro || b == Diamante || b == Cobre;
    public static bool EsCultivo(ushort b) => b >= Trigo0 && b <= Trigo3;
    public static bool EsTrigoMaduro(ushort b) => b == Trigo3;
}
