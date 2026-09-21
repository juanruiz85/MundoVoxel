using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MundoVoxel.Core;

/// <summary>
/// Cuentas por jugador de un servidor abierto a internet (opcional). Con ellas el
/// nombre del jugador deja de ser un dato que cualquiera puede escribir: es la base
/// para que nadie suplante a otro (hoy el inventario, las estadisticas y la
/// propiedad de un mundo van por nombre).
///
/// La clave NUNCA se guarda: se guarda su hash PBKDF2-SHA256 con sal aleatoria de
/// 16 bytes por cuenta, y la comparacion es en tiempo constante. El archivo
/// (cuentas.json) lleva solo nombre, sal, hash, iteraciones y fecha de alta.
/// </summary>
public sealed class Cuentas
{
    /// <summary>Iteraciones de PBKDF2. Alto a proposito: encarece la fuerza bruta.</summary>
    public const int Iteraciones = 120_000;

    public const int UsuarioMinimo = 3;
    public const int UsuarioMaximo = 16;
    public const int ClaveMinima = 6;
    public const int ClaveMaxima = 128;

    /// <summary>Una cuenta tal y como se guarda en disco (sin la clave).</summary>
    sealed class Registro
    {
        public string Usuario { get; set; } = "";
        public string Sal { get; set; } = "";
        public string Hash { get; set; } = "";
        public int Iteraciones { get; set; } = Cuentas.Iteraciones;
        public string Creada { get; set; } = "";
    }

    sealed class Archivo
    {
        public int Version { get; set; } = 1;
        public List<Registro> Registradas { get; set; } = new();
    }

    static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Sin distinguir mayusculas: "Juan" y "juan" son la misma cuenta.
    readonly Dictionary<string, Registro> _porNombre = new(StringComparer.OrdinalIgnoreCase);

    public int Total => _porNombre.Count;

    /// <summary>Nombre de cuenta aceptable: 3-16 caracteres, letras, digitos, guion,
    /// guion bajo o punto (nada de espacios ni simbolos raros).</summary>
    public static bool UsuarioValido(string usuario)
    {
        if (string.IsNullOrEmpty(usuario)) return false;
        if (usuario.Length < UsuarioMinimo || usuario.Length > UsuarioMaximo) return false;
        foreach (var ch in usuario)
            if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.')) return false;
        return true;
    }

    public bool Existe(string usuario) => _porNombre.ContainsKey((usuario ?? "").Trim());

    /// <summary>Nombre con la grafia guardada de la cuenta (o el mismo si no existe).
    /// Sirve para que el jugador entre siempre con el mismo nombre: el inventario y
    /// la propiedad de los mundos van por nombre.</summary>
    public string NombreCanonico(string usuario) =>
        _porNombre.TryGetValue((usuario ?? "").Trim(), out var registro) ? registro.Usuario : (usuario ?? "").Trim();

    /// <summary>Da de alta una cuenta. Falla con USUARIO_INVALIDO, CLAVE_CORTA o
    /// USUARIO_COGIDO si el nombre ya esta en uso (sin distinguir mayusculas).</summary>
    public bool Registrar(string usuario, string clave, out string error)
    {
        usuario = (usuario ?? "").Trim();
        clave ??= "";
        if (!UsuarioValido(usuario)) { error = "USUARIO_INVALIDO"; return false; }
        if (clave.Length < ClaveMinima || clave.Length > ClaveMaxima) { error = "CLAVE_CORTA"; return false; }
        if (_porNombre.ContainsKey(usuario)) { error = "USUARIO_COGIDO"; return false; }
        var sal = RandomNumberGenerator.GetBytes(16);
        _porNombre[usuario] = new Registro
        {
            Usuario = usuario,
            Sal = Convert.ToBase64String(sal),
            Hash = Convert.ToBase64String(Derivar(clave, sal, Iteraciones)),
            Iteraciones = Iteraciones,
            Creada = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        };
        error = "";
        return true;
    }

    /// <summary>Comprueba usuario y clave. Deriva siempre, tambien cuando el usuario
    /// no existe, para que el tiempo de respuesta no delate que nombres hay.</summary>
    public bool Verificar(string usuario, string clave)
    {
        usuario = (usuario ?? "").Trim();
        _porNombre.TryGetValue(usuario, out var registro);
        var sal = registro is null ? new byte[16] : Convert.FromBase64String(registro.Sal);
        var calculado = Derivar(clave ?? "", sal, registro?.Iteraciones ?? Iteraciones);
        if (registro is null) return false;
        byte[] esperado;
        try { esperado = Convert.FromBase64String(registro.Hash); } catch { return false; }
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    /// <summary>Cambia la clave sabiendo la vieja (y renueva la sal).</summary>
    public bool CambiarClave(string usuario, string claveVieja, string claveNueva, out string error)
    {
        if (!Verificar(usuario, claveVieja)) { error = "CREDENCIALES"; return false; }
        claveNueva ??= "";
        if (claveNueva.Length < ClaveMinima || claveNueva.Length > ClaveMaxima) { error = "CLAVE_CORTA"; return false; }
        var registro = _porNombre[(usuario ?? "").Trim()];
        var sal = RandomNumberGenerator.GetBytes(16);
        registro.Sal = Convert.ToBase64String(sal);
        registro.Hash = Convert.ToBase64String(Derivar(claveNueva, sal, Iteraciones));
        registro.Iteraciones = Iteraciones;
        error = "";
        return true;
    }

    static byte[] Derivar(string clave, byte[] sal, int iteraciones) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(clave), sal, iteraciones, HashAlgorithmName.SHA256, 32);

    public string AJson() => JsonSerializer.Serialize(new Archivo { Registradas = _porNombre.Values.ToList() }, Opciones);

    public void Guardar(string ruta)
    {
        var carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);
        File.WriteAllText(ruta, AJson(), new UTF8Encoding(false));
    }

    /// <summary>Carga el archivo de cuentas. Si falta o esta roto devuelve un
    /// almacen vacio en vez de reventar el arranque del servidor.</summary>
    public static Cuentas Cargar(string ruta)
    {
        var cuentas = new Cuentas();
        try
        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return cuentas;
            var archivo = JsonSerializer.Deserialize<Archivo>(File.ReadAllText(ruta), Opciones);
            if (archivo?.Registradas is null) return cuentas;
            foreach (var r in archivo.Registradas)
            {
                if (string.IsNullOrEmpty(r.Usuario) || string.IsNullOrEmpty(r.Sal) || string.IsNullOrEmpty(r.Hash)) continue;
                if (r.Iteraciones <= 0) continue;
                cuentas._porNombre[r.Usuario] = r;
            }
        }
        catch { return new Cuentas(); }
        return cuentas;
    }
}