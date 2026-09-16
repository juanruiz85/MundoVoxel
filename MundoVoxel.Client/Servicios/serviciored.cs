using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Servicios;

/// <summary>
/// Cliente de red: conecta por TCP, lee mensajes en un hilo en segundo plano
/// y los deja en una cola que la interfaz consume con un temporizador.
/// </summary>
public sealed class ServicioRed : IDisposable
{
    TcpClient? _tcp;
    Stream? _flujo;
    Thread? _lector;
    readonly ConcurrentQueue<Mensaje> _recibidos = new();
    readonly object _cerrojo = new();
    bool _saludado;   // ya llego el primer Bienvenido (los errores previos son del saludo)

    public int MiId { get; private set; } = -1;
    public string NombreServidor { get; private set; } = "";
    public string IpConectada { get; private set; } = "";
    public bool Conectado => _tcp?.Connected == true && _flujo != null;

    public event Action? AlConectar;
    public event Action? AlDesconectar;

    /// <summary>Conexion TLS (opt-in): el certificado del servidor es autofirmado,
    /// asi que se valida por su huella SHA-256 recordada por servidor
    /// (trust-on-first-use). La primera vez se guarda y se avisa; si despues
    /// CAMBIA, se rechaza la conexion (posible interceptacion) en vez de aceptarla
    /// en silencio.</summary>
    public event Action<string>? AlHuellaNueva;
    public event Action<string>? AlHuellaCambiada;

    /// <summary>Un ErrorServidor que llega ANTES del primer Bienvenido (por ejemplo,
    /// si el servidor pide clave): el menu lo muestra sin esperar al temporizador.
    /// El mensaje tambien queda en la cola, porque la reconexion automatica lo espera ahi.</summary>
    public event Action<ErrorServidor>? AlErrorSaludo;

    public bool Conectar(string ip, int puerto, int timeoutMs = 6000, bool cifrado = false)
    {
        Desconectar();
        var tcp = new TcpClient { NoDelay = true };
        var tarea = tcp.ConnectAsync(ip, puerto);
        if (!tarea.Wait(timeoutMs) || !tcp.Connected)
        {
            tcp.Dispose();
            return false;
        }
        Stream flujo = tcp.GetStream();
        if (cifrado)
        {
            string huellaVista = "";
            var ssl = new SslStream(flujo, false, (_, certificado, _, _) =>
            {
                if (certificado == null) return false;
                huellaVista = Tls.Huella(certificado.GetRawCertData());
                var esperada = Preferences.Get(Tls.ClaveHuella(ip, puerto), "");
                if (!Tls.HuellaAceptable(esperada, huellaVista))
                {
                    AlHuellaCambiada?.Invoke(huellaVista);   // posible interceptacion
                    return false;
                }
                if (esperada.Length == 0)
                {
                    Preferences.Set(Tls.ClaveHuella(ip, puerto), huellaVista);
                    AlHuellaNueva?.Invoke(huellaVista);
                }
                return true;
            });
            try
            {
                var handshake = ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost", // el certificado cubre localhost/equipo/loopback
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                });
                if (!handshake.Wait(timeoutMs))
                {
                    ssl.Dispose();
                    tcp.Close();
                    return false;
                }
            }
            catch
            {
                try { ssl.Dispose(); } catch { }
                try { tcp.Close(); } catch { }
                return false;
            }
            flujo = ssl;
        }
        _tcp = tcp;
        _flujo = flujo;
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
                if (m is ErrorServidor errSaludo && !_saludado) AlErrorSaludo?.Invoke(errSaludo);
                _recibidos.Enqueue(m);
                if (m is Bienvenido)
                {
                    _saludado = true;
                    AlConectar?.Invoke();
                }
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
        _saludado = false;
    }

    public void Dispose() => Desconectar();
}
