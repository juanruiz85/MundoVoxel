using MundoVoxel.Core;

namespace MundoVoxel.Client.Servicios;

/// <summary>
/// Reconexión automática en plena partida: si se pierde la conexión reintenta
/// al mismo servidor con retroceso exponencial (2 s, 4 s, 8 s…) hasta un tope
/// de 5 intentos. Al reconectar reenvía <see cref="Hola"/>, espera la
/// <see cref="ListaMundos"/> que el servidor manda tras el saludo y vuelve a
/// unirse al mismo mundo por el mismo camino que PaginaMundos (recordando el
/// Id del mundo y el PIN si era privado). No cambia nada del protocolo.
/// </summary>
public sealed class ServicioReconexion
{
    public const int MaxIntentos = 5;
    const int EsperaInicialMs = 2000;
    const int TimeoutListaMs = 8000;
    const int TimeoutUnidoMs = 20000;   // descargar el mundo comprimido puede tardar

    readonly ServicioRed _red;
    readonly ServicioIdioma _idioma;
    readonly object _cerrojo = new();
    CancellationTokenSource? _cts;

    public bool EnCurso { get; private set; }

    /// <summary>Eventos para la interfaz (siempre lanzados en el hilo principal,
    /// los manejadores pueden tocar la UI directamente).</summary>
    public event Action<int, int>? AlIniciar;      // (intento, tope): mostrar panel
    public event Action<int, int>? AlIntento;      // actualizar "intento X de Y"
    public event Action<Unido>? AlReconectado;     // volver a entrar al mundo
    public event Action<string>? AlFallo;          // agotado o mundo ya no existe: mensaje listo
    public event Action<string>? AlCancelar;       // el usuario canceló: mensaje listo

    public ServicioReconexion(ServicioRed red, ServicioIdioma idioma)
    {
        _red = red;
        _idioma = idioma;
    }

    /// <summary>Empieza el bucle de reconexión hacia el servidor de la sesión
    /// actual (EstadoSesion), recordando el mundo y su PIN. No hace nada si ya
    /// hay una reconexión en curso.</summary>
    public void Iniciar(string mundoId, string? pin)
    {
        lock (_cerrojo)
        {
            if (EnCurso) return;
            EnCurso = true;
            _cts = new CancellationTokenSource();
        }
        var token = _cts.Token;
        LanzarEnPrincipal(() => AlIniciar?.Invoke(1, MaxIntentos));
        _ = Task.Run(() => BucleAsync(mundoId, pin, token));
    }

    /// <summary>Cancela la reconexión en curso (botón Cancelar / tecla atrás).</summary>
    public void Cancelar()
    {
        try { _cts?.Cancel(); }
        catch (ObjectDisposedException) { } // el bucle acabó justo antes: nada que cancelar
    }

    async Task BucleAsync(string mundoId, string? pin, CancellationToken token)
    {
        try
        {
            var ip = EstadoSesion.Ip;
            var puerto = EstadoSesion.Puerto;
            int esperaMs = EsperaInicialMs;

            for (int intento = 1; intento <= MaxIntentos; intento++)
            {
                if (intento > 1) LanzarEnPrincipal(() => AlIntento?.Invoke(intento, MaxIntentos));

                // Retroceso exponencial ANTES de cada intento: 2 s, 4 s, 8 s…
                try { await Task.Delay(esperaMs, token); }
                catch (OperationCanceledException) { return; }
                esperaMs *= 2;
                if (token.IsCancellationRequested) return;

                bool ok = await Task.Run(() => _red.Conectar(ip, puerto), token);
                if (token.IsCancellationRequested) { AlCancelarConexion(); return; }
                if (!ok) continue;

                // Saludo: el servidor responde Bienvenido y luego ListaMundos.
                _red.Enviar(new Hola { Nombre = EstadoSesion.Nombre, Version = "1.0" });
                var lista = await EsperarMensaje(TimeoutListaMs, token,
                    m => m is ListaMundos or ErrorServidor);
                if (token.IsCancellationRequested) { AlCancelarConexion(); return; }
                if (lista is ErrorServidor temprano) { Fallo(TextoError(temprano)); return; }
                if (lista is not ListaMundos lm) continue;   // sin respuesta a tiempo: reintentar

                // El mundo ya no está en la lista: avisa y no insiste.
                if (lm.Mundos.All(x => x.Id != mundoId))
                {
                    Fallo(_idioma.O("error.no_existe"));
                    return;
                }

                // Reentrada por el mismo camino que PaginaMundos -> Unirse.
                _red.Enviar(new Unirse { Id = mundoId, Pin = pin });
                var respuesta = await EsperarMensaje(TimeoutUnidoMs, token,
                    m => m is Unido or ErrorServidor);
                if (token.IsCancellationRequested) { AlCancelarConexion(); return; }
                if (respuesta is ErrorServidor er) { Fallo(TextoError(er)); return; }
                if (respuesta is Unido unido)
                {
                    var (datosTrozos, errorTrozos) = await EsperarTrozosMundo(token);
                    if (token.IsCancellationRequested) { AlCancelarConexion(); return; }
                    if (errorTrozos != null) { Fallo(errorTrozos); return; }
                    if (datosTrozos == null) { _red.Desconectar(); continue; }
                    unido.MundoComprimido = datosTrozos;
                    Fin();
                    LanzarEnPrincipal(() => AlReconectado?.Invoke(unido));
                    return;
                }
                // Sin Unido a tiempo: la próxima pasada desconecta y reintenta.
                _red.Desconectar();
            }

            Fallo(_idioma.O("juego.reconectando_agotado", MaxIntentos));
        }
        catch (Exception ex)
        {
            Diag.Log("ServicioReconexion: " + ex);
            Fallo(_idioma.O("error.desconectado"));
        }
        finally
        {
            Fin();
        }
    }

    /// <summary>Consulta la cola hasta que llega un mensaje relevante o se agota
    /// el tiempo (la interfaz no consume mientras hay reconexión en curso;
    /// Bienvenido y otros mensajes sueltos se descartan aquí).</summary>
    /// <summary>Reensambla el mundo troceado tras la reconexion: consume los
    /// MundoChunk hasta completar el Total y devuelve los bytes unidos.</summary>
    async Task<(byte[]? Datos, string? Error)> EsperarTrozosMundo(CancellationToken token)
    {
        var trozos = new List<byte[]>();
        var fin = DateTime.UtcNow.AddMilliseconds(TimeoutUnidoMs);
        while (DateTime.UtcNow < fin)
        {
            var m = await EsperarMensaje(TimeoutListaMs, token, x => x is MundoChunk or ErrorServidor);
            if (token.IsCancellationRequested) return (null, null);
            if (m is ErrorServidor er) return (null, TextoError(er));
            if (m is not MundoChunk mc) return (null, null);
            while (trozos.Count < mc.Indice) trozos.Add(Array.Empty<byte>());
            trozos.Add(mc.Datos);
            if (mc.Total > 0 && trozos.Count >= mc.Total)
                return (trozos.SelectMany(t => t).ToArray(), null);
        }
        return (null, null);
    }

    async Task<Mensaje?> EsperarMensaje(int timeoutMs, CancellationToken token, Func<Mensaje, bool> relevante)
    {
        var fin = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < fin)
        {
            if (_red.Obtener() is Mensaje m)
            {
                if (relevante(m)) return m;
                continue;
            }
            try { await Task.Delay(50, token); }
            catch (OperationCanceledException) { return null; }
        }
        return null;
    }

    void AlCancelarConexion()
    {
        // Cancelar con la conexión ya establecida: cerrar para no aterrizar de
        // sorpresa en la lista de mundos tras haber cancelado.
        if (_red.Conectado) _red.Desconectar();
    }

    void Fallo(string mensaje)
    {
        Fin();
        LanzarEnPrincipal(() => AlFallo?.Invoke(mensaje));
    }

    void Fin()
    {
        lock (_cerrojo)
        {
            EnCurso = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    string TextoError(ErrorServidor er)
    {
        var clave = "error." + er.Codigo.ToLowerInvariant();
        return _idioma.Lang.Contiene(clave) ? _idioma.O(clave) : er.Mensaje;
    }

    static void LanzarEnPrincipal(Action accion)
    {
        if (MainThread.IsMainThread) accion();
        else MainThread.BeginInvokeOnMainThread(accion);
    }
}
