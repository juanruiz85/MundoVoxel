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
            s.CargarMundos(); // mundos guardados de sesiones anteriores
            s.Iniciar();
            if (s.EnEjecucion) _servidor = s;
        }
    }

    /// <summary>Guarda todos los mundos del servidor local a disco. Se llama al
    /// cerrar o suspender la app (App.OnSleep).</summary>
    public static void GuardarMundos()
    {
        lock (Cer) { _servidor?.GuardarMundos(); }
    }
}
