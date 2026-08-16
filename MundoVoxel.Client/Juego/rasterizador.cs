using System.IO;

namespace MundoVoxel.Client.Juego;

/// <summary>
/// Rasterizador por software mínimo: rellena triángulos con z-buffer sobre un
/// buffer BGR de 24 bits. Se ejecuta en segundo plano para no bloquear la UI.
/// Produce además el frame como BMP para mostrarlo con un único blit.
/// </summary>
public sealed class Rasterizador
{
    public int W { get; private set; }
    public int H { get; private set; }
    public byte[] Pix = Array.Empty<byte>(); // BGR, fila 0 = arriba
    float[] _prof = Array.Empty<float>();

    public void Inicializar(int w, int h)
    {
        if (w <= 0 || h <= 0) { w = 1; h = 1; }
        if (W == w && H == h && Pix.Length == w * h * 3) return;
        W = w;
        H = h;
        Pix = new byte[w * h * 3];
        _prof = new float[w * h];
    }

    /// <summary>Rellena el fondo con un degradado vertical (arriba → abajo).</summary>
    public void Limpiar(byte r0, byte g0, byte b0, byte r1, byte g1, byte b1)
    {
        int i = 0;
        for (int y = 0; y < H; y++)
        {
            float t = H > 1 ? y / (float)(H - 1) : 0f;
            byte bb = (byte)(b0 + (b1 - b0) * t);
            byte gg = (byte)(g0 + (g1 - g0) * t);
            byte rr = (byte)(r0 + (r1 - r0) * t);
            for (int x = 0; x < W; x++)
            {
                Pix[i] = bb; Pix[i + 1] = gg; Pix[i + 2] = rr;
                i += 3;
            }
        }
        Array.Fill(_prof, float.MaxValue);
    }

    /// <summary>
    /// Mezcla un color translúcido sobre TODO el frame (sin tocar el z-buffer).
    /// Se usa para el tinte de agua/lava cuando la cámara está sumergida:
    /// al nadar bajo el agua todo se ve con un velo azul (y naranja en lava),
    /// pero los bloques sólidos se siguen viendo opacos detrás del velo.
    /// </summary>
    public void Tinte(byte r, byte g, byte b, float alpha)
    {
        if (alpha <= 0f || alpha >= 1f)
        {
            if (alpha >= 1f)
                for (int i = 0; i < Pix.Length; i += 3)
                {
                    Pix[i] = b; Pix[i + 1] = g; Pix[i + 2] = r;
                }
            return;
        }
        for (int i = 0; i < Pix.Length; i += 3)
        {
            Pix[i] = (byte)(Pix[i] + (b - Pix[i]) * alpha);
            Pix[i + 1] = (byte)(Pix[i + 1] + (g - Pix[i + 1]) * alpha);
            Pix[i + 2] = (byte)(Pix[i + 2] + (r - Pix[i + 2]) * alpha);
        }
    }

    /// <summary>Dibuja un cuadrilátero convexo (dos triángulos) con z-test y alpha opcional.</summary>
    public void Cuadrilatero(
        float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy,
        float prof, byte r, byte g, byte b, float alpha = 1f)
    {
        Tri(ax, ay, bx, by, cx, cy, prof, r, g, b, alpha);
        Tri(ax, ay, cx, cy, dx, dy, prof, r, g, b, alpha);
    }

    void Tri(
        float x0, float y0, float x1, float y1, float x2, float y2,
        float prof, byte r, byte g, byte b, float alpha)
    {
        int minX = (int)MathF.Floor(MathF.Min(MathF.Min(x0, x1), x2));
        int maxX = (int)MathF.Ceiling(MathF.Max(MathF.Max(x0, x1), x2));
        int minY = (int)MathF.Floor(MathF.Min(MathF.Min(y0, y1), y2));
        int maxY = (int)MathF.Ceiling(MathF.Max(MathF.Max(y0, y1), y2));
        if (maxX < 0 || minX >= W || maxY < 0 || minY >= H) return;
        minX = Math.Max(minX, 0); maxX = Math.Min(maxX, W - 1);
        minY = Math.Max(minY, 0); maxY = Math.Min(maxY, H - 1);

        float dy10 = y1 - y0, dx10 = x1 - x0;
        float dy21 = y2 - y1, dx21 = x2 - x1;
        float dy02 = y0 - y2, dx02 = x0 - x2;

        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            float ay = py - y0, byy = py - y1, cy = py - y2;
            int rowIdx = y * W;
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float e0 = (px - x0) * dy10 - ay * dx10;
                float e1 = (px - x1) * dy21 - byy * dx21;
                float e2 = (px - x2) * dy02 - cy * dx02;
                bool dentro = (e0 >= 0 && e1 >= 0 && e2 >= 0) || (e0 <= 0 && e1 <= 0 && e2 <= 0);
                if (!dentro) continue;

                int idx = rowIdx + x;
                if (prof >= _prof[idx]) continue;
                _prof[idx] = prof;

                int p = idx * 3;
                if (alpha >= 1f)
                {
                    Pix[p] = b; Pix[p + 1] = g; Pix[p + 2] = r;
                }
                else
                {
                    Pix[p] = (byte)(Pix[p] + (b - Pix[p]) * alpha);
                    Pix[p + 1] = (byte)(Pix[p + 1] + (g - Pix[p + 1]) * alpha);
                    Pix[p + 2] = (byte)(Pix[p + 2] + (r - Pix[p + 2]) * alpha);
                }
            }
        }
    }

    /// <summary>Codifica el buffer como BMP de 24 bits (para mostrarlo vía PlatformImage).</summary>
    public byte[] ABmp()
    {
        int stride = (W * 3 + 3) & ~3;
        int dataSize = stride * H;
        int fileSize = 54 + dataSize;
        var ms = new MemoryStream(fileSize);
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write((byte)'B'); bw.Write((byte)'M');
            bw.Write(fileSize);
            bw.Write(0);            // reservado
            bw.Write(54);           // offset a píxeles
            bw.Write(40);           // tamaño cabecera info
            bw.Write(W);
            bw.Write(H);
            bw.Write((short)1);     // planos
            bw.Write((short)24);    // bpp
            bw.Write(0);            // compresión BI_RGB
            bw.Write(dataSize);
            bw.Write(2835);         // ppm horizontal
            bw.Write(2835);         // ppm vertical
            bw.Write(0); bw.Write(0);

            var pad = new byte[stride - W * 3];
            for (int y = H - 1; y >= 0; y--)  // BMP almacena filas de abajo hacia arriba
            {
                bw.Write(Pix, y * W * 3, W * 3);
                if (pad.Length > 0) bw.Write(pad);
            }
        }
        return ms.ToArray();
    }
}
