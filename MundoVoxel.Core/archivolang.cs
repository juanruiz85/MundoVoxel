namespace MundoVoxel.Core;

/// <summary>Lector de archivos .lang (formato: clave=texto, líneas con # son comentarios).</summary>
public sealed class ArchivoLang
{
    readonly Dictionary<string, string> _textos;
    public string Idioma { get; }

    public ArchivoLang(string idioma, IEnumerable<string> lineas)
    {
        Idioma = idioma;
        _textos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var linea in lineas)
        {
            var l = linea.Trim();
            if (l.Length == 0 || l.StartsWith('#')) continue;
            int i = l.IndexOf('=');
            if (i <= 0) continue;
            _textos[l[..i].Trim()] = l[(i + 1)..].Trim();
        }
    }

    /// <summary>Traduce una clave, sustituyendo {0}, {1}… Si falta la clave, devuelve la propia clave.</summary>
    public string O(string clave, params object[] args)
        => _textos.TryGetValue(clave, out var t) ? string.Format(t, args) : clave;

    public static ArchivoLang? CargarArchivo(string ruta)
    {
        if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return null;
        return new ArchivoLang(Path.GetFileNameWithoutExtension(ruta), File.ReadAllLines(ruta));
    }
}
