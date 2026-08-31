using System.IO.Compression;
using Microsoft.Maui.Graphics;
using MundoVoxel.Core;

namespace MundoVoxel.Client.Juego;

/// <summary>Lienzo abstracto para dibujar iconos (coordenadas logicas 0-31, como un
/// atlas de 32x32). Dos implementaciones: LienzoRaster (buffer RGBA -> PNG con
/// transparencia, para los slots del inventario/cofre) y LienzoCanvas (ICanvas,
/// para la hotbar del HUD y los drops en el mundo).</summary>
public interface ILienzoIcono
{
    void Rect(float x, float y, float w, float h, byte r, byte g, byte b);
    void Linea(float x0, float y0, float x1, float y1, float grosor, byte r, byte g, byte b);
    void Elipse(float cx, float cy, float rx, float ry, byte r, byte g, byte b);
}

/// <summary>Disenos de todos los items y bloques: cada material tiene su forma
/// (herramientas con cabeza y mango, lingotes con brillo, comida, semillas,
/// bloques con textura...). El mismo diseno sirve para la hotbar, el inventario,
/// el cofre y los drops.</summary>
public static class IconosItems
{
    // Paleta de materiales de herramientas
    static (byte r, byte g, byte b) ColorCabeza(ushort m) => m switch
    {
        (ushort)ItemId.PicoMadera or (ushort)ItemId.EspadaMadera or (ushort)ItemId.HachaMadera
            or (ushort)ItemId.PalaMadera or (ushort)ItemId.AzadaMadera => (150, 110, 70),
        (ushort)ItemId.PicoPiedra or (ushort)ItemId.EspadaPiedra or (ushort)ItemId.HachaPiedra
            or (ushort)ItemId.PalaPiedra or (ushort)ItemId.AzadaPiedra => (128, 128, 128),
        (ushort)ItemId.PicoCobre or (ushort)ItemId.EspadaCobre or (ushort)ItemId.HachaCobre
            or (ushort)ItemId.PalaCobre or (ushort)ItemId.AzadaCobre => (200, 120, 70),
        (ushort)ItemId.PicoHierro or (ushort)ItemId.EspadaHierro or (ushort)ItemId.HachaHierro
            or (ushort)ItemId.PalaHierro or (ushort)ItemId.AzadaHierro => (200, 200, 205),
        (ushort)ItemId.PicoOro or (ushort)ItemId.EspadaOro or (ushort)ItemId.HachaOro
            or (ushort)ItemId.PalaOro or (ushort)ItemId.AzadaOro => (245, 205, 60),
        _ => (110, 220, 230), // diamante
    };

    static int TipoHerramienta(ushort m) => m switch
    {
        (ushort)ItemId.PicoMadera or (ushort)ItemId.PicoPiedra or (ushort)ItemId.PicoCobre
            or (ushort)ItemId.PicoHierro or (ushort)ItemId.PicoOro or (ushort)ItemId.PicoDiamante => 0,
        (ushort)ItemId.EspadaMadera or (ushort)ItemId.EspadaPiedra or (ushort)ItemId.EspadaCobre
            or (ushort)ItemId.EspadaHierro or (ushort)ItemId.EspadaOro or (ushort)ItemId.EspadaDiamante => 1,
        (ushort)ItemId.HachaMadera or (ushort)ItemId.HachaPiedra or (ushort)ItemId.HachaCobre
            or (ushort)ItemId.HachaHierro or (ushort)ItemId.HachaOro or (ushort)ItemId.HachaDiamante => 2,
        (ushort)ItemId.PalaMadera or (ushort)ItemId.PalaPiedra or (ushort)ItemId.PalaCobre
            or (ushort)ItemId.PalaHierro or (ushort)ItemId.PalaOro or (ushort)ItemId.PalaDiamante => 3,
        _ => 4, // azada
    };

    static bool EsHerramienta(ushort m)
        => m >= (ushort)ItemId.PicoMadera && m <= (ushort)ItemId.AzadaPiedra
        || m >= (ushort)ItemId.PicoCobre && m <= (ushort)ItemId.AzadaDiamante;

    public static void Dibujar(ILienzoIcono l, ushort material)
    {
        if (EsHerramienta(material)) { Herramienta(l, material); return; }
        switch (material)
        {
            case (ushort)ItemId.Palo: Palo(l); return;
            case (ushort)ItemId.CarneCrudaCerdo: Carne(l, 220, 140, 130); return;
            case (ushort)ItemId.CarneCocinadaCerdo: Carne(l, 165, 110, 65); return;
            case (ushort)ItemId.CarneCrudaVaca: Carne(l, 185, 80, 80); return;
            case (ushort)ItemId.CarneCocinadaVaca: Carne(l, 150, 95, 55); return;
            case (ushort)ItemId.CarneCrudaOveja: Carne(l, 230, 180, 170); return;
            case (ushort)ItemId.CarneCocinadaOveja: Carne(l, 170, 120, 80); return;
            case (ushort)ItemId.CarnePodrida: Carne(l, 120, 140, 90); return;
            case (ushort)ItemId.Polvora: Polvora(l); return;
            case (ushort)ItemId.Cuero: Cuero(l); return;
            case (ushort)ItemId.Hueso: Hueso(l); return;
            case (ushort)ItemId.LingoteHierro: Lingote(l, 215, 215, 220); return;
            case (ushort)ItemId.Zanahoria: Zanahoria(l); return;
            case (ushort)ItemId.Manzana: Manzana(l); return;
            case (ushort)ItemId.SemillasTrigo: Semillas(l); return;
            case (ushort)ItemId.Trigo: TrigoItem(l); return;
            case (ushort)ItemId.OroBruto: MenaItem(l, 245, 205, 60); return;
            case (ushort)ItemId.CobreBruto: MenaItem(l, 220, 140, 70); return;
            case (ushort)ItemId.HierroBruto: MenaItem(l, 205, 160, 105); return;
            case (ushort)ItemId.DiamanteBruto: MenaItem(l, 125, 230, 225); return;
            case (ushort)ItemId.CarbonItem: MenaItem(l, 35, 35, 38); return;
            case (ushort)ItemId.LingoteOro: Lingote(l, 245, 205, 60); return;
            case (ushort)ItemId.LingoteCobre: Lingote(l, 215, 135, 75); return;
            case (ushort)ItemId.Diamante: DiamanteItem(l); return;
            case (ushort)ItemId.Lana: Lana(l); return;
            case (ushort)ItemId.Mechero: Mechero(l); return;
            case Bloques.Madera: Tronco(l); return;
            case Bloques.Piedra: BloqueTexturado(l, 128, 128, 128, 96, 96, 96); return;
            case Bloques.Tierra: BloqueTexturado(l, 110, 78, 45, 82, 58, 33); return;
            case Bloques.Arena: BloqueTexturado(l, 226, 208, 160, 190, 172, 130); return;
            case Bloques.Cesped: Cesped(l); return;
            case Bloques.Hoja: BloqueTexturado(l, 70, 120, 60, 45, 90, 40); return;
            case Bloques.Cristal: Cristal(l); return;
            case Bloques.Nieve: Nieve(l); return;
            case Bloques.Cactus: Cactus(l); return;
            case Bloques.Grava: BloqueTexturado(l, 118, 112, 108, 88, 82, 80); return;
            case Bloques.Arenisca: Arenisca(l); return;
            case Bloques.Tablones: Tablones(l); return;
            case Bloques.Horno: Horno(l); return;
            case Bloques.Mesa: MesaIcono(l); return;
            case Bloques.Carbon: Mena(l, 35, 35, 38); return;
            case Bloques.Hierro: Mena(l, 205, 160, 105); return;
            case Bloques.Oro: Mena(l, 245, 205, 60); return;
            case Bloques.Diamante: Mena(l, 125, 230, 225); return;
            case Bloques.Cobre: Mena(l, 220, 140, 70); return;
            case Bloques.Antorcha: AntorchaIcono(l); return;
            case Bloques.Tnt: TntIcono(l); return;
            case Bloques.Cofre: CofreIcono(l); return;
            case Bloques.Trigo0 or Bloques.Trigo1 or Bloques.Trigo2 or Bloques.Trigo3: PlantaIcono(l, material); return;
            case Bloques.Planton: PlantaIcono(l, material); return;
            default:
                // Bloque generico: color del material con borde oscuro (sombra)
                var (r, g, b) = Objetos.Color(material);
                l.Rect(1, 1, 30, 30, r, g, b);
                l.Rect(1, 29, 30, 2, (byte)(r / 2), (byte)(g / 2), (byte)(b / 2));
                l.Rect(29, 1, 2, 30, (byte)(r * 3 / 4), (byte)(g * 3 / 4), (byte)(b * 3 / 4));
                return;
        }
    }

    // ------------------------------ herramientas ------------------------------

    static void Herramienta(ILienzoIcono l, ushort m)
    {
        var c = ColorCabeza(m);
        int t = TipoHerramienta(m);
        // Mango diagonal (o vertical para la espada)
        if (t == 1)
        {
            l.Rect(14, 24, 4, 6, 120, 88, 55);        // mango
            l.Rect(12, 21, 8, 3, 110, 78, 45);        // guarda
            l.Rect(13, 3, 6, 18, c.r, c.g, c.b);      // hoja
            l.Rect(14, 2, 4, 3, (byte)Math.Min(255, c.r + 40), (byte)Math.Min(255, c.g + 40), (byte)Math.Min(255, c.b + 40)); // punta
        }
        else
        {
            l.Linea(11, 24, 24, 10, 3.5f, 150, 110, 70); // mango
            switch (t)
            {
                case 0: // pico: arco hacia abajo
                    l.Rect(6, 7, 20, 4, c.r, c.g, c.b);
                    l.Rect(6, 11, 6, 8, c.r, c.g, c.b);
                    l.Rect(20, 11, 6, 8, c.r, c.g, c.b);
                    break;
                case 2: // hacha
                    l.Rect(7, 5, 17, 5, c.r, c.g, c.b);
                    l.Rect(7, 10, 7, 4, c.r, c.g, c.b);
                    break;
                case 3: // pala
                    l.Rect(8, 3, 16, 7, c.r, c.g, c.b);
                    break;
                default: // azada
                    l.Rect(6, 5, 20, 4, c.r, c.g, c.b);
                    l.Rect(6, 9, 8, 5, c.r, c.g, c.b);
                    break;
            }
        }
    }

    // ------------------------------ items varios ------------------------------

    static void Palo(ILienzoIcono l)
    {
        l.Rect(13, 3, 6, 26, 160, 120, 70);
        l.Rect(14, 5, 2, 22, 195, 155, 100);
    }

    static void Carne(ILienzoIcono l, byte r, byte g, byte b)
    {
        l.Rect(9, 8, 14, 16, r, g, b);
        l.Rect(9, 6, 14, 3, (byte)(r * 3 / 4), (byte)(g * 3 / 4), (byte)(b * 3 / 4));
    }

    static void Polvora(ILienzoIcono l)
    {
        l.Rect(6, 8, 20, 16, 75, 75, 80);
        for (int i = 0; i < 5; i++)
        {
            float x = 9 + (i * 37 % 13);
            float y = 12 + (i * 29 % 9);
            l.Rect(x, y, 2.5f, 2.5f, 45, 45, 50);
        }
    }

    static void Cuero(ILienzoIcono l)
    {
        l.Rect(8, 10, 16, 12, 170, 120, 70);
        l.Linea(11, 13, 21, 13, 2f, 140, 95, 50);
        l.Linea(11, 17, 20, 17, 2f, 140, 95, 50);
        l.Rect(8, 7, 16, 3, 190, 145, 90);
    }

    static void Hueso(ILienzoIcono l)
    {
        l.Rect(12, 5, 8, 22, 235, 232, 225);
        l.Elipse(12, 5, 4, 3.5f, 235, 232, 225);
        l.Elipse(12, 27, 4, 3.5f, 235, 232, 225);
        l.Linea(14, 8, 18, 8, 2f, 205, 200, 190);
    }

    static void Lingote(ILienzoIcono l, byte r, byte g, byte b)
    {
        l.Rect(9, 9, 14, 14, r, g, b);
        l.Rect(11, 11, 7, 4, (byte)Math.Min(255, r + 40), (byte)Math.Min(255, g + 40), (byte)Math.Min(255, b + 40));
        l.Rect(9, 23, 14, 2, (byte)(r / 2), (byte)(g / 2), (byte)(b / 2));
    }

    static void Zanahoria(ILienzoIcono l)
    {
        l.Rect(14, 10, 5, 16, 240, 140, 50);
        l.Linea(16, 6, 16, 12, 2.5f, 80, 140, 60);
        l.Linea(16, 7, 12, 4, 2f, 90, 155, 65);
        l.Linea(16, 8, 21, 4, 2f, 90, 155, 65);
    }

    static void Manzana(ILienzoIcono l)
    {
        l.Elipse(16, 18, 8, 7.5f, 220, 60, 50);
        l.Rect(15, 9, 2.5f, 4, 120, 88, 55);
        l.Elipse(18, 8, 3, 2, 90, 155, 65);
        l.Elipse(13, 16, 2.5f, 2.5f, 240, 120, 100);
    }

    static void Semillas(ILienzoIcono l)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = 11 + (i % 2) * 9;
            float y = 11 + (i / 2) * 9;
            l.Elipse(x, y, 3, 2.2f, 165, 195, 80);
            l.Rect(x - 3, y - 1, 3, 2, 120, 150, 55);
        }
    }

    static void TrigoItem(ILienzoIcono l)
    {
        l.Linea(16, 26, 16, 8, 3f, 190, 160, 70);
        for (int i = 0; i < 4; i++)
        {
            l.Rect(11, 8 + i * 4.5f, 10, 3, 215, 185, 85);
        }
        l.Elipse(16, 6, 4, 3, 225, 195, 95);
    }

    static void MenaItem(ILienzoIcono l, byte r, byte g, byte b)
    {
        l.Rect(3, 4, 26, 24, 122, 118, 112);
        for (int i = 0; i < 4; i++)
        {
            float x = 7 + (i * 41 % 17);
            float y = 8 + (i * 33 % 15);
            l.Rect(x, y, 4.5f, 4.5f, r, g, b);
        }
    }

    static void DiamanteItem(ILienzoIcono l)
    {
        l.Elipse(16, 16, 9, 8, 110, 220, 230);
        l.Rect(14, 9, 4, 4, 180, 245, 250);
    }

    static void Lana(ILienzoIcono l)
    {
        l.Rect(6, 8, 20, 16, 240, 240, 235);
        l.Elipse(10, 12, 3, 2.5f, 210, 210, 205);
        l.Elipse(17, 12, 3, 2.5f, 210, 210, 205);
        l.Elipse(13, 18, 3, 2.5f, 210, 210, 205);
        l.Elipse(20, 18, 3, 2.5f, 210, 210, 205);
    }

    static void Mechero(ILienzoIcono l)
    {
        l.Rect(11, 10, 10, 18, 145, 145, 150);
        l.Rect(13, 13, 3, 6, 190, 190, 195);
        l.Rect(13, 21, 3, 4, 90, 90, 95);
        l.Elipse(16, 8, 3.5f, 5, 255, 150, 50);
        l.Elipse(16, 5, 2, 3, 255, 220, 90);
    }

    // ------------------------------ bloques ------------------------------

    static void BloqueTexturado(ILienzoIcono l, byte r, byte g, byte b, byte r2, byte g2, byte b2)
    {
        l.Rect(1, 1, 30, 30, r, g, b);
        for (int i = 0; i < 4; i++)
        {
            float x = 5 + (i * 47 % 20);
            float y = 6 + (i * 31 % 20);
            l.Rect(x, y, 3.5f, 3.5f, r2, g2, b2);
        }
        l.Rect(1, 29, 30, 2, (byte)(r / 2), (byte)(g / 2), (byte)(b / 2));
    }

    static void Cristal(ILienzoIcono l)
    {
        // Marco celeste + destellos diagonales (vidrio)
        l.Rect(1, 1, 30, 30, 150, 200, 225);
        l.Rect(4, 4, 24, 24, 200, 230, 245);
        l.Linea(6, 25, 25, 6, 2.5f, 235, 250, 255);
        l.Linea(10, 27, 27, 10, 1.5f, 220, 240, 250);
        l.Rect(1, 1, 30, 2, 130, 185, 215);
        l.Rect(1, 29, 30, 2, 110, 165, 195);
    }

    static void Arenisca(ILienzoIcono l)
    {
        l.Rect(1, 1, 30, 30, 222, 204, 150);
        for (int i = 0; i < 4; i++)
        {
            l.Rect(1, 6 + i * 6, 30, 1.8f, 198, 180, 128);
        }
        l.Rect(1, 29, 30, 2, 180, 162, 115);
    }

    static void Nieve(ILienzoIcono l)
    {
        l.Rect(1, 1, 30, 30, 240, 244, 248);
        l.Elipse(11, 10, 4, 3, 255, 255, 255);
        l.Elipse(21, 20, 3.5f, 3, 255, 255, 255);
        l.Rect(1, 29, 30, 2, 200, 210, 220);
    }

    static void Cactus(ILienzoIcono l)
    {
        l.Rect(12, 4, 8, 26, 90, 140, 60);
        l.Rect(8, 10, 4, 5, 90, 140, 60);
        l.Rect(20, 16, 4, 5, 90, 140, 60);
        l.Rect(13, 6, 2, 22, 115, 170, 80);
        for (int i = 0; i < 5; i++)
        {
            l.Rect(14, 7 + i * 5, 2, 1.5f, 230, 230, 190);
        }
    }

    static void Tronco(ILienzoIcono l)
    {
        l.Rect(2, 2, 28, 28, 122, 92, 60);
        l.Elipse(16, 16, 9, 9, 100, 75, 48);
        l.Elipse(16, 16, 5, 5, 80, 60, 40);
        l.Rect(2, 2, 28, 4, 140, 110, 75);
    }

    static void Cesped(ILienzoIcono l)
    {
        l.Rect(1, 1, 30, 30, 96, 160, 52);
        l.Rect(1, 20, 30, 11, 110, 78, 45);
        l.Rect(1, 19, 30, 2, 70, 115, 40);
        l.Rect(1, 29, 30, 2, 70, 50, 30);
    }

    static void Tablones(ILienzoIcono l)
    {
        l.Rect(1, 1, 30, 30, 176, 140, 84);
        for (int i = 0; i < 4; i++)
        {
            l.Rect(1, 4 + i * 7, 30, 2, 145, 112, 66);
        }
        l.Rect(1, 29, 30, 2, 120, 90, 55);
    }

    static void Horno(ILienzoIcono l)
    {
        l.Rect(1, 1, 30, 30, 96, 96, 102);
        l.Rect(8, 12, 16, 16, 150, 150, 155);
        l.Rect(11, 15, 10, 10, 28, 28, 30);
        l.Rect(1, 1, 30, 5, 115, 115, 120);
    }

    static void MesaIcono(ILienzoIcono l)
    {
        l.Rect(2, 12, 28, 6, 150, 110, 60);
        l.Rect(3, 16, 26, 3, 120, 84, 44);
        l.Rect(4, 19, 5, 13, 120, 84, 44);
        l.Rect(23, 19, 5, 13, 120, 84, 44);
        l.Rect(4, 18, 26, 2, 90, 65, 35);
    }

    static void Mena(ILienzoIcono l, byte r, byte g, byte b)
    {
        l.Rect(1, 1, 30, 30, 124, 120, 114);
        for (int i = 0; i < 5; i++)
        {
            float x = 4 + (i * 53 % 22);
            float y = 4 + (i * 37 % 22);
            l.Rect(x, y, 5, 5, r, g, b);
        }
        l.Rect(1, 29, 30, 2, 70, 66, 60);
    }

    static void AntorchaIcono(ILienzoIcono l)
    {
        l.Rect(14, 8, 4, 20, 150, 110, 70);
        l.Rect(13, 6, 6, 3, 160, 120, 80);
        l.Elipse(16, 6, 4, 6, 255, 150, 50);
        l.Elipse(16, 3, 2.5f, 4, 255, 220, 90);
    }

    static void TntIcono(ILienzoIcono l)
    {
        l.Rect(1, 1, 30, 30, 200, 60, 50);
        l.Rect(1, 13, 30, 6, 235, 235, 235);
        l.Rect(10, 2, 12, 4, 180, 45, 40);
    }

    static void CofreIcono(ILienzoIcono l)
    {
        l.Rect(3, 12, 26, 12, 140, 100, 55);
        l.Rect(2, 7, 28, 6, 115, 80, 45);
        l.Rect(13, 16, 6, 4, 220, 198, 80);
        l.Rect(3, 24, 26, 2, 80, 55, 30);
    }

    static void PlantaIcono(ILienzoIcono l, ushort b)
    {
        (byte, byte, byte) c = b switch
        {
            Bloques.Planton => (90, 150, 70),
            Bloques.Trigo0 => (120, 170, 60),
            Bloques.Trigo1 => (140, 180, 70),
            Bloques.Trigo2 => (170, 180, 70),
            _ => (200, 180, 70),
        };
        float alto = b == Bloques.Planton ? 14 : 18 + (b - Bloques.Trigo0) * 3;
        l.Linea(16, 28, 16, 28 - alto, 3f, c.Item1, c.Item2, c.Item3);
        l.Elipse(13, 26 - alto, 3.5f, 3, c.Item1, c.Item2, c.Item3);
        l.Elipse(19, 24 - alto, 3.5f, 3, c.Item1, c.Item2, c.Item3);
        l.Elipse(16, 22 - alto, 3, 3, c.Item1, c.Item2, c.Item3);
    }
}

/// <summary>Cache de imagenes de iconos (PNG con transparencia) por material,
/// para los slots del inventario y del cofre.</summary>
public static class IconosItemsCache
{
    static readonly Dictionary<ushort, ImageSource> _cache = new();

    public static ImageSource De(ushort material)
    {
        if (!_cache.TryGetValue(material, out var img))
        {
            var lienzo = new LienzoRaster();
            IconosItems.Dibujar(lienzo, material);
            var png = lienzo.Png();
            img = ImageSource.FromStream(() => new MemoryStream(png));
            _cache[material] = img;
        }
        return img;
    }
}

/// <summary>Lienzo que dibuja en un buffer RGBA y lo empaqueta como PNG con
/// transparencia (para las imagenes de los slots del inventario y el cofre).</summary>
public sealed class LienzoRaster : ILienzoIcono
{
    public const int S = 32;
    readonly byte[] _px = new byte[S * S * 4];
    const float Esc = S / 32f;

    static void Set(byte[] px, int x, int y, byte r, byte g, byte b, byte a = 255)
    {
        if (x < 0 || y < 0 || x >= S || y >= S) return;
        int i = (y * S + x) * 4;
        px[i] = r; px[i + 1] = g; px[i + 2] = b; px[i + 3] = a;
    }

    public void Rect(float x, float y, float w, float h, byte r, byte g, byte b)
    {
        int x0 = (int)(x * Esc), y0 = (int)(y * Esc);
        int x1 = Math.Min(S - 1, (int)((x + w) * Esc)), y1 = Math.Min(S - 1, (int)((y + h) * Esc));
        for (int yy = y0; yy <= y1; yy++)
            for (int xx = x0; xx <= x1; xx++)
                Set(_px, xx, yy, r, g, b);
    }

    public void Linea(float x0, float y0, float x1, float y1, float grosor, byte r, byte g, byte b)
    {
        int ix0 = (int)(x0 * Esc), iy0 = (int)(y0 * Esc), ix1 = (int)(x1 * Esc), iy1 = (int)(y1 * Esc);
        int radio = Math.Max(1, (int)(grosor * Esc / 2f));
        int dx = Math.Abs(ix1 - ix0), dy = Math.Abs(iy1 - iy0);
        int sx = ix0 < ix1 ? 1 : -1, sy = iy0 < iy1 ? 1 : -1;
        int err = dx - dy;
        int x = ix0, y = iy0;
        while (true)
        {
            for (int yy = -radio; yy <= radio; yy++)
                for (int xx = -radio; xx <= radio; xx++)
                    if (xx * xx + yy * yy <= radio * radio)
                        Set(_px, x + xx, y + yy, r, g, b);
            if (x == ix1 && y == iy1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }

    public void Elipse(float cx, float cy, float rx, float ry, byte r, byte g, byte b)
    {
        float ex = rx * Esc, ey = ry * Esc;
        int x0 = Math.Max(0, (int)((cx - rx) * Esc)), x1 = Math.Min(S - 1, (int)((cx + rx) * Esc));
        int y0 = Math.Max(0, (int)((cy - ry) * Esc)), y1 = Math.Min(S - 1, (int)((cy + ry) * Esc));
        for (int yy = y0; yy <= y1; yy++)
            for (int xx = x0; xx <= x1; xx++)
            {
                float nx = (xx + 0.5f - cx * Esc) / ex;
                float ny = (yy + 0.5f - cy * Esc) / ey;
                if (nx * nx + ny * ny <= 1f) Set(_px, xx, yy, r, g, b);
            }
    }

    /// <summary>PNG RGBA con transparencia (32x32 -> tamano real S x S).</summary>
    public byte[] Png()
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        EscribirChunk(ms, "IHDR", Ihdr());
        EscribirChunk(ms, "IDAT", Idat());
        EscribirChunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    byte[] Ihdr()
    {
        var b = new byte[13];
        b[0] = (byte)(S >> 24); b[1] = (byte)(S >> 16); b[2] = (byte)(S >> 8); b[3] = (byte)S;
        b[4] = (byte)(S >> 24); b[5] = (byte)(S >> 16); b[6] = (byte)(S >> 8); b[7] = (byte)S;
        b[8] = 8;   // bit depth
        b[9] = 6;   // color type: RGBA
        return b;
    }

    byte[] Idat()
    {
        // scanlines: filtro 0 + RGBA por fila
        var raw = new byte[S * (S * 4 + 1)];
        for (int y = 0; y < S; y++)
        {
            int off = y * (S * 4 + 1);
            raw[off] = 0;
            Array.Copy(_px, y * S * 4, raw, off + 1, S * 4);
        }
        using var def = new MemoryStream();
        using (var ds = new DeflateStream(def, CompressionLevel.Fastest, true))
            ds.Write(raw, 0, raw.Length);
        var deflate = def.ToArray();
        // zlib: cabecera 0x78 0x9C + deflate + adler32
        uint adler = Adler32(raw);
        var z = new byte[deflate.Length + 6];
        z[0] = 0x78; z[1] = 0x9C;
        Array.Copy(deflate, 0, z, 2, deflate.Length);
        z[z.Length - 4] = (byte)(adler >> 24);
        z[z.Length - 3] = (byte)(adler >> 16);
        z[z.Length - 2] = (byte)(adler >> 8);
        z[z.Length - 1] = (byte)adler;
        return z;
    }

    static uint Adler32(byte[] d)
    {
        uint a = 1, b = 0;
        foreach (var v in d) { a = (a + v) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }

    static readonly uint[] _crc = TablaCrc();
    static uint[] TablaCrc()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    static void EscribirChunk(MemoryStream ms, string tipo, byte[] datos)
    {
        int len = datos.Length;
        ms.WriteByte((byte)(len >> 24)); ms.WriteByte((byte)(len >> 16));
        ms.WriteByte((byte)(len >> 8)); ms.WriteByte((byte)len);
        var t = System.Text.Encoding.ASCII.GetBytes(tipo);
        ms.Write(t, 0, 4);
        ms.Write(datos, 0, datos.Length);
        uint crc = 0xFFFFFFFF;
        foreach (var v in t) crc = _crc[(crc ^ v) & 0xFF] ^ (crc >> 8);
        foreach (var v in datos) crc = _crc[(crc ^ v) & 0xFF] ^ (crc >> 8);
        crc ^= 0xFFFFFFFF;
        ms.WriteByte((byte)(crc >> 24)); ms.WriteByte((byte)(crc >> 16));
        ms.WriteByte((byte)(crc >> 8)); ms.WriteByte((byte)crc);
    }
}

/// <summary>Lienzo sobre ICanvas (hotbar del HUD y drops en el mundo). Escala el
/// diseno de 32x32 al rectangulo indicado.</summary>
public sealed class LienzoCanvas : ILienzoIcono
{
    readonly ICanvas _c;
    readonly RectF _r;
    readonly float _ex, _ey;

    public LienzoCanvas(ICanvas c, RectF r)
    {
        _c = c;
        _r = r;
        _ex = r.Width / 32f;
        _ey = r.Height / 32f;
    }

    public void Rect(float x, float y, float w, float h, byte r, byte g, byte b)
    {
        _c.FillColor = Color.FromRgb(r, g, b);
        _c.FillRectangle(_r.X + x * _ex, _r.Y + y * _ey, w * _ex, h * _ey);
    }

    public void Linea(float x0, float y0, float x1, float y1, float grosor, byte r, byte g, byte b)
    {
        _c.StrokeColor = Color.FromRgb(r, g, b);
        _c.StrokeSize = grosor * Math.Min(_ex, _ey);
        _c.DrawLine(_r.X + x0 * _ex, _r.Y + y0 * _ey, _r.X + x1 * _ex, _r.Y + y1 * _ey);
    }

    public void Elipse(float cx, float cy, float rx, float ry, byte r, byte g, byte b)
    {
        _c.FillColor = Color.FromRgb(r, g, b);
        _c.FillEllipse(_r.X + (cx - rx) * _ex, _r.Y + (cy - ry) * _ey, rx * 2 * _ex, ry * 2 * _ey);
    }
}
