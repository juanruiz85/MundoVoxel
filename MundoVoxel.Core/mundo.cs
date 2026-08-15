using System.IO.Compression;
using System.Numerics;

namespace MundoVoxel.Core;

/// <summary>Mundo de bloques almacenado en memoria (matriz plana).</summary>
public class Mundo
{
    public readonly int Ancho, Alto, Profundo;
    public readonly ushort[] Datos;
    public readonly int Semilla;

    public Mundo(int ancho, int alto, int profundo, int semilla, ushort[]? bloques = null)
    {
        Ancho = ancho;
        Alto = alto;
        Profundo = profundo;
        Semilla = semilla;
        Datos = bloques ?? new ushort[ancho * alto * profundo];
    }

    int Idx(int x, int y, int z) => x + z * Ancho + y * Ancho * Profundo;
    public bool Dentro(int x, int y, int z) => x >= 0 && x < Ancho && y >= 0 && y < Alto && z >= 0 && z < Profundo;

    public ushort Obtener(int x, int y, int z)
        => Dentro(x, y, z) ? Datos[Idx(x, y, z)] : (ushort)(y < 0 ? Bloques.Lecho : Bloques.Aire);

    public void Poner(int x, int y, int z, ushort tipo)
    {
        if (Dentro(x, y, z)) Datos[Idx(x, y, z)] = tipo;
    }

    /// <summary>Punto de apariciÃ³n: encima del bloque mÃ¡s alto del centro.</summary>
    public Vector3 ObtenerPuntoAparicion()
    {
        int cx = Ancho / 2, cz = Profundo / 2;
        for (int y = Alto - 1; y > 0; y--)
            if (Bloques.EsSolido(Obtener(cx, y, cz)))
                return new Vector3(cx + 0.5f, y + 1.01f, cz + 0.5f);
        return new Vector3(cx + 0.5f, Alto * 0.7f, cz + 0.5f);
    }

    /// <summary>Altura (Y) de la superficie en (x, z): el primer bloque libre sobre el suelo.</summary>
    public int Superficie(int x, int z)
    {
        for (int y = Alto - 1; y > 0; y--)
            if (Bloques.EsSolido(Obtener(x, y, z)))
                return y + 1;
        return 1;
    }

    public static Mundo Generar(int semilla, int ancho = 128, int alto = 48, int profundo = 128)
    {
        var m = new Mundo(ancho, alto, profundo, semilla);
        int nivelMar = (int)(alto * 0.42f);
        var rnd = new Random(semilla);
        for (int x = 0; x < ancho; x++)
        {
            for (int z = 0; z < profundo; z++)
            {
                float n = Ruido.FBM(x * 0.045f, z * 0.045f, semilla);
                float n2 = Ruido.FBM(x * 0.12f + 77f, z * 0.12f + 77f, semilla + 5) * 0.5f;
                int h = Math.Clamp((int)(nivelMar - 4 + n * 12 + n2 * 6), 2, alto - 6);
                for (int y = 0; y < alto; y++)
                {
                    ushort b;
                    if (y == 0) b = Bloques.Lecho;
                    else if (y < h - 3) b = Bloques.Piedra;
                    else if (y < h) b = Bloques.Tierra;
                    else if (y == h) b = h <= nivelMar + 1 ? Bloques.Arena : Bloques.Cesped;
                    else if (y < nivelMar) b = Bloques.Agua;
                    else b = Bloques.Aire;
                    m.Poner(x, y, z, b);
                }
                if (h > nivelMar + 2 && rnd.NextDouble() < 0.006)
                    PonerArbol(m, x, h + 1, z, rnd);
            }
        }
        PonerMinerales(m, rnd, nivelMar);
        return m;
    }

    /// <summary>
    /// Vetas de minerales bajo tierra, como en Minecraft: el carbon es comun y
    /// superficial, el diamante raro y profundo. Cada veta es un recorrido
    /// aleatorio que solo reemplaza piedra.
    /// </summary>
    static void PonerMinerales(Mundo m, Random rnd, int nivelMar)
    {
        void Vetas(int cantidad, ushort bloque, int yMax, int yMin, int tamMin, int tamMax)
        {
            for (int i = 0; i < cantidad; i++)
            {
                int x = rnd.Next(2, m.Ancho - 2);
                int z = rnd.Next(2, m.Profundo - 2);
                int y = rnd.Next(yMin, Math.Max(yMin + 1, yMax));
                PonerVeta(m, x, y, z, bloque, rnd.Next(tamMin, tamMax + 1), rnd);
            }
        }

        Vetas(420, Bloques.Carbon,   nivelMar + 8, 2, 4, 9);
        Vetas(220, Bloques.Hierro,   nivelMar + 2, 2, 3, 7);
        Vetas(110, Bloques.Oro,      nivelMar - 4, 2, 2, 5);
        Vetas(55,  Bloques.Diamante, nivelMar - 10, 2, 1, 4);
    }

    static void PonerVeta(Mundo m, int x, int y, int z, ushort bloque, int tamano, Random rnd)
    {
        for (int i = 0; i < tamano; i++)
        {
            if (m.Dentro(x, y, z) && m.Obtener(x, y, z) == Bloques.Piedra)
                m.Poner(x, y, z, bloque);
            switch (rnd.Next(6))
            {
                case 0: x++; break;
                case 1: x--; break;
                case 2: y++; break;
                case 3: y--; break;
                case 4: z++; break;
                default: z--; break;
            }
        }
    }

    static void PonerArbol(Mundo m, int x, int y, int z, Random rnd)
    {
        int altura = 4 + rnd.Next(3);
        for (int i = 0; i < altura; i++) m.Poner(x, y + i, z, Bloques.Madera);
        for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dz == 0 && dy < 1) continue;
                    if (Math.Abs(dx) == 2 && Math.Abs(dz) == 2 && dy != 0) continue;
                    int px = x + dx, py = y + altura + dy, pz = z + dz;
                    if (m.Dentro(px, py, pz) && m.Obtener(px, py, pz) == Bloques.Aire)
                        m.Poner(px, py, pz, Bloques.Hoja);
                }
        m.Poner(x, y + altura + 1, z, Bloques.Hoja);
    }

    public byte[] Serializar()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(Ancho);
        bw.Write(Alto);
        bw.Write(Profundo);
        bw.Write(Semilla);
        foreach (var b in Datos) bw.Write(b);
        return ms.ToArray();
    }

    public static Mundo Deserializar(byte[] datos)
    {
        using var ms = new MemoryStream(datos);
        using var br = new BinaryReader(ms);
        int a = br.ReadInt32(), al = br.ReadInt32(), p = br.ReadInt32(), s = br.ReadInt32();
        var m = new Mundo(a, al, p, s);
        for (int i = 0; i < m.Datos.Length; i++) m.Datos[i] = br.ReadUInt16();
        return m;
    }

    public static byte[] Comprimir(byte[] datos)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest, true))
            gz.Write(datos);
        return ms.ToArray();
    }

    public static byte[] Descomprimir(byte[] datos)
    {
        using var ms = new MemoryStream(datos);
        using var gz = new GZipStream(ms, CompressionMode.Decompress);
        using var salida = new MemoryStream();
        gz.CopyTo(salida);
        return salida.ToArray();
    }
}

