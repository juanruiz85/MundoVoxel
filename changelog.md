# Changelog

Todas las etapas del proyecto se registran aquí. Formato basado en [Keep a Changelog](https://keepachangelog.com/es/1.1.0/).

## [0.9.0] - 2026-08-16

### Añadido (mecánicas nuevas)
- **Oxígeno y ahogamiento**: al estar bajo el agua se consume oxígeno (barra de burbujas en el HUD); al agotarse pierdes vida (2/s) hasta salir a la superficie o morir. Duración configurable en `ajustes.config.json` (`OxigenoMax`).
- **Lava**: nuevo bloque Lava (id 30), líquido, no sólido, no colocable ni rompible. `PonerLagosLava` genera lagos de lava en el subsuelo (excluyendo la zona del spawn). Caer en lava quema (4 de vida/s, configurable).
- **Muerte con causa + respawn manual**: al morir se muestra un panel con la causa ("se ahogó", "ardió en lava", "fue asesinado por...") y un botón **Reaparecer**; reapareces en el punto de aparición con vida completa conservando el inventario.
- **Modo espectador (tecla G)**: vuela, atraviesa bloques, no rompe/coloca/suelta; revierte al modo normal. También revive (limpia el estado de muerte) si estás muerto.
- **Drop direccional**: soltar un ítem (Q) lo lanza 1-3 bloques en la dirección de la mira según el pitch (hacia arriba/abajo).
- **Límite de pitch configurable**: `Ajustes.PitchLimite` (radianes, default 1.55) evita mirar completamente arriba/abajo en el cliente.
- **Hoja con probabilidades configurables**: el drop de hojas (plantón/manzana/palo) usa los porcentajes de `ajustes.config.json`.
- **Ajustes centralizados** (`ajustes.cs`): carga `ajustes.config.json` junto al ejecutable (oxígeno, daño de lava, ahogamiento, pitch, drop de hoja) con valores por defecto si el archivo no existe.
- **Punto de aparición seguro**: `ObtenerPuntoAparicion` busca en espiral (radio 0-7) una columna con suelo visible y **2 bloques de aire libres** (cuerpo + cabeza), evitando aparecer dentro de árboles o bajo el agua (causa de la vista negra al entrar a un mundo remoto desde Android).
- **Registro de mensajes nuevos en el protocolo**: `OxigenoMsg`, `MuerteInfo` y `ModoEspectador` registrados como `JsonDerivedType` (sin esto, `Enviar` lanzaba excepción al serializarlos).
- **Modo espectador revive** en `gameserver.cs`: `c.Muerto = false` al activarlo, para no quedar atascado muerto.
- **`docs/guia-de-pruebas.md`**: guía completa para probar en Windows y Android (suite automática, build, emulador, multijugador Windows+Android en el mismo mundo, comandos adb y solución de problemas).

### Probado
- **Multijugador en vivo**: servidor dedicado + cliente Windows + cliente Android (emulador) en el mismo mundo «MundoMultijugador». El log del servidor confirma las conexiones (`AnaPC se conectó`, `Bruno se conectó`, ambos entraron), el HUD de ambos clientes muestra **2 jugadores** y las entidades (jugador remoto, mobs con barra de vida) se ven en las dos pantallas; Windows renderiza el mundo 3D a ~215 FPS y Android a ~277 FPS. Se corrigió la **vista negra al entrar desde Android** (spawn dentro de árbol/agua → espiral con aire libre).
- **Suite automática: PRUEBAS SUPERADAS** (59+ comprobaciones): oxígeno (barra + daño), lava (líquido, no colocable, lagos fuera del spawn), espectador (no rompe/coloca, revive), muerte con causa + respawn con vida 20, soltar ítem direccional, trigo con agua, TNT, cofre inicial, hostiles de noche, minerales, multijugador y más.
- **Builds**: Windows `net10.0-windows10.0.19041.0` Debug **0 errores**; Android `net10.0-android` Release publish **exit 0**; APK `com.mundovoxel.app-Signed.apk` instalado con `adb install -r` (Success).

### Corregido
- **Vista negra del mundo en Android al entrar a un mundo remoto (causa raíz)**: el bloque **Lava (id 30)** quedó fuera de la paleta de colores del renderizador (`ColoresBase` tenía 30 entradas, índices 0–29). Cuando un lago de lava quedaba visible a la cámara, `RasterizarCara` calculaba un índice fuera de rango y lanzaba `IndexOutOfRangeException` en **cada frame**, por lo que el BMP del mundo nunca se pintaba (pantalla negra con HUD y etiquetas de entidades visibles). En Windows no se manifestó porque en la semilla de prueba la lava no quedaba expuesta; el mundo remoto de la prueba sí la tenía visible. Fix: se añadió el color de la lava a `ColoresBase` y `RasterizarCara` ahora clampea el id de bloque al tamaño de la paleta (defensa ante futuros bloques). Verificado en Android con mundo remoto: cielo y terreno visibles, **2 jugadores** (AnaPC en Windows + Bruno en Android) en el mismo mundo con sus etiquetas visibles en ambas pantallas.
- Diagnóstico: `Diag.Log` también escribe a **logcat** en Android (`adb logcat -s MVX`), y `RenderizadorVoxel` expone `NumMallas`.

### Conocido
- Al entrar a un **mundo remoto** desde Android con varios clientes activos, el render por software del emulador puede tardar en refrescar el frame del mundo (alterna entre mundo visible y negro unos segundos bajo carga); el mundo **local** en Android renderiza de inmediato.

## [0.8.2] - 2026-08-16

### Probado
- **Primera prueba real de la app en Android (emulador API 35)**: se instaló el APK Release en un emulador x86_64 (AVD `mvx` creado para la ocasión) y se verificó el flujo completo: menú principal → Jugar solo → crear mundo "MundoAndroid" → **mundo 3D renderizado a ~167 FPS** con HUD (10 corazones, hotbar 9 slots, crosshair, coordenadas), botones táctiles (Romper/Colocar/Saltar/Volar/Chat), una vaca con su barra de vida, chat de bienvenida, menú de pausa completo y **inventario/crafteo** (grid 2×2, resultado, cursor, sección Cocina con fundir oro/hierro/cobre). **Cero crashes** (logcat sin FATAL).
- Aprendizaje: el APK **Debug** de .NET MAUI no incluye los assemblies (usa *Fast Deployment* con `dotnet build -t:Run`) y **aborta con SIGABRT** si se instala con `adb install` directo. Para instalar con adb hay que publicar el APK **Release** (`dotnet publish -f net10.0-android -c Release`).
- Para reproducir: `emulator -avd mvx` + `adb install -r MundoVoxel.Client/bin/Release/net10.0-android/com.mundovoxel.app-Signed.apk`.

## [0.8.1] - 2026-08-15

### Corregido
- **Crash al entrar a un mundo (Windows y Android)**: `paginajuego.xaml` usaba `IsTabStop="False"` en 4 botones (menú ☰, reanudar, volar, distancia), una propiedad de WinUI que no existe en MAUI. Al navegar a la página de juego, MAUI lanzaba `XamlParseException` al parsear el XAML y la app se cerraba. Fix: la propiedad se quitó del XAML y ahora se aplica `IsTabStop = false` al botón nativo de WinUI vía `Handler.PlatformView` (solo Windows; en Android los botones no capturan la barra espaciadora del mismo modo).
- Verificado con UIA: **MUNDO CREADO Y PARTIDA INICIADA [OK]** (216 FPS, HUD completo, oveja con barra de vida) y build Android **0 errores**.

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
