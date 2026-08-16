using System.Text.Json;

namespace MundoVoxel.Core;

/// <summary>
/// Ajustes del juego cargados desde `ajustes.config.json` (junto al ejecutable).
/// Si el archivo no existe o falta una clave, se usan los valores por defecto.
/// </summary>
public static class Ajustes
{
    public sealed class Config
    {
        // Hojas: probabilidades de soltar items al romper una hoja
        public double ProbabilidadPlantonHoja { get; set; } = 0.10;
        public double ProbabilidadManzanaHoja { get; set; } = 0.06;
        public double ProbabilidadPaloHoja { get; set; } = 0.12;
        // Cámara: límite de inclinación vertical (radianes; Minecraft usa ~±1.5708 = ±90°)
        public float LimitePitch { get; set; } = 1.55f;
        // Sensibilidad por defecto del ratón (multiplicador)
        public float SensibilidadRaton { get; set; } = 1f;
        // Agua: tiempo de oxígeno en segundos
        public float OxigenoMaximo { get; set; } = 15f;
        // Daño por segundo al tocar lava
        public float DanioLavaPorSegundo { get; set; } = 4f;
        // Daño por segundo al quedarse sin oxígeno
        public float DanioAhogamientoPorSegundo { get; set; } = 2f;
    }

    static Config _cfg = new();

    public static Config Actual => _cfg;

    /// <summary>Carga el archivo de ajustes desde el directorio indicado (o usa defaults).</summary>
    public static void Cargar(string directorio)
    {
        try
        {
            var ruta = Path.Combine(directorio, "ajustes.config.json");
            if (File.Exists(ruta))
            {
                var texto = File.ReadAllText(ruta);
                var c = JsonSerializer.Deserialize<Config>(texto, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
                if (c != null) _cfg = c;
            }
        }
        catch
        {
            // Si el archivo está mal, seguimos con los valores por defecto.
        }
    }
}
