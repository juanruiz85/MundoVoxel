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
    public static string Token = "Token";
    public static string Reanudar = "Reanudar";
    public static string Romper = "Romper";
    public static string Colocar = "Colocar";
    public static string Saltar = "⤒";
    public static string Volar = "Volar";
    public static string Chat = "Chat";
    public static string Menu = "☰";
    public static string Sensibilidad = "Sensibilidad ratón";
    public static string InvTitulo = "Inventario";
    public static string InvTuInventario = "Tu inventario";
    public static string InvCocina = "Cocina (horno)";
    public static string InvResultado = "Resultado";
    public static string InvCerrar = "Cerrar";
    public static string CofreTitulo = "Cofre";
    public static string CofreCerrar = "Cerrar cofre";
    public static string MuerteTitulo = "Has muerto";
    public static string Reaparecer = "Reaparecer";
    public static string App = "MundoVoxel";
    public static string Mundos = "Mundos";

    public static void Cargar(ServicioIdioma i)
    {
        Unirse = i.O("mundos.unirse");
        Borrar = i.O("mundos.borrar");
        Crear = i.O("mundos.crear");
        Actualizar = i.O("mundos.refrescar");
        Desconectar = i.O("mundos.desconectar");
        Token = i.O("mundos.token");
        Reanudar = i.O("pausa.reanudar");
        Romper = i.O("boton.romper");
        Colocar = i.O("boton.colocar");
        Saltar = i.O("boton.saltar");
        Volar = i.O("boton.volar");
        Chat = i.O("boton.chat");
        Menu = i.O("boton.menu");
        Sensibilidad = i.O("pausa.sensibilidad");
        InvTitulo = i.O("inv.titulo");
        InvTuInventario = i.O("inv.tu_inventario");
        InvCocina = i.O("inv.cocina");
        InvResultado = i.O("inv.resultado");
        InvCerrar = i.O("inv.cerrar");
        CofreTitulo = i.O("cofre.titulo");
        CofreCerrar = i.O("cofre.cerrar");
        MuerteTitulo = i.O("juego.muerte_titulo");
        Reaparecer = i.O("juego.reaparecer");
        App = i.O("app.titulo");
        Mundos = i.O("mundos.pagina");
    }
}
