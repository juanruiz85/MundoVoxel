namespace MundoVoxel.Core;

/// <summary>
/// Reensambla un mundo que llega por regiones (streaming por proximidad): el
/// servidor manda una cabecera (Unido) y despues cada region por separado, en
/// el orden que quiera y a medida que el jugador se mueve. Vive en Core para
/// poder probarlo sin MAUI.
/// </summary>
public sealed class MundoRemoto
{
    readonly bool[] _recibidas;

    public Mundo Mundo { get; }
    public int RegionesX { get; }
    public int RegionesZ { get; }
    public int Total { get; }
    public int Recibidas { get; private set; }
    public bool Completo => Recibidas >= Total;

    public MundoRemoto(int ancho, int alto, int profundo, int semilla)
    {
        if (ancho < 1 || alto < 1 || profundo < 1)
            throw new ArgumentOutOfRangeException(nameof(ancho), "Dimensiones de mundo invalidas.");
        Mundo = new Mundo(ancho, alto, profundo, semilla);
        RegionesX = Mundo.RegionesX;
        RegionesZ = Mundo.RegionesZ;
        Total = Mundo.TotalRegiones;
        _recibidas = new bool[Total];
    }


    int Indice(int rx, int rz) => rz * RegionesX + rx;
    bool Dentro(int rx, int rz) => rx >= 0 && rx < RegionesX && rz >= 0 && rz < RegionesZ;
    public bool Recibida(int rx, int rz) => Dentro(rx, rz) && _recibidas[Indice(rx, rz)];

    /// <summary>Guarda una region recibida y devuelve true si con esta el mundo
    /// ya esta completo. Una region repetida o fuera de rango se ignora sin
    /// error (el servidor reenvia regiones al moverse el jugador), pero una
    /// region corrupta lanza InvalidDataException y no cuenta como recibida.</summary>
    public bool Aplicar(int rx, int rz, byte[] datos)
    {
        if (!Dentro(rx, rz) || _recibidas[Indice(rx, rz)]) return Completo;
        Mundo.AplicarRegion(rx, rz, datos);
        _recibidas[Indice(rx, rz)] = true;
        Recibidas++;
        return Completo;
    }

    /// <summary>Region (rx, rz) de una posicion del mundo, en coordenadas de bloque.</summary>
    public static (int Rx, int Rz) RegionDe(float x, float z)
        => Mundo.RegionDe((int)MathF.Floor(x), (int)MathF.Floor(z));
}