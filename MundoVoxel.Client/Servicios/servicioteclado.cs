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
        // handledEventsToo: aunque un boton marcara la tecla como "procesada",
        // interceptamos SIEMPRE la barra espaciadora para quitarle el foco:
        // asi, aunque un boton tenga foco, la barra espaciadora nunca lo activa
        // (el boton solo dispara Click al soltar la tecla con foco).
        elemento.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent,
            new Microsoft.UI.Xaml.Input.KeyEventHandler((_, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Space)
                {
                    // Si el foco esta en un campo de texto (chat), dejar que escriba
                    var foco = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(elemento.XamlRoot);
                    if (foco is Microsoft.UI.Xaml.Controls.TextBox) { /* normal */ }
                    else
                    {
                        // Aunque un boton tenga foco, la barra espaciadora nunca lo
                        // activa: movemos el foco al contenedor raiz (el boton solo
                        // dispara Click al soltar la tecla CON foco, asi que al
                        // perderlo no se activa aunque se mantenga pulsada).
                        elemento.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                        e.Handled = true;
                    }
                }
                if (_pulsadas.Add((int)e.Key)) AlPulsar?.Invoke((int)e.Key);
            }), true);
        elemento.AddHandler(Microsoft.UI.Xaml.UIElement.KeyUpEvent,
            new Microsoft.UI.Xaml.Input.KeyEventHandler((_, e) =>
            {
                _pulsadas.Remove((int)e.Key);
                AlSoltar?.Invoke((int)e.Key);
            }), true);
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
