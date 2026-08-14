namespace MundoVoxel.Client.Servicios;

/// <summary>Registro de diagnostico en crash.log junto al ejecutable.</summary>
public static class Diag
{
    public static void Log(string m)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now:HH:mm:ss}] {m}{Environment.NewLine}");
        }
        catch { }
    }
}
