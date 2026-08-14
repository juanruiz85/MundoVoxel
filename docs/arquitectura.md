# Arquitectura de MundoVoxel

## Visión general

```
┌─────────────────────────────┐        TCP (JSON)        ┌──────────────────────────┐
│  MundoVoxel.Client (MAUI)   │ ◄──────────────────────► │  MundoVoxel.Server        │
│  Windows / Android          │    puerto 25575         │  Windows / Linux (servicio)│
└─────────────────────────────┘                          └────────────┬─────────────┘
        │                                                              │ usa
        │ referencia                                                    ▼
        ▼                                                    ┌──────────────────────┐
┌─────────────────────────────┐                              │  MundoVoxel.Core     │
│  Servidor local incrustado  │◄── «Jugar solo» ────────────►│  GameServer + Mundo  │
└─────────────────────────────┘                              └──────────────────────┘
```

- **`MundoVoxel.Core`** no depende de ninguna UI: es la pieza que comparte el servidor dedicado, el servidor local incrustado y el cliente.
- El cliente «Jugar solo» levanta un `GameServer` en el mismo proceso (hilo en segundo plano) y se conecta por TCP a `127.0.0.1`, de modo que **solo y multijugador usan exactamente el mismo camino de red**.

## Módulos

### MundoVoxel.Core

| Archivo | Responsabilidad |
|---|---|
| `Bloques.cs` | Tabla de 12 bloques: solidez, transparencia, líquidos y clave `.lang` para el nombre. |
| `Ruido.cs` | Ruido de valor 2D + FBM (sin dependencias externas) para el terreno. |
| `Mundo.cs` | Matriz plana `ushort[]` (Ancho×Alto×Profundo), generación (terreno, agua, arena, árboles), serialización binaria + GZip, punto de aparición. |
| `Rayos.cs` | Raycaster voxel (Amanatides & Woo) con normal de la cara golpeada. |
| `Protocolo.cs` | Mensajes del protocolo (JSON polimórfico, discriminador `Tipo`) + tramas con prefijo de longitud (int32 LE). |
| `GameServer.cs` | Servidor: acepta TCP, gestiona mundos en memoria, valida acciones, difunde cambios y posiciones. |
| `ArchivoLang.cs` | Parser de archivos `.lang` (`clave=texto`, comentarios `#`, formato `{0}`). |

### Protocolo (mensajes principales)

| Mensaje | Dirección | Contenido |
|---|---|---|
| `Hola` / `Bienvenido` | C→S / S→C | Nombre del jugador; id asignado + nombre del servidor. |
| `ListarMundos` / `ListaMundos` | C→S / S→C | Catálogo: id, nombre, creador, abierto, jugadores/máx, `IdDueno`. |
| `CrearMundo` / `MundoCreado` | C→S / S→C | Nombre, `Abierto`, `Pin` (4 dígitos si es privado); id del mundo. |
| `Unirse` / `Unido` | C→S / S→C | Id del mundo + `Pin` opcional; devuelve el mundo completo comprimido (GZip) y el punto de aparición. |
| `Error` | S→C | Códigos: `PIN_INCORRECTO`, `LLENO`, `NO_EXISTE`, `NO_DUENO`, `PIN_INVALIDO`, `LIMITE_MUNDOS`, `MUNDO_BORRADO`. |
| `Salir` | C→S | Sale del mundo actual (el mundo queda en memoria). |
| `BorrarMundo` / `MundoBorrado` | C→S / S→C | Solo el creador; expulsa a los jugadores dentro. |
| `RomperBloque` / `ColocarBloque` / `BloqueCambio` | C→S / S→C | Coordenadas (+tipo al colocar); el servidor valida y difunde. |
| `Posicion` / `Posiciones` | C→S / S→C | Posición + orientación; el servidor reenvía el estado de todos (10 Hz). |
| `Chat` | C→S / S→C | Mensaje de chat difundido al mundo. |

### Validaciones del servidor (autoridad)
- Romper: dentro del mundo, distancia ≤ 7 bloques, no `Aire` ni `Lecho`.
- Colocar: dentro del mundo, `y > 0`, tipo colocable, destino vacío, distancia ≤ 7 y **no dentro del espacio de otro jugador**.
- Clave: solo mundos privados; debe coincidir exactamente con los 4 dígitos.
- Borrar: solo el `IdDueno` (la conexión que creó el mundo).

### Concurrencia
- Un `lock` global protege los diccionarios de mundos/conexiones; `ConcurrentDictionary` para las conexiones.
- Hilo lector por conexión (async), hilo de difusión de posiciones (10 Hz) y hilo de aceptación.
- Los envíos se serializan con `lock(conexion)` para evitar intercalado de tramas.

## Cliente (MAUI)

| Archivo | Responsabilidad |
|---|---|
| `Servicios/ServicioRed.cs` | Conexión TCP, hilo lector, cola de mensajes para la UI. |
| `Servicios/ServicioIdioma.cs` | Carga `lang/es.lang` (externa junto al ejecutable o incrustada) y traduce claves. |
| `Servicios/ServicioTeclado.cs` | Estado de teclas en Windows (eventos `KeyDown/KeyUp` del contenido de la ventana); códigos virtuales portables. |
| `Servicios/ServidorLocal.cs` | Arranca un `GameServer` en proceso para «Jugar solo». |
| `Juego/RenderizadorVoxel.cs` | Renderizador 3D por software sobre `GraphicsView`: mallas por chunk, caras expuestas, sombreado por cara, niebla por distancia, cielo degradado. |
| `Juego/VistaJuego.cs` | Lógica de partida: cámara, física (gravedad, colisiones AABB), raycast para bloques, jugadores remotos, joystick, HUD. |
| `Paginas/*` | Menú principal, lista de mundos y pantalla de juego (XAML + code-behind). |

### Flujo de datos del cliente
1. `PaginaMenu` → `ServicioRed.Conectar(ip, puerto)` → `Hola` → `Bienvenido` + `ListaMundos`.
2. `PaginaMundos` pinta la lista; al crear/unirse recibe `Unido` con el mundo comprimido.
3. `PaginaJuego` descomprime y construye el `Mundo` local + mallas; el bucle (≈30 fps):
   - procesa mensajes de red (`BloqueCambio` → re-malla el chunk afectado; `Posiciones` → actualiza jugadores remotos; `Chat` → línea en el chat),
   - actualiza física/entrada y envía `Posicion` cada 100 ms,
   - invalida el `GraphicsView` para redibujar.
4. Romper/colocar se envía al servidor y se aplica **cuando llega** el `BloqueCambio` (el servidor es la autoridad).

## Límites actuales (diseño deliberado)
- Los mundos viven **solo en memoria**: reiniciar el servidor los pierde. El creador decide si borrarlos o conservarlos.
- El mundo completo se transmite al entrar (64×40×64 ≈ 327 KB sin comprimir, ~60-100 KB comprimido): suficiente para LAN; para internet de alta latencia convendría *chunk streaming*.
- El renderizado es por software (canvas 2D con painter's algorithm): prioriza portabilidad; para mundos enormes convendría OpenGL/OpenGL ES (p. ej. Silk.NET).

## Próximos pasos

1. **Etapa 3 — Pulido del cliente**: verificar compilación MAUI en esta máquina (workload), corregir cualquier error de build, probar en Windows.
2. Persistencia opcional de mundos en disco (JSON/binary por mundo, con guardado al borrar/salir).
3. Modo supervivencia (salud, hambre, daño de caída) y antigravedad para el agua.
4. Día/noche, iluminación por bloques y biomas (desierto, nieve).
5. Chunk streaming + mundos más grandes (128³ o infinitos por regiones).
6. Interpolación de jugadores remotos (hoy la posición se muestra tal cual llega).
7. Anti-cheat básico (validar velocidad/teletransportes) y moderación de chat.
8. Portar el cliente a Linux cuando MAUI lo soporte oficialmente.
9. Publicar APK y paquetes (MSIX/instalador Windows) con CI.
