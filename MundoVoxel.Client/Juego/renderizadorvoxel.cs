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
    // Los minerales usan el color de la PIEDRA: su textura de mena (manchas del
    // color del metal) la dibuja AgregarManchas al proyectar cada cara.
    static readonly (byte r, byte g, byte b)[] ColoresBase =
    {
        (0,0,0), (122,92,60), (128,128,128), (110,78,45), (226,208,160), (52,110,190),
        (96,160,52), (136,128,120), (170,90,70), (70,120,60), (200,220,230), (60,60,70),
        (176,140,84), (90,90,96), (160,130,80), (216,204,160),
        (122,118,112), (126,122,116), (124,120,114), (120,116,110), (96,200,196),
        (110,80,52), (120,170,60), (140,180,70), (170,180,70), (200,180,70),
        (80,150,70), (200,60,50), (255,170,60), (200,120,90),
        (123,119,113), (255,90,20),
        (40,40,46), (12,10,20),
    };

    // Colores de las manchas de mena (RGB)
    static readonly (byte r, byte g, byte b)[] ColoresMena =
    {
        (35,35,38),    // carbon
        (205,160,105), // hierro
        (220,140,70),  // cobre
        (245,205,60),  // oro
        (125,230,225), // diamante
    };

    // Sombreado por dirección de cara: +Y, -Y, +X, -X, +Z, -Z
    static readonly float[] Brillo = { 0.82f, 0.82f, 1.00f, 0.55f, 0.90f, 0.90f };

    public const int NivelesNiebla = 16;

    readonly Dictionary<(int, int), ChunkMalla> _mallas = new();
    public int NumMallas => _mallas.Count;
    readonly List<CaraVista> _visibles = new();
    byte[] _paletaRgb = Array.Empty<byte>(); // 4 bytes por entrada: R,G,B,alfa
    readonly float[] _sx = new float[4], _sy = new float[4], _zc = new float[4];

    Color _cieloArriba = Color.FromArgb("#6cb6e8");
    Color _cieloAbajo = Color.FromArgb("#cfe3f2");
    float _brillo = 1f; // luz global: 1 de dia, ~0.28 de noche

    const float Fov = 75f * MathF.PI / 180f;
    const float InicioNiebla = 0.30f;

    public int DistanciaChunks { get; set; } = 2;

    // Luz de antorchas: mapa 3D (0-15) del tamano del mundo, recalculado cuando
    // cambia una antorcha. La luz se suma al brillo global (de dia casi no se
    // nota; de noche ilumina alrededor de la llama).
    byte[] _luz = Array.Empty<byte>();
    int _anchoMundo, _profMundo;

    /// <summary>
    /// Aplica la hora del mundo (0-24h) a la luz global y al color del cielo.
    /// De dia brilla, al anochecer se oscurece y de noche queda una luz azulada.
    /// </summary>
    public void AplicarHora(float hora)
    {
        float t;
        if (hora >= 6f && hora < 18f) t = 1f;
        else if (hora < 5f || hora >= 19f) t = 0.30f;
        else if (hora < 6f) t = 0.30f + (hora - 5f) * 0.70f;   // amanecer 5-6
        else t = 1f - (hora - 18f) * 0.70f;                     // atardecer 18-19
        _brillo = t;
        float ar = Math.Min(1f, t * 1.1f), ag = Math.Min(1f, t * 0.95f), ab = Math.Min(1f, t * 1.5f);
        _cieloArriba = new Color(0x6c / 255f * ar, 0xb6 / 255f * ag, 0xe8 / 255f * ab);
        _cieloAbajo = new Color(0xcf / 255f * ar, 0xe3 / 255f * ag, 0xf2 / 255f * ab);
        PrepararPaleta();
    }

    public Color ColorBloque(ushort b)
    {
        var c = ColoresBase[Math.Min(b, (ushort)(ColoresBase.Length - 1))];
        return new Color(c.r / 255f, c.g / 255f, c.b / 255f);
    }

    struct CaraVista
    {
        public float Ax, Ay, Bx, By, Cx, Cy, Dx, Dy, Prof;
        public byte Bloque, Dir, Niebla;
        public int ColorArgb;   // >= 0: color base propio (con sombra/niebla)
        public byte Luz;        // luz de antorcha 0-15 en la celda del bloque
        public bool Emisivo;    // ignora sombra/niebla (llama de antorcha)
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
        RecalcularLuz(mundo);
        PrepararPaleta();
    }

    /// <summary>Reconstruye el chunk afectado por un cambio de bloque y los vecinos del borde.
    /// Con recalcularLuz=true se recalcula el mapa de luz de antorchas (colocar/romper antorcha).</summary>
    public void ReconstruirAlrededor(Mundo mundo, int x, int y, int z, bool recalcularLuz = false)
    {
        int cx = x / ChunkMalla.Tam, cz = z / ChunkMalla.Tam;
        var conjunto = new HashSet<(int, int)> { (cx, cz) };
        void Anadir(int px, int pz) { if (_mallas.ContainsKey((px, pz))) conjunto.Add((px, pz)); }
        if (x % ChunkMalla.Tam == 0) Anadir(cx - 1, cz);
        if (x % ChunkMalla.Tam == ChunkMalla.Tam - 1 || x == mundo.Ancho - 1) Anadir(cx + 1, cz);
        if (z % ChunkMalla.Tam == 0) Anadir(cx, cz - 1);
        if (z % ChunkMalla.Tam == ChunkMalla.Tam - 1 || z == mundo.Profundo - 1) Anadir(cx, cz + 1);
        foreach (var (px, pz) in conjunto) _mallas[(px, pz)].Reconstruir(mundo);
        if (recalcularLuz) RecalcularLuz(mundo);
    }

    /// <summary>Recalcula la luz de antorchas con un BFS multi-fuente. La luz (15 en la
    /// antorcha) decae 1 por bloque y solo la propagan los bloques transparentes; los
    /// opacos reciben luz pero no la dejan pasar (como en Minecraft).</summary>
    public void RecalcularLuz(Mundo mundo)
    {
        int ancho = mundo.Ancho, alto = mundo.Alto, prof = mundo.Profundo;
        _anchoMundo = ancho; _profMundo = prof;
        if (_luz.Length != ancho * alto * prof) _luz = new byte[ancho * alto * prof];
        Array.Clear(_luz);
        var cola = new Queue<(int, int, int)>();
        for (int x = 0; x < ancho; x++)
            for (int z = 0; z < prof; z++)
                for (int y = 0; y < alto; y++)
                    if (mundo.Obtener(x, y, z) == Bloques.Antorcha)
                    {
                        _luz[(y * prof + z) * ancho + x] = 15;
                        cola.Enqueue((x, y, z));
                    }
        Span<(int dx, int dy, int dz)> dirs = stackalloc (int, int, int)[6]
        {
            (1,0,0), (-1,0,0), (0,1,0), (0,-1,0), (0,0,1), (0,0,-1),
        };
        while (cola.Count > 0)
        {
            var (x, y, z) = cola.Dequeue();
            int li = _luz[(y * prof + z) * ancho + x] - 1;
            if (li <= 0) continue;
            for (int k = 0; k < 6; k++)
            {
                int nx = x + dirs[k].dx, ny = y + dirs[k].dy, nz = z + dirs[k].dz;
                if (nx < 0 || ny < 0 || nz < 0 || nx >= ancho || ny >= alto || nz >= prof) continue;
                int ni = (ny * prof + nz) * ancho + nx;
                if (_luz[ni] >= li) continue;
                _luz[ni] = (byte)li;
                ushort nb = mundo.Obtener(nx, ny, nz);
                if (Bloques.EsTransparente(nb) || nb == Bloques.Antorcha) cola.Enqueue((nx, ny, nz));
            }
        }
    }

    byte LuzEn(int x, int y, int z)
    {
        if (_luz.Length == 0) return 0;
        if (x < 0 || y < 0 || z < 0 || x >= _anchoMundo || y >= _luz.Length / (_anchoMundo * _profMundo) || z >= _profMundo) return 0;
        return _luz[(y * _profMundo + z) * _anchoMundo + x];
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
                    float sombra = Brillo[d] * _brillo;
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

        // Tinte de liquido: si la camara esta DENTRO de agua/lava, se aplica un
        // velo translucido a todo el frame (azul en agua, naranja en lava) para
        // que se note que estas sumergido sin que los bloques solidos parezcan
        // transparentes (antes, al no haber caras entre piedra y agua, se veia
        // a traves de todo el terreno).
        int ox = (int)MathF.Floor(ojo.X), oy = (int)MathF.Floor(ojo.Y), oz = (int)MathF.Floor(ojo.Z);
        if (mundo.Dentro(ox, oy, oz))
        {
            ushort bloqueOjo = mundo.Obtener(ox, oy, oz);
            if (bloqueOjo == Bloques.Agua) r.Tinte(40, 90, 190, 0.40f);
            else if (bloqueOjo == Bloques.Lava) r.Tinte(230, 80, 20, 0.45f);
        }
    }

    void RasterizarCara(Rasterizador r, in CaraVista cv)
    {
        // El bloque puede exceder la paleta (p. ej. Lava=30 recien anadido):
        // clampar para nunca salir del array (la paleta se dimensiona con el
        // tamano de ColoresBase en PrepararPaleta).
        int b = Math.Min((int)cv.Bloque, ColoresBase.Length - 1);

        if (cv.Emisivo)
        {
            // Llama de antorcha: color fijo, siempre brillante (sin sombra ni niebla)
            int er = (cv.ColorArgb >> 16) & 0xFF, eg = (cv.ColorArgb >> 8) & 0xFF, eb = cv.ColorArgb & 0xFF;
            r.Cuadrilatero(cv.Ax, cv.Ay, cv.Bx, cv.By, cv.Cx, cv.Cy, cv.Dx, cv.Dy, cv.Prof, (byte)er, (byte)eg, (byte)eb);
            return;
        }

        int br, bg, bb;
        if (cv.ColorArgb >= 0 || cv.Luz > 0)
        {
            if (cv.ColorArgb >= 0)
            {
                br = (cv.ColorArgb >> 16) & 0xFF;
                bg = (cv.ColorArgb >> 8) & 0xFF;
                bb = cv.ColorArgb & 0xFF;
            }
            else
            {
                br = ColoresBase[b].r; bg = ColoresBase[b].g; bb = ColoresBase[b].b;
            }
            // La luz de antorcha se SUMA al brillo global: de dia no cambia casi
            // nada, de noche ilumina la zona alrededor de la llama.
            float sombra = MathF.Min(1f, MathF.Max(Brillo[cv.Dir] * _brillo, cv.Luz / 15f));
            float t = cv.Niebla / (float)(NivelesNiebla - 1);
            float rr = br / 255f * sombra * (1 - t) + _cieloAbajo.Red * t;
            float gg = bg / 255f * sombra * (1 - t) + _cieloAbajo.Green * t;
            float bl = bb / 255f * sombra * (1 - t) + _cieloAbajo.Blue * t;
            r.Cuadrilatero(cv.Ax, cv.Ay, cv.Bx, cv.By, cv.Cx, cv.Cy, cv.Dx, cv.Dy, cv.Prof,
                (byte)(rr * 255), (byte)(gg * 255), (byte)(bl * 255), 1f);
            return;
        }

        int idx = (b * 6 * NivelesNiebla + cv.Dir * NivelesNiebla + cv.Niebla) * 4;
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
        float areaPx = (maxX - minX) * (maxY - minY);
        var (bx, by, bz) = BloqueDeCara(in cara);
        byte luz = LuzEn(bx, by, bz);
        _visibles.Add(new CaraVista
        {
            Ax = _sx[0], Ay = _sy[0], Bx = _sx[1], By = _sy[1],
            Cx = _sx[2], Cy = _sy[2], Dx = _sx[3], Dy = _sy[3],
            Prof = prof, Bloque = (byte)cara.Bloque, Dir = cara.Dir, Niebla = (byte)niebla,
            ColorArgb = cara.ColorArgb, Luz = luz, Emisivo = cara.Emisivo,
        });
        // Textura procedural: manchas de mena, vetas, franjas y bocas de horno
        // (solo cuando la cara ocupa suficiente area en pantalla).
        if (areaPx >= 200f) AgregarDetalle(in cara, bx, by, bz, ojo, der, arriba, fwd, f, cxp, cyp, prof, (byte)niebla);
    }

    /// <summary>Bloque al que pertenece una cara (el centro de la cara esta en el
    /// plano desplazado segun la direccion: +X -> x+1, +Y -> y+1, +Z -> z+1).</summary>
    static (int x, int y, int z) BloqueDeCara(in Cara cara)
    {
        float cx = (cara.A.X + cara.B.X + cara.C.X + cara.D.X) * 0.25f;
        float cy = (cara.A.Y + cara.B.Y + cara.C.Y + cara.D.Y) * 0.25f;
        float cz = (cara.A.Z + cara.B.Z + cara.C.Z + cara.D.Z) * 0.25f;
        int bx = (int)MathF.Floor(cx) - (cara.Dir == 0 ? 1 : 0);
        int by = (int)MathF.Floor(cy) - (cara.Dir == 2 ? 1 : 0);
        int bz = (int)MathF.Floor(cz) - (cara.Dir == 4 ? 1 : 0);
        return (bx, by, bz);
    }

    static uint Hash3(int x, int y, int z)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    /// <summary>Ejes del plano de la cara (origen en una esquina de la celda del
    /// bloque) para dibujar detalles sobre ella.</summary>
    static (Vector3 orig, Vector3 u, Vector3 v) EjesCara(byte dir, int x, int y, int z)
    {
        switch (dir)
        {
            case 0: return (new(x + 1, y, z), new(0, 0, 1), new(0, 1, 0)); // +X
            case 1: return (new(x, y, z), new(0, 0, 1), new(0, 1, 0));     // -X
            case 2: return (new(x, y + 1, z), new(1, 0, 0), new(0, 0, 1)); // +Y
            case 3: return (new(x, y, z), new(1, 0, 0), new(0, 0, 1));     // -Y
            case 4: return (new(x, y, z + 1), new(1, 0, 0), new(0, 1, 0)); // +Z
            default: return (new(x, y, z), new(1, 0, 0), new(0, 1, 0));    // -Z
        }
    }

    /// <summary>Dibuja la textura procedural sobre la cara: manchas de mena en los
    /// minerales, vetas en madera/tablones, franja en TNT y boca en el horno.</summary>
    void AgregarDetalle(in Cara cara, int bx, int by, int bz,
        Vector3 ojo, Vector3 der, Vector3 arriba, Vector3 fwd,
        float f, float cxp, float cyp, float profCara, byte niebla)
    {
        ushort b = cara.Bloque;
        bool mineral = Bloques.EsMineral(b);
        bool lateral = cara.Dir == 0 || cara.Dir == 1 || cara.Dir == 4 || cara.Dir == 5;
        if (!mineral && b != Bloques.Madera && b != Bloques.Tablones && b != Bloques.Tnt && b != Bloques.Horno) return;

        var (orig, u, v) = EjesCara(cara.Dir, bx, by, bz);
        uint h = Hash3(bx, by, bz);
        byte luz = LuzEn(bx, by, bz);

        if (mineral)
        {
            int color = ColorMenaArgb(b);
            for (int i = 0; i < 4; i++)
            {
                uint s = h + (uint)i * 2654435761u;
                float cu = 0.13f + ((s >> 8) & 0xFF) / 255f * 0.74f;
                float cv = 0.13f + ((s >> 16) & 0xFF) / 255f * 0.74f;
                float tam = 0.09f + ((s >> 24) & 0xFF) / 255f * 0.14f;
                Mancha(orig, u, v, cu, cv, tam, tam, color, ojo, der, arriba, fwd, f, cxp, cyp, profCara, cara.Bloque, cara.Dir, niebla, luz);
            }
            return;
        }

        if (b == Bloques.Madera)
        {
            if (cara.Dir == 2) // tope del tronco: duramen mas claro
                Mancha(orig, u, v, 0.5f, 0.5f, 0.62f, 0.62f, 0x8C6946, ojo, der, arriba, fwd, f, cxp, cyp, profCara, b, cara.Dir, niebla, luz);
            else if (lateral) // corteza: vetas verticales oscuras
                for (int i = 0; i < 3; i++)
                {
                    uint s = h + (uint)i * 40503u;
                    float cu = 0.18f + ((s >> 8) & 0xFF) / 255f * 0.64f;
                    Mancha(orig, u, v, cu, 0.5f, 0.075f, 0.98f, 0x5F442A, ojo, der, arriba, fwd, f, cxp, cyp, profCara, b, cara.Dir, niebla, luz);
                }
            return;
        }

        if (b == Bloques.Tablones)
        {
            // vetas horizontales de la madera
            for (int i = 0; i < 3; i++)
            {
                uint s = h + (uint)i * 7919u;
                float cv = 0.22f + ((s >> 8) & 0xFF) / 255f * 0.56f;
                Mancha(orig, u, v, 0.5f, cv, 0.96f, 0.055f, 0x966E46, ojo, der, arriba, fwd, f, cxp, cyp, profCara, b, cara.Dir, niebla, luz);
            }
            return;
        }

        if (b == Bloques.Tnt)
        {
            // franja blanca horizontal (estilo dinamita)
            Mancha(orig, u, v, 0.5f, 0.5f, 0.96f, 0.16f, 0xEBEBEB, ojo, der, arriba, fwd, f, cxp, cyp, profCara, b, cara.Dir, niebla, luz);
            return;
        }

        if (b == Bloques.Horno && cara.Dir == 4)
        {
            // boca del horno: marco gris y hueco oscuro
            Mancha(orig, u, v, 0.5f, 0.45f, 0.52f, 0.56f, 0x969696, ojo, der, arriba, fwd, f, cxp, cyp, profCara, b, cara.Dir, niebla, luz, 0.002f);
            Mancha(orig, u, v, 0.5f, 0.45f, 0.36f, 0.40f, 0x1C1C1E, ojo, der, arriba, fwd, f, cxp, cyp, profCara, b, cara.Dir, niebla, luz, 0.004f);
        }
    }

    static int ColorMenaArgb(ushort b)
    {
        var c = b switch
        {
            Bloques.Carbon => ColoresMena[0],
            Bloques.Hierro => ColoresMena[1],
            Bloques.Cobre => ColoresMena[2],
            Bloques.Oro => ColoresMena[3],
            _ => ColoresMena[4], // diamante
        };
        return (c.r << 16) | (c.g << 8) | c.b;
    }

    /// <summary>Proyecta y encola un pequeno cuadrilatero (detalle de textura) sobre
    /// la cara; profundidad ligeramente menor que la cara para ganar el z-test.</summary>
    void Mancha(Vector3 orig, Vector3 u, Vector3 v, float cu, float cv, float tu, float tv,
        int color, Vector3 ojo, Vector3 der, Vector3 arriba, Vector3 fwd,
        float f, float cxp, float cyp, float profCara, ushort bloque, byte dir, byte niebla, byte luz, float desvio = 0.003f)
    {
        var p0 = orig + u * (cu - tu * 0.5f) + v * (cv - tv * 0.5f);
        var p1 = orig + u * (cu + tu * 0.5f) + v * (cv - tv * 0.5f);
        var p2 = orig + u * (cu + tu * 0.5f) + v * (cv + tv * 0.5f);
        var p3 = orig + u * (cu - tu * 0.5f) + v * (cv + tv * 0.5f);
        Span<Vector3> pts = stackalloc Vector3[4] { p0, p1, p2, p3 };
        float zcTotal = 0;
        for (int i = 0; i < 4; i++)
        {
            var d = pts[i] - ojo;
            float xc = Vector3.Dot(d, der);
            float yc = Vector3.Dot(d, arriba);
            float zc = Vector3.Dot(d, fwd);
            if (zc < 0.12f) return;
            zcTotal += zc;
            _sx[i] = cxp + xc * f / zc;
            _sy[i] = cyp - yc * f / zc;
        }
        _visibles.Add(new CaraVista
        {
            Ax = _sx[0], Ay = _sy[0], Bx = _sx[1], By = _sy[1],
            Cx = _sx[2], Cy = _sy[2], Dx = _sx[3], Dy = _sy[3],
            Prof = profCara - desvio, Bloque = (byte)bloque, Dir = dir, Niebla = niebla,
            ColorArgb = color, Luz = luz,
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
