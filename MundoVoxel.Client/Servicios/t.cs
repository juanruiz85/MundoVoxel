namespace MundoVoxel.Client.Servicios;

/// <summary>
/// Textos estáticos para usar en plantillas XAML (x:Static).
/// Se cargan una vez al iniciar la aplicación desde el archivo .lang.
/// </summary>
public static class T
{
    public static string Unirse = "Unirse";
    public static string Borrar = "Borrar";
    public static string Crear = "Crear mundo";
    public static string Actualizar = "Actualizar";
    public static string Desconectar = "Desconectar";
    public static string Reanudar = "Reanudar";
    public static string Romper = "Romper";
    public static string Colocar = "Colocar";
    public static string Saltar = "⤒";
    public static string Volar = "Volar";
    public static string Chat = "Chat";
    public static string Menu = "☰";

    public static void Cargar(ServicioIdioma i)
    {
        Unirse = i.O("mundos.unirse");
        Borrar = i.O("mundos.borrar");
        Crear = i.O("mundos.crear");
        Actualizar = i.O("mundos.refrescar");
        Desconectar = i.O("mundos.desconectar");
        Reanudar = i.O("pausa.reanudar");
        Romper = i.O("boton.romper");
        Colocar = i.O("boton.colocar");
        Saltar = i.O("boton.saltar");
        Volar = i.O("boton.volar");
        Chat = i.O("boton.chat");
        Menu = i.O("boton.menu");
    }
}
