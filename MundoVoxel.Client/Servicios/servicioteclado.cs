namespace MundoVoxel.Client.Servicios;

/// <summary>
/// Estado de las teclas del teclado. En Windows se enlaza al contenido de la ventana
/// (KeyDown/KeyUp); en Android los botones táctiles simulan pulsaciones con códigos propios.
/// Los códigos son enteros portables (ver Juego.Teclas).
/// </summary>
public sealed class ServicioTeclado
{
    readonly HashSet<int> _pulsadas = new();
    static readonly HashSet<object> _vinculados = new();

    public bool EstaPulsada(int codigo) => _pulsadas.Contains(codigo);
    public event Action<int>? AlPulsar;
    public event Action<int>? AlSoltar;

#if WINDOWS
    public void Vincular(Microsoft.UI.Xaml.UIElement elemento)
    {
        lock (_vinculados)
        {
            if (!_vinculados.Add(elemento)) return; // una sola vez por ventana
        }
        elemento.KeyDown += (_, e) =>
        {
            if (_pulsadas.Add((int)e.Key)) AlPulsar?.Invoke((int)e.Key);
        };
        elemento.KeyUp += (_, e) =>
        {
            _pulsadas.Remove((int)e.Key);
            AlSoltar?.Invoke((int)e.Key);
        };
    }
#endif

    public void SimularPulsacion(int codigo)
    {
        if (_pulsadas.Add(codigo)) AlPulsar?.Invoke(codigo);
    }

    public void SimularSoltar(int codigo)
    {
        _pulsadas.Remove(codigo);
        AlSoltar?.Invoke(codigo);
    }
}
