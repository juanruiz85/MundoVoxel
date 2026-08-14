using System.Collections.Concurrent;
using System.Net.Sockets;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Servicios;

/// <summary>
/// Cliente de red: conecta por TCP, lee mensajes en un hilo en segundo plano
/// y los deja en una cola que la interfaz consume con un temporizador.
/// </summary>
public sealed class ServicioRed : IDisposable
{
    TcpClient? _tcp;
    NetworkStream? _flujo;
    Thread? _lector;
    readonly ConcurrentQueue<Mensaje> _recibidos = new();
    readonly object _cerrojo = new();

    public int MiId { get; private set; } = -1;
    public string NombreServidor { get; private set; } = "";
    public string IpConectada { get; private set; } = "";
    public bool Conectado => _tcp?.Connected == true && _flujo != null;

    public event Action? AlConectar;
    public event Action? AlDesconectar;

    public bool Conectar(string ip, int puerto, int timeoutMs = 6000)
    {
        Desconectar();
        var tcp = new TcpClient { NoDelay = true };
        var tarea = tcp.ConnectAsync(ip, puerto);
        if (!tarea.Wait(timeoutMs) || !tcp.Connected)
        {
            tcp.Dispose();
            return false;
        }
        _tcp = tcp;
        _flujo = tcp.GetStream();
        IpConectada = ip;
        _lector = new Thread(LoopLectura) { IsBackground = true };
        _lector.Start();
        return true;
    }

    void LoopLectura()
    {
        try
        {
            while (_flujo != null)
            {
                var m = Frames.LeerAsync(_flujo, CancellationToken.None).GetAwaiter().GetResult();
                if (m == null) break;
                if (m is Bienvenido b)
                {
                    MiId = b.IdJugador;
                    NombreServidor = b.NombreServidor;
                }
                _recibidos.Enqueue(m);
                if (m is Bienvenido) AlConectar?.Invoke();
            }
        }
        catch
        {
        }
        finally
        {
            AlDesconectar?.Invoke();
        }
    }

    public bool Enviar(Mensaje m)
    {
        if (_flujo == null) return false;
        try
        {
            var datos = Protocolo.Codificar(m);
            lock (_cerrojo) _flujo.Write(datos);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Extrae el siguiente mensaje recibido (o null si no hay).</summary>
    public Mensaje? Obtener() => _recibidos.TryDequeue(out var m) ? m : null;

    public void Desconectar()
    {
        try { _flujo?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }
        _flujo = null;
        _tcp = null;
        _lector = null;
    }

    public void Dispose() => Desconectar();
}
