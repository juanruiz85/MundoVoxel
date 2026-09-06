namespace MundoVoxel.Client.Servicios;

/// <summary>Datos de la sesión actual (nombre e IP elegidos en el menú).</summary>
public static class EstadoSesion
{
    public static string Nombre { get; set; } = "Jugador";
    public static string Ip { get; set; } = "127.0.0.1";
    public static int Puerto { get; set; } = 25575;
    /// <summary>Mensaje pendiente para mostrar en el menú al volver (p. ej.
    /// reconexión agotada); se muestra una vez y se limpia.</summary>
    public static string MensajeMenu { get; set; } = "";
}

/// <summary>Información de un mundo recibido del servidor (para entrar en la partida).</summary>
public sealed class DatosMundo
{
    public required string Id { get; init; }
    public required string Nombre { get; init; }
    public required string Dueno { get; init; }
    public required int IdDueno { get; init; }
    public required byte[] MundoComprimido { get; init; }
    public required float Ax { get; init; }
    public required float Ay { get; init; }
    public required float Az { get; init; }
    /// <summary>Sensibilidad del ratA3n guardada (1 = normal).</summary>
    public float Sensibilidad { get; init; } = 1f;
    /// <summary>Clave usada para entrar (solo mundos privados; la recuerda en
    /// memoria para poder reconectar automáticamente a este mundo).</summary>
    public string? PinUsado { get; init; }
}
