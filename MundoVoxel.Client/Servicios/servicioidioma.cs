using System.Reflection;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Servicios;

/// <summary>
/// Carga el idioma desde un archivo .lang externo (fácil de editar) o, si no existe,
/// desde el recurso incrustado en la aplicación.
/// </summary>
public sealed class ServicioIdioma
{
    public ArchivoLang Lang { get; }

    public ServicioIdioma()
    {
        var externo = BuscarExterno();
        var archivo = ArchivoLang.CargarArchivo(externo);
        if (archivo != null)
        {
            Lang = archivo;
        }
        else
        {
            Lang = new ArchivoLang("es", CargarEmpotrado());
        }
        T.Cargar(this); // textos estáticos para plantillas XAML
    }

    static string BuscarExterno()
    {
        try
        {
            if (OperatingSystem.IsAndroid())
                return Path.Combine(FileSystem.AppDataDirectory, "lang", "es.lang");
            return Path.Combine(AppContext.BaseDirectory, "lang", "es.lang");
        }
        catch
        {
            return "";
        }
    }

    static IEnumerable<string> CargarEmpotrado()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lang.es.lang");
            if (s == null) return new[] { "app.titulo=MundoVoxel" };
            using var r = new StreamReader(s);
            return r.ReadToEnd().Split('\n');
        }
        catch
        {
            return new[] { "app.titulo=MundoVoxel" };
        }
    }

    public string O(string clave, params object[] args) => Lang.O(clave, args);
}
