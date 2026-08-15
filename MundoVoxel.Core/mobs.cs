using System.Numerics;

namespace MundoVoxel.Core;

/// <summary>Tipos de mob del juego (adaptación de los 6 mobs de la referencia MinecraftJS).</summary>
public enum TipoMob : byte
{
    Cerdo = 0,
    Vaca = 1,
    Oveja = 2,
    Zombi = 3,
    Creeper = 4,
    EsqueletoWither = 5,
}

/// <summary>Datos estáticos de cada mob: tamaño, hostilidad, velocidad y radio de agresión.</summary>
public sealed record InfoMob(float Ancho, float Alto, bool Hostil, float Velocidad, float AreaAgresion);

public static partial class MobsInfo
{
    public static InfoMob Datos(TipoMob t) => t switch
    {
        TipoMob.Cerdo => new(1.0f, 1.0f, false, 1.4f, 0f),
        TipoMob.Vaca => new(1.2f, 1.3f, false, 1.2f, 0f),
        TipoMob.Oveja => new(1.2f, 1.2f, false, 1.2f, 0f),
        TipoMob.Zombi => new(0.8f, 1.9f, true, 1.0f, 11f),
        TipoMob.Creeper => new(0.8f, 1.7f, true, 1.0f, 11f),
        _ => new(0.8f, 1.9f, true, 1.0f, 11f),
    };
}

/// <summary>Estado simulado de un mob (autoritativo en el servidor).</summary>
public sealed class Mob
{
    public int Id;
    public TipoMob Tipo;
    public float Px, Py, Pz;
    public float Ry;
    public float VelX, VelZ;
    public float TiempoCambio;
    public float Salud;
}
