using MundoVoxel.Core;

namespace MundoVoxel.Client.Servicios;

/// <summary>Servidor local incrustado para jugar solo sin instalar el servidor aparte.</summary>
public static class ServidorLocal
{
    static GameServer? _servidor;
    static readonly object Cer = new();

    public static void Asegurar(int puerto)
    {
        lock (Cer)
        {
            if (_servidor != null) return;
            var s = new GameServer(puerto, "MundoVoxel local");
            s.Iniciar();
            if (s.EnEjecucion) _servidor = s;
        }
    }
}
