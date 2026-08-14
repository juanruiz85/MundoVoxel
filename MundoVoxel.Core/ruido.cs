namespace MundoVoxel.Core;

/// <summary>Ruido de valor con interpolación suave (sin dependencias externas).</summary>
public static class Ruido
{
    static uint Mezclar(uint x)
    {
        x = ((x >> 16) ^ x) * 0x45d9f3b;
        x = ((x >> 16) ^ x) * 0x45d9f3b;
        x = (x >> 16) ^ x;
        return x;
    }

    static float Suave(float t) => t * t * (3f - 2f * t);
    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static float Valor2D(float x, float z, int semilla)
    {
        int xi = (int)MathF.Floor(x), zi = (int)MathF.Floor(z);
        float fx = x - xi, fz = z - zi;
        float s = Suave(fx), t = Suave(fz);
        float n00 = (Mezclar((uint)(xi * 374761393 + zi * 668265263 + (uint)semilla * 974711)) & 0xFFFF) / 65535f;
        float n10 = (Mezclar((uint)((xi + 1) * 374761393 + zi * 668265263 + (uint)semilla * 974711)) & 0xFFFF) / 65535f;
        float n01 = (Mezclar((uint)(xi * 374761393 + (zi + 1) * 668265263 + (uint)semilla * 974711)) & 0xFFFF) / 65535f;
        float n11 = (Mezclar((uint)((xi + 1) * 374761393 + (zi + 1) * 668265263 + (uint)semilla * 974711)) & 0xFFFF) / 65535f;
        return Lerp(Lerp(n00, n10, s), Lerp(n01, n11, s), t);
    }

    public static float FBM(float x, float z, int semilla, int octavas = 4, float persistencia = 0.5f)
    {
        float total = 0, amp = 1, freq = 1, max = 0;
        for (int i = 0; i < octavas; i++)
        {
            total += Valor2D(x * freq, z * freq, semilla + i * 101) * amp;
            max += amp;
            amp *= persistencia;
            freq *= 2;
        }
        return total / max;
    }
}
