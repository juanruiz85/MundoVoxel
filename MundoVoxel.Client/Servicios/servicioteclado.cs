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
        // Interceptamos la barra espaciadora en el TUNEL (PreviewKeyDown), que se
        // ejecuta ANTES de que el control con foco reciba la tecla. Asi, aunque un
        // boton (menu, reanudar...) tenga el foco, el espacio nunca llega a el:
        // el boton no se "arma" y no dispara Click al soltar la tecla.
        // No se toca el foco (desenfocarlo lo dejaba en null y WinUI dejaba de
        // enrutar las teclas siguientes: Escape, T, WASD...).
        elemento.AddHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent,
            new Microsoft.UI.Xaml.Input.KeyEventHandler((_, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Space)
                {
                    // Si el foco esta en un campo de texto (chat), dejar que escriba
                    var foco = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(elemento.XamlRoot);
                    if (foco is not Microsoft.UI.Xaml.Controls.TextBox)
                        e.Handled = true;
                }
                if (_pulsadas.Add((int)e.Key)) AlPulsar?.Invoke((int)e.Key);
            }), true);
        elemento.AddHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyUpEvent,
            new Microsoft.UI.Xaml.Input.KeyEventHandler((_, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Space)
                {
                    var foco = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(elemento.XamlRoot);
                    if (foco is not Microsoft.UI.Xaml.Controls.TextBox)
                        e.Handled = true;
                }
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
