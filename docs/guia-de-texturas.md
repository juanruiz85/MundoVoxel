# Guía de texturas e iconos — MundoVoxel

MundoVoxel usa un **renderizador por software** (sin GPU): no hay archivos de
imagen (PNG/JPG) para las texturas. Todo lo que se ve se genera **por código**
con tres capas. Esta guía explica cada capa, dónde se edita y cómo añadir
nuevos ítems.

## Resumen rápido

| Qué se ve | Dónde se define | Archivo |
|---|---|---|
| Color de cada bloque en el mundo | `ColoresBase` (paleta por bloque) | `MundoVoxel.Client/Juego/renderizadorvoxel.cs` |
| Detalles del bloque en el mundo (manchas de mena, vetas, franjas, bocas) | `AgregarDetalle()` + `AgregarManchas()` | `MundoVoxel.Client/Juego/renderizadorvoxel.cs` |
| Formas especiales en el mundo (antorcha, cofre, mesa, plantas) | `AgregarAntorcha/Cofre/Mesa/Planta()` | `MundoVoxel.Client/Juego/chunkmalla.cs` |
| Diseño de cada ítem (hotbar, inventario, cofre, drops) | `IconosItems.Dibujar()` | `MundoVoxel.Client/Juego/iconositems.cs` |
| Colores de referencia de los ítems | `Objetos.Color()` | `MundoVoxel.Core/objetos.cs` |

## 1. Color de los bloques en el mundo

`RenderizadorVoxel.ColoresBase` es un array de tuplas `(r, g, b)` indexado por
el id del bloque (`Bloques.Tierra = 1`, `Bloques.Piedra = 2`, ...). Cada cara
recibe ese color multiplicado por el sombreado de la dirección (arriba más
claro, abajo más oscuro) y el brillo global del día/noche, y mezclado con la
niebla según la distancia.

Para cambiar el color de un bloque: editar la tupla correspondiente en
`ColoresBase`. **Ojo con los minerales**: su color base es el de la PIEDRA
(122,118,112 aprox.) porque la apariencia de "mena" la dan las manchas del
punto siguiente.

## 2. Detalles procedurales sobre las caras (texturas del mundo)

`AgregarDetalle()` se ejecuta al proyectar cada cara y añade pequeños
cuadriláteros sobre la superficie (proyectados con el mismo z-buffer, así que
respetan la oclusión):

- **Minerales** (`Bloques.EsMineral`): 4 manchas del color del metal por cara,
  en posiciones deterministas (`Hash3(x,y,z)`) — el mismo bloque siempre tiene
  las mismas manchas. Solo se dibujan si la cara ocupa suficiente área en
  pantalla (`areaPx >= 200`).
- **Tronco**: vetas verticales oscuras en los lados, duramen claro arriba.
- **Tablones**: vetas horizontales.
- **TNT**: franja blanca horizontal.
- **Horno**: boca oscura con marco en la cara +Z.

Para añadir un detalle nuevo: añadir un `case` en `AgregarDetalle()` y
llamar a `Mancha(orig, u, v, centroU, centroV, tamU, tamV, colorARGB, ...)`.
Las coordenadas `u/v` van de 0 a 1 dentro de la cara.

## 3. Formas especiales (no son cubos)

En `ChunkMalla.Reconstruir()` algunos bloques no generan el cubo completo:

- **Antorcha**: poste delgado + llama de dos planos cruzados (colores
  `emisivos`: siempre brillan, incluso de noche). Las partículas animadas se
  dibujan aparte en `VistaJuego.DibujarAntorchas()`.
- **Cofre**: caja + tapa sobresaliente + cerradura metálica. **Todas las
  caras se generan siempre** (el z-buffer oculta las que no se ven) — así no
  quedan transparencias.
- **Mesa de trabajo**: tablero grueso + 4 patas.
- **Trigo / plantón**: dos planos cruzados verdes/amarillos cuya altura
  depende del estado de crecimiento.

Para añadir una forma nueva: crear un método `AgregarXxx(caras, x, y, z)` que
llame a `AgregarCara(caras, esquinaA, esquinaB, esquinaC, esquinaD, bloque,
dir, colorARGB)` con los 4 vértices 3D de cada cara, y añadir el `case` en el
bucle de `Reconstruir`. Con `ColorArgb` se fija un color propio (recibe
sombra y niebla); con `emisivo: true` el color no se oscurece nunca.

## 4. Iconos de los ítems (hotbar, inventario, cofre y drops)

`IconosItems.Dibujar(ILienzoIcono l, ushort material)` dibuja el **diseño** de
cada ítem con primitivas sobre un lienzo lógico de **32×32** (coordenadas 0-31).
El mismo diseño se usa en tres sitios:

| Sitio | Lienzo | Cómo se pinta |
|---|---|---|
| Hotbar (HUD) | `LienzoCanvas` sobre `ICanvas` | directo en `VistaJuego.DibujarHud()` |
| Slots del inventario y cofre | `LienzoRaster` → PNG con transparencia → `ImageSource` del botón | `PintarSlot()` + `IconosItemsCache.De(material)` |
| Drops en el mundo | `LienzoCanvas` sobre `ICanvas` | billboard flotante en `VistaJuego.DibujarDrops()` |

### Cómo editar un icono

Abrir `iconositems.cs` y buscar el `case` del material en `Dibujar()`. Los
métodos disponibles en el lienzo:

- `l.Rect(x, y, w, h, r, g, b)` — rectángulo relleno.
- `l.Linea(x0, y0, x1, y1, grosor, r, g, b)` — línea (el mango de las
  herramientas es una línea diagonal gruesa).
- `l.Elipse(cx, cy, rx, ry, r, g, b)` — elipse rellena.

Todo en coordenadas 0-31. Ejemplo: cambiar el color del mango de todas las
herramientas → editar la llamada `l.Linea(11, 24, 24, 10, 3.5f, 150, 110, 70)`
en `Herramienta()`.

### Cómo añadir un ítem nuevo (checklist completa)

1. **Id**: añadir el id en `Bloques` (si es bloque, `bloques.cs`) o en
   `ItemId` (si es ítem, `objetos.cs`).
2. **Nombre**: `Objetos.Nombre()` en `objetos.cs`.
3. **Color de referencia**: `Objetos.Color()` en `objetos.cs` (hotbar antigua
   y fallbacks).
4. **Si es bloque**: color en `ColoresBase` (renderizador) y, si aplica,
   detalles en `AgregarDetalle()` o forma en `chunkmalla.cs`.
5. **Icono**: `case` en `IconosItems.Dibujar()` con su diseño.
6. **Receta** (opcional): `Recetas()` en `objetos.cs`.
7. Compilar: `& "C:\Program Files\dotnet\dotnet.exe" build
   MundoVoxel.Client\MundoVoxel.Client.csproj -f net10.0-windows10.0.19041.0`
   (usar la ruta completa: el `dotnet` del PATH de AutoClaw no tiene SDK).

### Notas técnicas del PNG de los slots

`LienzoRaster.Png()` es un encoder PNG mínimo escrito a mano (firma + IHDR +
IDAT con zlib/DeflateStream + adler32 + CRC32 + IEND) que produce un PNG RGBA
de 32×32 con **transparencia**, cacheado por material en
`IconosItemsCache`. Los slots (`Button` MAUI) muestran el icono arriba y la
cantidad debajo (`ContentLayout = ImagePosition.Top`).

## 5. Colores de los ítems (referencia)

`Objetos.Color(material)` devuelve el color "canónico" de cada ítem/bloque
(los drops antiguos y el HUD lo usaban como color plano). Hoy sigue siendo
útil como color de fallback para materiales sin diseño específico.

## 6. Limitaciones conocidas

- El rasterizador por software no interpola texturas de imagen por píxel: los
  "detalles" son cuadriláteros planos sobre la cara.
- Los iconos son 32×32 lógicos (el PNG de los slots sale a 32×32 reales);
  dibujar detalles más finos de 1×1 unidad no se aprecia.
- Los billboards (drops, nombres, fuego) no tienen z-test: se dibujan sobre
  todo lo que esté detrás en pantalla.
