# Changelog

Todas las etapas del proyecto se registran aquí. Formato basado en [Keep a Changelog](https://keepachangelog.com/es/1.1.0/).

## [0.8.0] - 2026-08-15

### Añadido
- **Controles de ratón completos**: clic izquierdo = romper bloque / atacar mob, clic derecho = colocar bloque de la mano (manejador nativo de puntero, sin interferir con el toque).
- **Sensibilidad del ratón configurable**: deslizador en el menú de pausa (0.25×–3×), guardado entre sesiones (`Preferences`).
- **Mobs de día/noche**: zombis y esqueletos solo aparecen de noche; de día el mundo solo genera pasivos. Los hostiles expuestos al sol se queman (−3 de vida/s) y mueren soltando drops; los creepers no se queman.
- **Creeper que explota al atacar**: radio de explosión configurable en `mobs.config.json` (0 = sin destrucción, default 3); si hay agua cerca (radio 3), la explosión no rompe bloques (solo daña).
- **Cofre inicial en el spawn**: un cofre con 5 herramientas de piedra (pico, hacha, espada, pala, azada) rodeado por 4 antorchas en diagonal. Se puede **craftear cofres** (8 tablones) y **almacenar ítems** (27 slots del cofre + 27 del inventario, mover de 1 en 1 con el cursor).
- **Agua y natación**: al estar en agua la gravedad baja y el espacio hace flotar; los cultivos (trigo y plantones) crecen casi el doble de rápido con agua cerca (radio 3).
- **Fix tecla Espacio vs menú**: el botón ☰ del menú ya no recibe foco (no se activa con Espacio); solo responde al clic.
- **Sección de IA en el README**: nueva sección "Uso de IA en el desarrollo" con el total de tokens usados, prompts, respuestas generadas, modelos (zai_auto, DeepSeek V4 Flash, GLM-5 Turbo), agentes, costo real ($0, ZAI sin cargo) y costo estimado a tarifas de mercado. Se actualiza en cada commit con el script `docs/actualizar-stats-ia.ps1` (lee los archivos de sesión del gateway AutoClaw/OpenClaw).

### Corregido
- **Salto siempre activo**: el estado `EnSuelo` se borraba al inicio del frame y el salto se comprobaba antes de `Mover`; ahora se conserva el estado del frame anterior y el salto funciona correctamente.
- **Cofre inicial fuera del punto de aparición**: el cofre se colocaba en la celda exacta del spawn; ahora se coloca UNA celda al lado y sus 4 antorchas van en diagonal (sin estorbar la construcción cercana).
- **`HoraInicial = 0` no era medianoche**: el centinela `> 0 ? Hora : 8` trataba 0 como "sin especificar"; el default ahora es `-1` y 0 es medianoche válida. La suite usa hora 0 para verificar que los hostiles salen de noche.
- **Prueba de soltar item (Q) flaky**: la explosión de la TNT deja drops que ensucian la cola de `Inventario`; la prueba ahora lee hasta ver el valor esperado (madera 9 → 10).
- **Suite completa: PRUEBAS SUPERADAS (59 comprobaciones)**.

## [0.7.0] - 2026-08-15

### Anadido (paquete estilo Minecraft Indev)
- **Soltar items (tecla Q)**: quita 1 del slot seleccionado y crea un drop frente al jugador que se puede recoger.
- **Hotbar dinamica + herramientas en mano**: la hotbar muestra los primeros 9 slots del inventario; la herramienta seleccionada (pico/espada/hacha/pala/azada) se dibuja como figura voxel en la mano (mango de palo + cabeza del color del material). La seleccion viaja con el material (mensaje `SeleccionarSlot { Slot, Material }`) para que el servidor valide lo que hay en la mano.
- **Arboles -> plantones/manzanas/palos**: al romper hojas caen con probabilidad (10% planton, 6% manzana, 12% palo); el cesped puede soltar semillas de trigo.
- **Trigo + azada**: la azada labra tierra/cesped (`TierraLabrada`), las semillas se plantan y el trigo crece en 4 etapas hasta madurar; cosecharlo da trigo + semillas. El planton crece hasta convertirse en arbol.
- **TNT + mechero**: receta (lingote de hierro + piedra); el mechero enciende la TNT (cuenta 3 s) y explota (radio 3.5, destruye bloques con 30% de drops, daña jugadores y mobs).
- **Fundicion con combustible**: 8 recetas de cocina (3 carnes + oro/hierro/cobre/diamante en bruto -> lingotes/diamante + arena -> cristal); fundir minerales requiere 1 carbon como combustible (error `SIN_CARBON`).
- **Herramientas de 6 materiales**: 35 recetas de crafteo (pico/espada/hacha/pala/azada en madera, piedra, cobre, hierro, oro y diamante).
- **Vida del jugador + ataque hostil configurable**: el jugador tiene 20 de salud (HUD de corazones); los mobs hostiles golpean de cerca (cooldown 1 s, daño por tipo) y al morir reapareces en el spawn conservando el inventario. `mobs.config.json` junto al ejecutable permite ajustar tamano, hostilidad, velocidad, area de agresion y daño por mob sin recompilar.
- **Ciclo dia/noche + antorchas**: 24 h en ~5 minutos; el render baja el brillo de noche (cielo azulado nocturno). Antorcha = palo + carbon (bloque 27) y TNT (26) ya son colocables con sus colores.
- **Kit inicial**: al entrar a un mundo por primera vez recibes madera, tierra, piedra, arena, palos, antorchas, semillas de trigo y un mechero para poder construir desde el primer momento.
- **Barras de vida sobre los mobs** y etiquetas con su nombre.
- `CrearMundo` acepta una **semilla opcional** para mundos reproducibles.

### Corregido
- **Drops de mob con varios items**: la recogida enviaba un `Inventario` por cada drop; el cliente podia leer un inventario intermedio sin todos los drops. Ahora se envia un unico inventario por jugador tras recoger todo el lote.
- **TNT no desaparecia al explotar**: la TNT central se quedaba como bloque (solo se difundia el aire de los bloques alrededor). Ahora se consume y difunde su `BloqueCambio`.
- **Pruebas**: el mundo privado de la suite usa semilla fija (terreno determinista); las posiciones de mineria se apartan de los jugadores (el servidor rechaza colocar bloques encima de un jugador) y la explosion de TNT se espera leyendo toda la rafaga de cambios. Suite completa: **PRUEBAS SUPERADAS (34 comprobaciones)**.

## [0.6.0] - 2026-08-15

### Anadido
- **Inventario y crafteo tipo Minecraft**: panel con cuadricula de crafteo 2x2/3x3 (la mesa de trabajo cerca amplia a 3x3), boton de resultado, inventario 3x9 y cursor de items (clic para coger/soltar/apilar). El juego se pausa al abrirlo (tecla E o menu).
- **Sistema de mobs extensible** (`mobsdef.cs`): cada mob se define con diseno voxel (capas ASCII + paleta de colores) + datos de comportamiento; anadir un mob nuevo = 1 entrada en el enum + su diseno + su fila en `MobsInfo.Datos` + su botin. Los mobs ya se ven como figuras (cuerpo, cabeza, patas) en vez de cuadros de color, rotan segun su orientacion y tienen tamano Minecraft.
- **Mapa mas grande**: 128x48x128 (antes 64x40x64) con las mismas FPS (~208) gracias al render por chunks.
- **Minerales** (como MinecraftJS): carbon (16), hierro (17), oro (18) y diamante (19) con colores de las texturas de referencia; vetas por profundidad (carbon comun y superficial, diamante raro y profundo) que solo reemplazan piedra; requieren pico para soltar su bloque.

### Corregido
- **Bug del "brinco"**: al aterrizar la fisica usaba la coordenada de la cabeza en vez del pie (y rebotaba ~2 bloques en bucle) y `EnSuelo` nunca se reseteaba. Ahora el jugador se apoya correctamente en el suelo.
- **Drops**: los recoge el jugador mas cercano (antes el primero del diccionario podia robar el drop de otro).
- Pruebas: dimensiones del mundo y posiciones de mobs actualizadas al nuevo tamano; nuevas pruebas de minerales (los 4 tipos presentes bajo tierra).

## [0.5.0] - 2026-08-14

### Añadido (crafteos estilo Minecraft)
- **Recetas clásicas (Indev)** con los materiales disponibles:
  - Madera → 4 tablones (1 tronco = 4 tablones).
  - 2 tablones → 4 palos (vertical).
  - 4 tablones → mesa de trabajo (2×2).
  - 8 piedra → horno (anillo 3×3).
  - 4 arena → arenisca (2×2).
  - **Herramientas de madera y piedra**: pico, espada, hacha, pala y azada, con sus combinaciones correctas (p. ej. pico = 3 material + 2 palos, espada = 2 material + 1 palo).
- **Bloques nuevos**: Mesa de trabajo (14) y Arenisca (15).
- **Ítems nuevos**: 10 herramientas.
- **Mecánicas**: la piedra requiere un **pico** para soltar su bloque; la **espada** aumenta el daño a mobs (+2 madera, +4 piedra).
- Panel de inventario con botones de recetas generados dinámicamente.

## [0.4.0] - 2026-08-14

### Añadido (mecánicas de supervivencia del JS)
- **Combate y drops**: el jugador golpea mobs (la acción de romper ataca al mob bajo la mira); al morir, el mob **suelta ítems** (botín por tipo) que aparecen en el suelo y se recogen al pasar.
- **Inventario** por jugador (autoritativo en el servidor): romper bloques los mete al inventario; panel en el cliente (tecla `E`) con lista de ítems.
- **Crafteo**: madera → 4 tablones, 2 tablones → 4 palos, 4 tablones → horno. Bloques nuevos: `Tablones` (12) y `Horno` (13).
- **Cocina**: carne cruda → cocinada (cerdo/vaca/oveja) si hay un horno colocado cerca (mensaje `SIN_HORNO` en caso contrario).
- Protocolo nuevo: `GolpearMob`, `Drops`, `Inventario`, `Craftear`, `Cocinar`.

### Verificado
- **Servidor como servicio de Windows**: `sc create MundoVoxelServer` → RUNNING, escucha en 0.0.0.0:25575 y sirve clientes reales (`Bienvenido` + `ListaMundos`). `appsettings.json` ahora se carga desde el directorio del ejecutable (en un servicio el CWD es System32).
- **systemd** (Linux): unidad `deploy/mundovoxel-server.service` + `AddSystemd` validados (no ejecutable en esta máquina).
- **Pruebas automáticas**: 27 comprobaciones (multijugador, mobs, romper→inventario, crafteo, cocina, drops).

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
