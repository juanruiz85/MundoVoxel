# Changelog

Todas las etapas del proyecto se registran aquí. Formato basado en [Keep a Changelog](https://keepachangelog.com/es/1.1.0/).

## [0.3.0] - 2026-08-14

### Añadido
- **Mobs** (adaptación 3D de los 6 mobs de la referencia MinecraftJS): cerdo, vaca, oveja (pasivos, deambulan) y zombi, creeper y esqueleto wither (hostiles, persiguen al jugador en un radio de 11 bloques).
  - `MundoVoxel.Core/mobs.cs`: tipos, datos estáticos (tamaño, velocidad, hostilidad) y estado simulado.
  - `GameServer`: genera 9 mobs por mundo cerca del spawn y los simula/difunde a ~4 Hz (mensaje `Mobs`).
  - Cliente: los mobs se muestran como cajas de color con etiqueta (reutilizando el renderizador de cajas); colores distintivos por tipo.
  - Prueba automática añadida: se verifican mobs difundidos, posiciones válidas y variedad de tipos.
- **Compilación Android operativa**: JDK 17 (Microsoft OpenJDK), Android SDK (`platforms;android-36`, `build-tools;36.0.0`, `platform-tools`) y workload `maui-android` instalados; `dotnet build -f net10.0-android` genera el APK firmado (`com.mundovoxel.app-Signed.apk`).

## [0.2.1] - 2026-08-14

### Corregido
- **Pantalla de juego congelada al entrar al mundo**: el renderizador dibujaba miles de caras por frame con llamadas individuales a `ICanvas.FillPath` en el hilo de la UI, bloqueándola. Ahora:
  - Nuevo `Juego/rasterizador.cs`: rasterizador por software (relleno de triángulos con z-buffer sobre un buffer BGR), portable Windows/Android.
  - `RenderizadorVoxel.Rasterizar`: proyecta y rasteriza las caras en el buffer (dos pasadas: opacas y líquidas con alpha), sin llamadas nativas por cara.
  - El render se ejecuta en **segundo plano** (`Task.Run`) con una instantánea de la cámara y los jugadores; el hilo de la UI solo asigna el frame a un control `Image` (`ImageSource.FromStream` con el BMP), manteniendo el `GraphicsView` para gestos y HUD. Resultado: **~215 FPS sin bloqueos**.
- Botón «Crear mundo» duplicado y sin efecto: `BtnCrearConfirmar`/`BtnCancelar` no tenían `Clicked` conectado; se enlazaron los manejadores y el botón inferior pasó a «+ Nuevo mundo».
- Título de la ventana WinUI vacío: `Window{ Title="MundoVoxel" }` en `app.xaml.cs`.
- `paginajuego.xaml`: `IsHitTestVisible` (no existe en MAUI 10) → `InputTransparent`; anchos de chat con `%` inválidos → valores numéricos.

## [0.2.0] - 2026-08-13 (etapa 2)

### Añadido
- **Cliente .NET MAUI** (Windows + Android) en `MundoVoxel.Client`:
  - Menú principal en español: nombre de jugador, IP y puerto del servidor.
  - «Jugar solo»: arranca un servidor local incrustado y se conecta solo.
  - Lista de mundos del servidor con estados (público/privado, jugadores, creador).
  - Crear mundo (público o privado con clave de 4 dígitos) y borrar mundos propios.
  - Pantalla de juego 3D (renderizador propio sobre `GraphicsView`): terreno, agua, árboles, niebla, jugadores remotos.
  - Controles de escritorio (WASD, espacio, F, T, Esc, clic/doble clic, 1-9) y táctiles (joystick, botones).
  - Chat en vivo, HUD (coordenadas, FPS, barra de bloques), menú de pausa.
  - Textos 100 % desde `lang/es.lang` (editable).
- Documentación: `README.md`, `CHANGELOG.md`, `docs/ARQUITECTURA.md`, `docs/MANUAL_DE_USO.md`.
- Scripts de despliegue del servicio en `deploy/` (systemd y Windows).

### Corregido (compilación y arranque del cliente en .NET 10 / MAUI 10.0.20)
- `mundovoxel.client.csproj`: en .NET 8+ `UseMaui` ya no incluye los paquetes automáticamente; se añadió `Microsoft.Maui.Controls` (vía `$(MauiVersion)`) y `Microsoft.Extensions.Logging.Debug` (para `Logging.AddDebug`).
- `Platforms/Windows/app.xaml`: la raíz WinUI ahora es `<maui:MauiWinUIApplication>` (patrón de plantilla) en lugar de `<local:App xmlns:local="using:Microsoft.Maui">`, que rompía el compilador XAML (XamlCompiler, MSB3073).
- `Juego/renderizadorvoxel.cs`: adaptado a la API de MAUI 10 (`LinearGradientPaint` con `PaintGradientStop[]` + puntos; `PathF` sin `Clear()`, se recrea por cara).
- `app.xaml.cs`: la página raíz ya no se inyecta en el constructor (eso construía las páginas antes de cargar los recursos de `App.xaml` y crasheaba con `StaticResource no encontrado`); ahora se resuelve en `CreateWindow` (patrón recomendado en MAUI 10, además elimina el aviso de `MainPage` obsoleto).
- Registro de excepciones no controladas a `crash.log` junto al ejecutable (`mauiprogram.cs` y `Platforms/Windows/App.xaml.cs`), útil para diagnosticar fallos de arranque.

## [0.1.0] - 2026-08-13 (etapa 1)

### Añadido
- `MundoVoxel.Core` (biblioteca compartida):
  - Bloques (12 tipos), ruido de valor/FBM, generación procedural de mundos (64×40×64).
  - Raycaster por voxeles (Amanatides & Woo) para romper/colocar bloques.
  - Serialización y compresión (GZip) de mundos.
  - Protocolo de red JSON con discriminador de tipo y tramas con prefijo de longitud.
  - `GameServer`: mundos en memoria (hasta 40), públicos o privados con **clave de 4 dígitos**, hasta 12 jugadores por mundo; crear/unirse/salir/borrar mundos; romper/colocar con validación de distancia; chat; sincronización de posiciones a 10 Hz; el mundo vacío **permanece en memoria**.
- `MundoVoxel.Server`: aplicación de consola .NET 10 con `Microsoft.Extensions.Hosting`; se ejecuta como proceso normal, **servicio de Windows** (`AddWindowsService`) o **servicio systemd de Linux** (`AddSystemd`); configuración vía `appsettings.json`.
- `MundoVoxel.Pruebas`: prueba automática del protocolo (16 comprobaciones: conexión, mundos, clave incorrecta/correcta, bloques, chat, persistencia y borrado).

### Correcciones
- Serialización de posiciones: `System.Text.Json` no restaura `Vector3` (propiedades de solo lectura); se reemplazó por campos `float` (`Ax/Ay/Az`, `Px/Py/Pz`) en el protocolo.

## [No publicado]
- Portar el cliente a Linux cuando MAUI tenga soporte oficial (o backend comunitario).
- Persistencia opcional de mundos en disco.
- Supervivencia: salud, hambre, daño por caída.
- Ciclo día/noche e iluminación.
