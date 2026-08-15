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

/// <summary>Datos estáticos de cada mob: tamaño, hostilidad, velocidad, radio de agresión y daño.</summary>
public sealed record InfoMob(float Ancho, float Alto, bool Hostil, float Velocidad, float AreaAgresion, float Danio);

public static partial class MobsInfo
{
    static readonly Dictionary<TipoMob, InfoMob> _overrides = new();

    /// <summary>
    /// Datos efectivos (con overrides de configuracion aplicados).
    /// El servidor puede cargar `mobs.config.json` para ajustar velocidad,
    /// radio de agresion o daño de cada mob sin recompilar.
    /// </summary>
    public static InfoMob Datos(TipoMob t) => _overrides.TryGetValue(t, out var o) ? o : Base(t);

    /// <summary>Aplica overrides por tipo (los campos omitidos mantienen el valor base).</summary>
    public static void AplicarConfig(IReadOnlyDictionary<TipoMob, InfoMob> cfg)
    {
        _overrides.Clear();
        if (cfg == null) return;
        foreach (var (tipo, info) in cfg)
        {
            var b = Base(tipo);
            _overrides[tipo] = new InfoMob(
                info.Ancho > 0 ? info.Ancho : b.Ancho,
                info.Alto > 0 ? info.Alto : b.Alto,
                info.Hostil,
                info.Velocidad > 0 ? info.Velocidad : b.Velocidad,
                info.AreaAgresion >= 0 ? info.AreaAgresion : b.AreaAgresion,
                info.Danio > 0 ? info.Danio : b.Danio);
        }
    }

    static InfoMob Base(TipoMob t) => t switch
    {
        TipoMob.Cerdo => new(1.0f, 1.0f, false, 1.4f, 0f, 0f),
        TipoMob.Vaca => new(1.2f, 1.3f, false, 1.2f, 0f, 0f),
        TipoMob.Oveja => new(1.2f, 1.2f, false, 1.2f, 0f, 0f),
        TipoMob.Zombi => new(0.8f, 1.9f, true, 1.0f, 11f, 3f),
        TipoMob.Creeper => new(0.8f, 1.7f, true, 1.0f, 11f, 6f),
        _ => new(0.8f, 1.9f, true, 1.0f, 11f, 4f),
    };

    /// <summary>Vida maxima de un mob segun su tipo (hostiles aguantan mas).</summary>
    public static int SaludMaxima(TipoMob t) => Datos(t).Hostil ? 20 : 10;
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
