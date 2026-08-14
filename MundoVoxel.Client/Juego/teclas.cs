namespace MundoVoxel.Client.Juego;

/// <summary>Códigos de tecla portables (valores de VirtualKey de Windows y códigos propios para táctil).</summary>
public static class Teclas
{
    public const int W = 0x57, A = 0x41, S = 0x53, D = 0x44;
    public const int Espacio = 0x20, F = 0x46, T = 0x54, Escape = 0x1B, R = 0x52, Mayus = 0x10;
    public const int Num1 = 0x31, Num9 = 0x39;

    // Códigos virtuales para los controles táctiles (Android)
    public const int Volar = 0x100, Chat = 0x101, Pausa = 0x102,
                     Romper = 0x103, Colocar = 0x104, Saltar = 0x105, Bajar = 0x106;
}
