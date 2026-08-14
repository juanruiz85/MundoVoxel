using System.Numerics;
using Microsoft.Maui.Graphics;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Juego;

/// <summary>
/// Renderizador 3D por software: proyecta las caras de los chunks y las rasteriza
/// sobre un buffer de píxeles con z-buffer (sin llamadas nativas por cara).
/// Portátil: funciona igual en Windows y Android.
/// </summary>
public sealed class RenderizadorVoxel
{
    // Colores base por bloque (RGB)
    static readonly (byte r, byte g, byte b)[] ColoresBase =
    {
        (0,0,0), (122,92,60), (128,128,128), (110,78,45), (226,208,160), (52,110,190),
        (96,160,52), (136,128,120), (170,90,70), (70,120,60), (200,220,230), (60,60,70),
        (176,140,84), (90,90,96),
    };

    // Sombreado por dirección de cara: +Y, -Y, +X, -X, +Z, -Z
    static readonly float[] Brillo = { 0.82f, 0.82f, 1.00f, 0.55f, 0.90f, 0.90f };

    public const int NivelesNiebla = 16;

    readonly Dictionary<(int, int), ChunkMalla> _mallas = new();
    readonly List<CaraVista> _visibles = new();
    byte[] _paletaRgb = Array.Empty<byte>(); // 4 bytes por entrada: R,G,B,alfa
    readonly float[] _sx = new float[4], _sy = new float[4], _zc = new float[4];

    Color _cieloArriba = Color.FromArgb("#6cb6e8");
    Color _cieloAbajo = Color.FromArgb("#cfe3f2");

    const float Fov = 75f * MathF.PI / 180f;
    const float InicioNiebla = 0.30f;

    public int DistanciaChunks { get; set; } = 2;

    public Color ColorBloque(ushort b)
    {
        var c = ColoresBase[Math.Min(b, (ushort)(ColoresBase.Length - 1))];
        return new Color(c.r / 255f, c.g / 255f, c.b / 255f);
    }

    struct CaraVista
    {
        public float Ax, Ay, Bx, By, Cx, Cy, Dx, Dy, Prof;
        public byte Bloque, Dir, Niebla;
    }

    public void ConstruirMallas(Mundo mundo)
    {
        _mallas.Clear();
        int cx = (int)MathF.Ceiling(mundo.Ancho / (float)ChunkMalla.Tam);
        int cz = (int)MathF.Ceiling(mundo.Profundo / (float)ChunkMalla.Tam);
        for (int x = 0; x < cx; x++)
            for (int z = 0; z < cz; z++)
            {
                var m = new ChunkMalla(x, z);
                m.Reconstruir(mundo);
                _mallas[(x, z)] = m;
            }
        PrepararPaleta();
    }

    /// <summary>Reconstruye el chunk afectado por un cambio de bloque y los vecinos del borde.</summary>
    public void ReconstruirAlrededor(Mundo mundo, int x, int y, int z)
    {
        int cx = x / ChunkMalla.Tam, cz = z / ChunkMalla.Tam;
        var conjunto = new HashSet<(int, int)> { (cx, cz) };
        void Anadir(int px, int pz) { if (_mallas.ContainsKey((px, pz))) conjunto.Add((px, pz)); }
        if (x % ChunkMalla.Tam == 0) Anadir(cx - 1, cz);
        if (x % ChunkMalla.Tam == ChunkMalla.Tam - 1 || x == mundo.Ancho - 1) Anadir(cx + 1, cz);
        if (z % ChunkMalla.Tam == 0) Anadir(cx, cz - 1);
        if (z % ChunkMalla.Tam == ChunkMalla.Tam - 1 || z == mundo.Profundo - 1) Anadir(cx, cz + 1);
        foreach (var (px, pz) in conjunto) _mallas[(px, pz)].Reconstruir(mundo);
    }

    void PrepararPaleta()
    {
        int n = ColoresBase.Length * 6 * NivelesNiebla;
        _paletaRgb = new byte[n * 4];
        for (int b = 0; b < ColoresBase.Length; b++)
        {
            for (int d = 0; d < 6; d++)
            {
                for (int niebla = 0; niebla < NivelesNiebla; niebla++)
                {
                    float t = niebla / (float)(NivelesNiebla - 1);
                    float sombra = Brillo[d];
                    float r = ColoresBase[b].r / 255f * sombra;
                    float g = ColoresBase[b].g / 255f * sombra;
                    float bl = ColoresBase[b].b / 255f * sombra;
                    r = r * (1 - t) + _cieloAbajo.Red * t;
                    g = g * (1 - t) + _cieloAbajo.Green * t;
                    bl = bl * (1 - t) + _cieloAbajo.Blue * t;
                    float alfa = Bloques.EsLiquido((ushort)b) ? 0.62f : 1f;
                    int i = (b * 6 * NivelesNiebla + d * NivelesNiebla + niebla) * 4;
                    _paletaRgb[i] = (byte)(r * 255);
                    _paletaRgb[i + 1] = (byte)(g * 255);
                    _paletaRgb[i + 2] = (byte)(bl * 255);
                    _paletaRgb[i + 3] = (byte)(alfa * 255);
                }
            }
        }
    }

    /// <summary>Rasteriza el mundo (cielo + caras) sobre el buffer de destino.</summary>
    public void Rasterizar(Rasterizador r, Mundo mundo, Camara cam, int ancho, int alto)
    {
        var ojo = cam.Pos;
        float f = (alto / 2f) / MathF.Tan(Fov / 2f);
        float cxp = ancho / 2f, cyp = alto / 2f;
        float maxDist = DistanciaChunks * ChunkMalla.Tam * 0.9f;
        float inicioNiebla = maxDist * InicioNiebla;

        r.Limpiar(
            (byte)(_cieloArriba.Red * 255), (byte)(_cieloArriba.Green * 255), (byte)(_cieloArriba.Blue * 255),
            (byte)(_cieloAbajo.Red * 255), (byte)(_cieloAbajo.Green * 255), (byte)(_cieloAbajo.Blue * 255));

        var fwd = cam.Adelante;
        var der = cam.Derecha;
        var arriba = Vector3.Cross(der, fwd);
        int pcx = (int)(ojo.X / ChunkMalla.Tam), pcz = (int)(ojo.Z / ChunkMalla.Tam);

        _visibles.Clear();
        foreach (var (k, malla) in _mallas)
        {
            int dx = k.Item1 - pcx, dz = k.Item2 - pcz;
            if (Math.Abs(dx) > DistanciaChunks || Math.Abs(dz) > DistanciaChunks) continue;
            float cex = k.Item1 * ChunkMalla.Tam + ChunkMalla.Tam / 2f - ojo.X;
            float cez = k.Item2 * ChunkMalla.Tam + ChunkMalla.Tam / 2f - ojo.Z;
            if (cex * fwd.X + cez * fwd.Z < -ChunkMalla.Tam * 3) continue;

            foreach (var cara in malla.Caras)
            {
                ProyectarCara(cara, ojo, der, arriba, fwd, f, cxp, cyp, ancho, alto, maxDist, inicioNiebla);
            }
        }

        // Pasada 1: opacas (z-buffer normal).
        for (int i = 0; i < _visibles.Count; i++)
        {
            var cv = _visibles[i];
            if (Bloques.EsLiquido(cv.Bloque)) continue;
            RasterizarCara(r, cv);
        }

        // Pasada 2: líquidas (agua) de lejos a cerca, con alpha.
        Span<int> indicesLiquido = _visibles.Count <= 1024
            ? stackalloc int[_visibles.Count]
            : new int[_visibles.Count];
        int n = 0;
        for (int i = 0; i < _visibles.Count; i++)
            if (Bloques.EsLiquido(_visibles[i].Bloque)) indicesLiquido[n++] = i;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (_visibles[indicesLiquido[j]].Prof > _visibles[indicesLiquido[i]].Prof)
                {
                    int tmp = indicesLiquido[i];
                    indicesLiquido[i] = indicesLiquido[j];
                    indicesLiquido[j] = tmp;
                }
        for (int i = 0; i < n; i++) RasterizarCara(r, _visibles[indicesLiquido[i]]);
    }

    void RasterizarCara(Rasterizador r, in CaraVista cv)
    {
        int idx = (cv.Bloque * 6 * NivelesNiebla + cv.Dir * NivelesNiebla + cv.Niebla) * 4;
        r.Cuadrilatero(cv.Ax, cv.Ay, cv.Bx, cv.By, cv.Cx, cv.Cy, cv.Dx, cv.Dy, cv.Prof,
            _paletaRgb[idx], _paletaRgb[idx + 1], _paletaRgb[idx + 2], _paletaRgb[idx + 3] / 255f);
    }

    void ProyectarCara(in Cara cara, Vector3 ojo, Vector3 der, Vector3 arriba, Vector3 fwd,
        float f, float cxp, float cyp, int ancho, int alto, float maxDist, float inicioNiebla)
    {
        Span<Vector3> pts = stackalloc Vector3[4] { cara.A, cara.B, cara.C, cara.D };
        float prof = 0;
        for (int i = 0; i < 4; i++)
        {
            var d = pts[i] - ojo;
            float xc = Vector3.Dot(d, der);
            float yc = Vector3.Dot(d, arriba);
            float zc = Vector3.Dot(d, fwd);
            if (zc < 0.12f) return;
            prof += zc;
            _zc[i] = zc;
            _sx[i] = cxp + xc * f / zc;
            _sy[i] = cyp - yc * f / zc;
        }
        prof *= 0.25f;
        if (prof > maxDist) return;
        float minX = MathF.Min(MathF.Min(_sx[0], _sx[1]), MathF.Min(_sx[2], _sx[3]));
        float maxX = MathF.Max(MathF.Max(_sx[0], _sx[1]), MathF.Max(_sx[2], _sx[3]));
        float minY = MathF.Min(MathF.Min(_sy[0], _sy[1]), MathF.Min(_sy[2], _sy[3]));
        float maxY = MathF.Max(MathF.Max(_sy[0], _sy[1]), MathF.Max(_sy[2], _sy[3]));
        if (maxX < -64 || minX > ancho + 64 || maxY < -64 || minY > alto + 64) return;

        int niebla = (int)((prof - inicioNiebla) / (maxDist - inicioNiebla) * NivelesNiebla);
        niebla = Math.Clamp(niebla, 0, NivelesNiebla - 1);
        _visibles.Add(new CaraVista
        {
            Ax = _sx[0], Ay = _sy[0], Bx = _sx[1], By = _sy[1],
            Cx = _sx[2], Cy = _sy[2], Dx = _sx[3], Dy = _sy[3],
            Prof = prof, Bloque = (byte)cara.Bloque, Dir = cara.Dir, Niebla = (byte)niebla,
        });
    }

    /// <summary>Rasteriza una caja (p. ej. un jugador o un mob) con sus 6 caras.</summary>
    public void RasterizarCaja(Rasterizador r, Camara cam, Vector3 min, Vector3 max, Color color)
    {
        float f = (r.H / 2f) / MathF.Tan(Fov / 2f);
        float cxp = r.W / 2f, cyp = r.H / 2f;
        var ojo = cam.Pos;
        var fwd = cam.Adelante;
        var der = cam.Derecha;
        var arriba = Vector3.Cross(der, fwd);

        Span<Vector3> esquinas = stackalloc Vector3[8]
        {
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z),
            new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        };
        Span<(int a, int b, int c, int d)> caras = stackalloc (int, int, int, int)[6]
        {
            (0,1,2,3), (4,5,6,7), (0,1,5,4), (1,2,6,5), (2,3,7,6), (3,0,4,7),
        };

        Span<float> sx = stackalloc float[8], sy = stackalloc float[8], zc = stackalloc float[8];
        for (int i = 0; i < 8; i++)
        {
            var d = esquinas[i] - ojo;
            float xc = Vector3.Dot(d, der);
            float yc = Vector3.Dot(d, arriba);
            zc[i] = Vector3.Dot(d, fwd);
            if (zc[i] < 0.12f) return;
            sx[i] = cxp + xc * f / zc[i];
            sy[i] = cyp - yc * f / zc[i];
        }

        byte rb = (byte)(color.Red * 255);
        byte gb = (byte)(color.Green * 255);
        byte bb = (byte)(color.Blue * 255);

        for (int i = 0; i < 6; i++)
        {
            var ca = caras[i];
            float prof = (zc[ca.a] + zc[ca.b] + zc[ca.c] + zc[ca.d]) * 0.25f;
            r.Cuadrilatero(sx[ca.a], sy[ca.a], sx[ca.b], sy[ca.b], sx[ca.c], sy[ca.c], sx[ca.d], sy[ca.d], prof, rb, gb, bb);
        }
    }
}
