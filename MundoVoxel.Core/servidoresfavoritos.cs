using System.Text.Json;

namespace MundoVoxel.Core;

/// <summary>Un servidor favorito guardado por el cliente (alias + dirección).</summary>
public sealed class ServidorFavorito
{
    public string Alias { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Puerto { get; set; } = 25575;
}

/// <summary>
/// Lista de servidores favoritos del cliente, persistida en JSON en
/// %LOCALAPPDATA%\MundoVoxel\servidores.json (el servidor no la usa).
/// El formato sigue el patrón de la carpeta de mundos del GameServer.
/// </summary>
public static class ServidoresFavoritos
{
    /// <summary>Tope de favoritos guardados (evita que la lista crezca sin límite).</summary>
    public const int Maximo = 20;

    /// <summary>Ruta del archivo de favoritos (AppData local del usuario).</summary>
    public static string RutaArchivo => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MundoVoxel", "servidores.json");

    static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Carga los favoritos del archivo por defecto (lista vacía si no existe o está dañado).</summary>
    public static List<ServidorFavorito> Cargar() => Cargar(RutaArchivo);

    public static List<ServidorFavorito> Cargar(string ruta)
    {
        try
        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return new List<ServidorFavorito>();
            var lista = JsonSerializer.Deserialize<List<ServidorFavorito>>(File.ReadAllText(ruta), Opciones);
            if (lista == null) return new List<ServidorFavorito>();
            // Descartar entradas incompletas y recortar al tope
            return lista.Where(f => !string.IsNullOrWhiteSpace(f.Ip)).Take(Maximo).ToList();
        }
        catch
        {
            return new List<ServidorFavorito>(); // archivo dañado: empezar de cero, nunca romper el menú
        }
    }

    /// <summary>Guarda la lista de favoritos (crea la carpeta si falta).</summary>
    public static void Guardar(List<ServidorFavorito> lista) => Guardar(RutaArchivo, lista);

    public static void Guardar(string ruta, List<ServidorFavorito> lista)
    {
        var dir = Path.GetDirectoryName(ruta);
        try
        {
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ruta, JsonSerializer.Serialize(lista.Take(Maximo).ToList(), Opciones));
        }
        catch
        {
            // sin permiso de disco: los favoritos no se guardan, pero el juego sigue
        }
    }

    /// <summary>Añade (o actualiza) un favorito en el archivo por defecto: si ya
    /// existe la misma ip+puerto se actualiza el alias en su sitio; el nuevo
    /// servidor se guarda al final.</summary>
    public static void Agregar(ServidorFavorito favorito) => AgregarRuta(RutaArchivo, favorito);

    public static void AgregarRuta(string ruta, ServidorFavorito favorito)
    {
        var lista = Cargar(ruta);
        var existente = lista.FirstOrDefault(f => MismaDireccion(f, favorito));
        if (existente != null)
            existente.Alias = favorito.Alias;
        else
            lista.Add(favorito);
        Guardar(ruta, lista);
    }

    /// <summary>Quita el favorito cuya dirección coincida (si existe) del archivo por defecto.</summary>
    public static void Quitar(string ip, int puerto) => QuitarRuta(RutaArchivo, ip, puerto);

    public static void QuitarRuta(string ruta, string ip, int puerto)
    {
        var lista = Cargar(ruta);
        int antes = lista.Count;
        lista.RemoveAll(f => MismaDireccion(f, new ServidorFavorito { Ip = ip, Puerto = puerto }));
        if (lista.Count != antes) Guardar(ruta, lista);
    }

    static bool MismaDireccion(ServidorFavorito a, ServidorFavorito b)
        => a.Ip.Trim().Equals(b.Ip.Trim(), StringComparison.OrdinalIgnoreCase) && a.Puerto == b.Puerto;
}
