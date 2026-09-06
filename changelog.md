# Changelog

Todas las etapas del proyecto se registran aquí. Formato basado en [Keep a Changelog](https://keepachangelog.com/es/1.1.0/).

## [0.10.8] - 2026-09-05

### Agregado (servidores favoritos y reconexion automatica)
- **Servidores favoritos**: en el menu se guarda el servidor actual (alias + IP + puerto), se listan los persistidos y se conecta con un clic. La lista vive en `%LOCALAPPDATA%\MundoVoxel\servidores.json` (JSON, tope de 20, sin duplicados por IP:puerto; un archivo danado se trata como lista vacia en vez de romper el menu). Logica en `MundoVoxel.Core/servidoresfavoritos.cs` con rutas inyectables para poder probarla.
- **Reconexion automatica en plena partida**: si se pierde la conexion, el cliente reintenta al mismo servidor con retroceso exponencial (2 s, 4 s, 8 s... hasta 5 intentos) mostrando un panel con el intento en curso y un boton de cancelar. Al reconectar reenvia `Hola`, espera `ListaMundos`, vuelve a unirse al mismo mundo por el camino normal (recordando su Id y la clave si era privado) y reconstruye el mundo local con el `Unido` nuevo. Si el mundo ya no existe avisa; si se agotan los intentos vuelve al menu con el motivo. Todo en el cliente (`Servicios/servicioreconexion.cs`), sin cambios de protocolo ni del servidor.

### Probado
- Suite automatica: PRUEBAS SUPERADAS (91 comprobaciones; +8 de favoritos: persistencia, dedup por direccion, alias actualizado, tope de 20 y archivo danado).
- Builds con 0 errores: cliente Windows, cliente Android; Core y Pruebas.

### Documentacion
- Las estadisticas de IA del readme ahora incluyen el consumo del **ZCode CLI** (87 llamadas; DeepSeek V4 Pro y ruteo auto), que el gateway no ve, tomado de `~/.zcode/cli/rollout/model-io-*.jsonl`.

## [0.10.7] - 2026-09-05

### Agregado (mineria por golpes: hacha y pala con funcion real)
- **Los bloques duros requieren varios golpes**, validado por el servidor sin cambios de protocolo ni del cliente (el cliente ya golpea cada 250 ms al mantener pulsado): piedra/menas/ladrillo/horno/arenisca 5 golpes a mano, **2 con pico de madera/piedra/cobre/oro y 1 con pico de hierro o diamante**; madera y tablones 3 golpes a mano, **1 con cualquier hacha**; cofre y mesa 2 a mano (1 con hacha); tierra/cesped/tierra labrada/arena/nieve/grava 2 a mano, **1 con cualquier pala**. Plantas, hojas, TNT y el resto siguen rompiendose al primer golpe.
- Basta con TENER la herramienta correcta en el inventario (mismo criterio que los drops desde 0.10.5). El progreso de golpes se reinicia si cambia el bloque o pasan 2 s sin golpear.
- Con esto las cinco herramientas tienen funcion real: **pico** (minar piedra y menas, y romperlas mas rapido), **espada** (el golpe de mano hace 5 y las espadas suman +2 madera, +4 piedra, +5 cobre, +6 hierro, +8 diamante), **azada** (labrar tierra para sembrar), **hacha** (madera y muebles al primer golpe) y **pala** (tierra/arena/nieve/grava al primer golpe).

### Seguridad (auditoria completa en docs/auditoria-seguridad.md)
- **Anti-autoclick**: los golpes a mobs dentro de 250 ms se ignoran (antes un cliente modificado podia drenar la salud de un mob con cientos de mensajes por segundo).
- **Fuerza bruta de clave**: maximo 5 claves erradas por minuto por conexion en mundos privados (error MUCHOS_INTENTOS); el contador se reinicia al acertar o tras 60 s.

### Corregido (suite)
- Los puntos de rotura usan un nuevo helper RomperHasta (golpes hasta que cae el bloque): la suite ya no depende de que todo se rompa al primer golpe.
- Los 5 fallos de la primera corrida de esta sesion fueron intermitentes (flaky), no una regresion: verificado con corridas A/B (HEAD verde y HEAD con los cambios tambien verde).

### Verificado
- Suite: PRUEBAS SUPERADAS (82 comprobaciones; 12 nuevas: tabla de golpes, crafteo y efecto real de hacha y pala).
- Builds con 0 errores: Core, Pruebas, cliente Windows, cliente Android (net10.0-android); servidor linux-x64 publish exit 0.

## [0.10.6] - 2026-09-04

### Corregido (tests de la suite)
- **Los 5 tests intermitentes de 0.10.5 quedan robustos** (eran fallos de los tests, no del juego):
  - **Trigo (2)**: la espera de maduración pasa a tope por reloj real (5 min) en vez de 300 lecturas × 700 ms (~210 s), que en corridas cargadas se agotaba antes de ver `Trigo3`.
  - **Soltar con Q (1)**: el servidor solo envía `Inventario` tras un evento, así que leer uno "fresco" a secas devolvía null y el test se quedaba sin slot; ahora se fuerza un inventario actual con un crafteo barato (madera → tablones) antes de elegir el slot, y el mensaje de fallo detalla slot/material/cantidades.
  - **TNT del cofre (2)**: el slot ya no se supone el 6; se busca la TNT en el `CofreAbierto` recién abierto (si se extrae una herramienta, los slots se desplazan).

### Añadido
- **Interpolación de jugadores remotos**: la posición de los demás jugadores ya no salta con cada mensaje `Posiciones` (10 Hz); el cliente interpola hacia la última posición recibida con suavizado exponencial (~150 ms de convergencia) y aplica de golpe los saltos grandes (>8 bloques, teletransporte/respawn).
- **Anti-cheat de movimiento (opt-in)**: `AntiCheatSaltoMax` (bloques máximos entre mensajes de posición, caza teletransportes) y `AntiCheatVelocidadMax` (bloques/segundo, caza speedhack) en `ajustes.config.json`; 0 = desactivado (por defecto, como hasta ahora). Con los límites activos, el salto imposible se ignora y el servidor conserva la posición anterior (las acciones se validan por distancia contra la posición aceptada).
- **Moderación básica de chat**: se quitan caracteres de control (saltos de línea que falsearían el historial), se mantiene el tope de 200 caracteres y se añade anti-flood (más de 10 mensajes en 3 s se ignoran).
- **3 tests nuevos**: teletransporte ignorado con anti-cheat activo, movimiento normal aceptado, y limpieza de caracteres de control en el chat.

### Actualizado
- `readme.md` y `docs/arquitectura.md`: las ideas "a corto plazo" ya hechas (persistencia en disco, inventario/supervivencia, día/noche/biomas) se marcan como tales y quedan solo las pendientes; se corrigen las menciones a "mundos solo en memoria" y el conteo de comprobaciones (71).
- `docs/guia-de-pruebas.md`: nota del entorno recortado (sin `ProgramFiles(x86)`/`ProgramData` el restore de NuGet falla con `Value cannot be null (Parameter 'path1')`) y tiempo estimado de la suite actualizado.

### Probado
- Suite automática: **PRUEBAS SUPERADAS** — 2 corridas verdes consecutivas tras los ajustes de los tests (67/67) y una tercera con los tests nuevos de anti-cheat y moderación (70/70; 71 puntos de comprobación contando uno condicional).
- Builds: Core/Pruebas **0 errores**; cliente Windows `net10.0-windows10.0.19041.0` **0 errores** (8 avisos preexistentes).

## [0.10.5] - 2026-08-31

### Corregido (survival: consumo de items, minerales y transparencias)
- **Los bloques colocados son ilimitados**: `Colocar` no descontaba el bloque del inventario al colocarlo. Ahora el servidor **verifica que tienes el bloque** (anti-cheat) y **descuenta 1** al colocar, notificando el inventario actualizado al cliente. Verificado en la suite: colocar tierra 10 → 9.
- **El oro (y otros minerales) no se guardaban al romperlos**: el drop solo salia si el pico estaba **seleccionado en la mano** — con otro item en mano el oro se rompia sin soltar nada. Ahora basta con **tener un pico en el inventario** (aunque no este seleccionado).
- **Transparencias junto al cofre y la mesa**: estaban marcados como opacos aunque **no llenan la celda** (el cofre es 0.06-0.94, la mesa tiene patas) — los vecinos no generaban sus caras contra el hueco. Ahora son **transparentes al render** (siguen solidos para la colision): los vecinos generan sus caras y el z-buffer oculta lo que no se ve.
- **Soltar items (Q) imposible**: el drop caia a 2 bloques y el radio de auto-recogida es 2.5 — el item se volvia a recoger al instante y no se podia soltar nada. Ahora el item cae a **3 bloques** (fuera del radio de auto-recogida).
- **Borrado de mundos fantasma**: el botón de borrar de la lista ya elimina también el archivo `.mundo` del disco (antes el mundo borrado reaparecia al reabrir).

### Añadido
- **Biomas** (desierto y tundra): un segundo ruido de baja frecuencia clasifica cada columna al generar — **desierto** (arena, cactus de 1-3 bloques, sin arboles), **tundra** (capa de **nieve**, bloque nuevo id 33, sin arboles) y llanura/bosque normal. Bloques nuevos: **Nieve** (33) y **Cactus** (34) con color, icono e idioma.
- **TNT en el cofre inicial** (2 unidades) junto a las herramientas de piedra y lingotes de hierro.
- **6 tests nuevos en la suite**: consumo al colocar (10→9), anti-cheat al colocar sin tenerlo, picar piedra natural con pico en el inventario, "sin pico la piedra rompida no se guarda", picar carbón/oro natural con teletransporte a la veta, fundir oro → lingote, y TNT del cofre.

### Estado de la suite (76 de 81 verdes)
- **PRUEBAS SUPERADAS con 5 fallos conocidos**, todos de los tests nuevos pendientes de ajuste (no del juego): el trigo tarda más en madurar de lo que el test espera (2 fallos), el test de soltar con Q busca slot y choca con el item soltado (1), y la TNT del cofre se movio de slot al extraer el pico de piedra en el test del cofre (2 fallos). Se ajustan en el siguiente commit.

## [0.10.4] - 2026-08-30

### Corregido
- **"Atraviesa bloques" / bloques invisibles**: era el mismo bug de los items fantasma — los items no-colocables se colocaban como bloques con id invalido que no eran solidos (se atravesaban) y se pintaban negros. Arreglado con `EsColocable`. Ademas se añadio un **anti-atasco**: si por lag o al salir del modo espectador el jugador queda dentro de un solido, se empuja hacia arriba hasta quedar libre (antes se quedaba encerrado o atravesando la pared).
- **Cofre e inventario desincronizados**: el panel del cofre usaba un cursor local que el servidor no conocia — al mover items se duplicaban o desaparecian. Rediseñado sin cursor: **clic izquierdo mueve todo el stack** y **clic derecho mueve 1**, directo entre inventario y cofre en ambas direcciones (el servidor valida y responde con el estado nuevo de ambos paneles). El mensaje `SacarDeCofre` ahora lleva `Cantidad`.
- **"Cuadro negro" al colocar items**: `EsColocable` aceptaba items que no son bloques (palo, pico, semillas... id >= 1000): el servidor los colocaba y el renderizador clampeaba su color al ultimo de la paleta (casi negro). Ahora solo los bloques reales (`id < Info.Length`) son colocables, y el cliente ni siquiera envía la colocación con un item no colocable (las semillas se plantan con la tecla U, por `UsarBloque`, sin cambios).
- **Transparencias en cofre y mesa**: la malla omitía sus caras contra vecinos opacos y quedaban huecos. Ahora se generan TODAS las caras de la forma (el z-buffer oculta las que no se ven).

### Añadido (texturas e iconos procedurales, pedidos por el usuario)
- **Persistencia de mundos en disco**: los mundos ya no se pierden al cerrar el juego. Al crear un mundo y al cerrar/suspender la app (`App.OnSleep`) se guardan en `%LOCALAPPDATA%\MundoVoxel\mundos\*.mundo` (metadata del mundo + bloques comprimidos en gzip + hora del día + inventarios persistidos por jugador). Al arrancar, el servidor local **carga los mundos guardados** y aparecen en la lista; al volver a entrar se **restaura el inventario** que tenías (en vez de dar el kit de nuevo). El servidor dedicado también carga al arrancar y guarda al apagar (StopAsync).
- **Lanzador `lanzar-mundovoxel.bat`**: el `dotnet` del PATH de AutoClaw (solo runtime 8.0) pisa al SDK del sistema y el exe ya no encuentra el runtime 10 al hacer doble clic. El lanzador fija `DOTNET_ROOT` y arranca el juego; documentado en el README (sección «Cliente MAUI (Windows)»).
- **Biomas** (desierto y tundra): un segundo ruido de baja frecuencia clasifica cada columna — las zonas de **desierto** tienen arena en superficie, **cactus** de 1-3 bloques y sin árboles; las de **tundra** llevan una capa de **nieve** (bloque nuevo, id 33, blanco) y sin árboles; el resto es llanura/bosque normal. La nieve tiene color, icono y nombre en el idioma.
- **Sistema de iconos de items** (`iconositems.cs`): cada item/bloque tiene un **diseno dibujado con primitivas** sobre un lienzo de 32x32, con dos salidas del mismo diseño: `LienzoCanvas` (ICanvas, para la hotbar del HUD y los drops) y `LienzoRaster` (buffer RGBA -> PNG con transparencia via encoder PNG mínimo, para los slots del inventario y el cofre como `ImageSource` del botón).
- **Diseños**: herramientas (pico/espada/hacha/pala/azada con cabeza por material y mango), palo, semillas, mechero con llama, lingotes con brillo, menas en bruto, carbón, diamante, manzana, zanahoria, trigo, lana, cuero, hueso, polvora, carnes, antorcha, TNT, cofre, mesa, tronco con anillos, tablones con vetas, cesped con capa de tierra, horno con boca, bloques con textura y fallback de bloque de color con borde.
- **Hambre y comida**: nueva barra de comida en el HUD (10 marcas naranjas sobre los corazones) que baja despacio (~17 min). Al llegar a 0 la salud baja poco a poco hasta quedarse en 1 corazón (no mata). Para comer: selecciona comida en la hotbar (carne cocinada +8, cruda/manzana +4, zanahoria/podrida +3, trigo +2) y pulsa **U** o **clic derecho** — se consume el ítem y la barra sube.
- **Borrado persistente de mundos**: el botón de borrar de la lista de mundos ahora también elimina el archivo `.mundo` del disco — antes el mundo borrado reaparecía como fantasma al reabrir el juego.
- **Textura de hojas**: el follaje ya no es un cubo verde plano — tiene manchas de matas verdes oscuras y claras.
- **Drops con icono en el mundo**: los items caidos ya no son cajas de color — se ven como **iconos flotantes** (cuadro oscuro + diseño del item) con balanceo vertical.
- **Trigo y plantón con forma de planta** (dos planos cruzados verdes/amarillos) en la malla.
- **Guía de texturas**: `docs/guia-de-texturas.md` — explica las 3 capas (colores del mundo, detalles procedurales, iconos), cómo editar un icono, cómo añadir un item nuevo (checklist) y las limitaciones del renderizador por software.

### Probado
- Suite automática: **PRUEBAS SUPERADAS**; build cliente: **0 errores**.
- Verificación en el cliente real: la hotbar pasa de bloques de color plano (1 tono por slot) a iconos con diseño (6-11 tonos por slot); los slots del inventario muestran el icono PNG con transparencia sobre el fondo.
- **Nota de entorno**: el `dotnet` del PATH de AutoClaw (solo runtime 8.0) pisa al SDK del sistema y el exe ya no arranca sin `DOTNET_ROOT`. Los scripts de prueba fijan `DOTNET_ROOT=C:\Program Files\dotnet` y los builds usan la ruta completa del SDK.

## [0.10.3] - 2026-08-16

### Añadido (apariencia y control, pedidos por el usuario)
- **Sincronización hotbar ↔ inventario**: al mover ítems dentro del inventario (tecla E, clic izquierdo/derecho en los slots) la barra inferior (hotbar) refleja al instante los primeros 9 slots. Antes la hotbar solo se actualizaba al recibir el inventario del servidor.
- **Rueda del ratón para cambiar de ítem**: la rueda cambia el slot seleccionado de la hotbar (arriba = hacia la izquierda, estilo Minecraft) y avisa al servidor con `SeleccionarSlot`. Funciona con el ratón capturado (modo FPS).
- **Textura de minerales**: carbón, hierro, cobre, oro y diamante ya no son cubos de un solo color: se ven como **piedra con manchas del metal** (patrón procedural por cara con hash determinista por bloque, con sombreado y niebla).
- **Antorcha con forma, llama y luz**: la antorcha ya no es un cubo naranja — es un **poste delgado de madera con una llama** de dos planos cruzados (naranja/amarillo, siempre brillante aunque sea de noche) más **partículas de fuego animadas** que suben y parpadean (solo visuales, no queman). Además emite **luz real**: un mapa de luz 3D (BFS desde las antorchas, la luz decae 1 por bloque y no atraviesa bloques opacos) se suma al brillo global — de noche ilumina la zona alrededor de la llama. El mapa se recalcula al colocar o romper una antorcha.
- **Cofre y mesa de crafteo con forma**: el cofre es una caja con tapa sobresaliente y cerradura metálica; la mesa de trabajo tiene tablero y 4 patas. El césped ahora tiene los **lados de tierra** (antes todo verde) y los troncos, tablones, TNT y el horno ganaron detalles (vetas, franja blanca de dinamita y boca de horno).

### Probado
- **Suite automática: PRUEBAS SUPERADAS**; build cliente Windows `net10.0-windows10.0.19041.0`: **0 errores**.
- **Verificación en el cliente real (capturas + análisis de píxeles)**: la antorcha colocada muestra su llama (1285 píxeles naranjas/amarillos con R=255); el crafteo con clics derechos (1 madera → tablones) funciona en el cliente. El crafteo de mesa/cofre con el grid 3x3 quedó para prueba manual (el flujo de UIA es frágil, el mecanismo está cubierto por la suite).

## [0.10.2] - 2026-08-16

### Corregido (inventario, crafteo y menas, reportados por el usuario)
- **El inventario del cliente aparecía vacío (bug raíz)**: la pantalla de mundos consumía TODOS los mensajes de red del tick en que llegaba `Unido`, incluido el `Inventario` del kit de inicio (y los Mobs/Drops iniciales), porque el bucle `while (_red.Obtener() is Mensaje m)` seguía drenando la cola después de navegar a la página de juego. Ahora `Procesar` devuelve `true` al navegar y el bucle se detiene: el resto de mensajes quedan para la página de juego. Resultado: el kit (Madera 10, Tierra 10, Piedra 5, Arena 5, Palo 8, Antorcha 2, Semillas 4, Mechero 1 y **Pico de madera**) se muestra en el inventario y la hotbar desde el primer momento.
- **Romper bloques "no agregaba nada al inventario"**: era la combinación del bug raíz anterior + romper piedra/menas **sin pico** (no caía nada). El kit inicial ahora incluye un **pico de madera**, así que picar piedra, carbón, hierro, cobre, oro o diamante suelta el bloque correspondiente y se ve sumarse en el inventario.
- **No se podían poner cantidades parciales en el crafteo**: el clic izquierdo movía el stack completo y las recetas exigen cantidades exactas (p. ej. 2 tablones para un palo). Ahora el **clic derecho en un slot mueve UN solo ítem** (cursor ↔ slot, en las dos direcciones), tanto en los 27 slots del inventario como en los 4 del grid de crafteo (se enlaza el `RightTapped` del botón nativo WinUI).
- **Foco al entrar al mundo**: al crear/entrar a un mundo, el foco quedaba en null (el botón "Crear mundo" desaparece) y las teclas no llegaban hasta hacer clic; ahora `OnAppearing` enfoca el botón del menú vía `Dispatcher`.

### Añadido
- **Vetas de cobre** en la generación del mundo (`Vetas(80, Bloques.Cobre, nivelMar - 2, 2, 2, 5)`): el mineral ya existía pero nunca se generaba. Junto con el carbón (420), hierro (220), oro (110) y diamante (55), todas las menas están bajo tierra y se obtienen con pico.
- **Natación en lava** (`controladorjugador.cs`): nuevo `EnLavaCuerpo` (pies/pecho) con empuje propio (salto 3.2, hundimiento lento, avance 0.82) — antes la lava usaba la gravedad normal y el jugador se hundía. No toca `EnLava` (el indicador del HUD).

### Probado
- **Suite automática: PRUEBAS SUPERADAS** (incluye el ajuste del test "sin pico, la piedra no suelta bloque" que ahora selecciona el slot Madera por el pico del kit).
- **Verificación en el cliente Windows real (UIA + diags + píxeles)**: el kit llega y se muestra (`inventario recibido: 9 slots`; slots con 10/10/5/5/8/2/4...; panel visible confirmado por píxeles); el clic derecho mueve 1 ítem (`OnSlotInv(0,0,uno=True)`: Madera 10 → 9 y cursor "Madera x 1"); la tecla E abre el panel y el foco queda en el juego. El usuario confirmó manualmente que el crafteo con cantidades parciales funciona.
- Build cliente Windows `net10.0-windows10.0.19041.0`: **0 errores**.

## [0.10.1] - 2026-08-16

### Corregido (entrada y render, reportados por el usuario)
- **Captura del ratón en Windows**: al hacer clic en el juego el cursor desaparece y queda clavado en el centro de la vista, y la vista gira con el ratón (estilo FPS). Se unificaron los sistemas de coordenadas (antes se mezclaban coordenadas relativas al elemento, al contenido y de pantalla, por lo que la mira apuntaba a un lado y el cursor quedaba en otro): ahora el centro se calcula con `GetClientRect` + `ClientToScreen` (área cliente real, sin desviarse por la barra de título), el giro se lee con `GetCursorPos` contra el centro guardado en pantalla, y `VincularRaton` se reintenta en el tick si el handler no estaba listo al entrar. `ShowCursor` se oculta/muestra en bucle para no desbalancear el contador global.
- **Barra espaciadora ya no activa el menú (☰)**: el espacio se intercepta en la **fase de túnel** (`PreviewKeyDown`/`PreviewKeyUp`, se ejecuta antes de que el botón con foco reciba la tecla), así el botón nunca se "arma" ni dispara su Click al soltar la tecla. Además ya no se desenfoca el control (dejar el foco en null hacía que WinUI dejara de enrutar TODAS las teclas siguientes: Escape, T, WASD...).
- **Agua/lava ya no se ve "todo transparente"**: la interfaz piedra-agua no generaba ninguna cara (`EsVisible` hacía `if (!EsTransparente(bloque)) return false;` sin generar la cara del opaco contra el líquido) y se veía a través de todo el terreno. Ahora un bloque opaco genera cara si el vecino es transparente, y al estar sumergido se aplica un **velo translúcido** al frame completo (azul en agua 0.40, naranja en lava 0.45) con `Rasterizador.Tinte`: se nota que estás dentro del líquido pero los bloques sólidos se ven sólidos.

### Probado
- **Automatizado con UIA + análisis de píxeles (cliente Windows real)**: clic en el centro → cursor clavado en el centro exacto del área cliente; mover el ratón → la región central de la imagen cambia (la vista gira); espacio con foco en el botón ☰ → el menú NO se abre y las teclas siguientes (Escape abre la pausa, T abre el chat) siguen funcionando; modo espectador sumergido en un lago → tinte azul activo (B domina sobre R/G) y bloques con contraste visible (desviación de luminosidad alta), sin transparencias. La lava usa el mismo mecanismo (`Tinte` naranja 0.45) y su generación está cubierta por la suite.
- Build cliente Windows `net10.0-windows10.0.19041.0`: **0 errores**.

## [0.10.0] - 2026-08-16

### Añadido
- **Configuración del mundo al crearlo**: el diálogo de nuevo mundo permite ajustar tamaño (ancho/alto/profundidad), porcentajes de agua y lava, cantidad de mobs, duración del día y radio de aparición de mobs (mín/máx, estilo Minecraft). El protocolo `CrearMundo` se amplió con todos los ajustes y se guardan por mundo.
- **Mundo más grande por defecto**: 192×64×192 (antes 96×64×96), con generación más rápida y lagos de agua/lava **profundos** (4–20 bloques).
- **Ciclo día/noche más largo por defecto**: 1200 s por día completo (configurable por mundo).
- **Mobs por franja horaria**: de día aparecen animales (vaca, cerdo, oveja); de noche zombis, esqueletos y creepers, en un anillo de aparición `[RadioSpawnMin, RadioSpawnMax]` alrededor del jugador.
- **Ciclo de mobs dinámico (día/noche continuo)**: al anochecer los animales que sobreviven se quedan pero algunos desaparecen progresivamente para dejar sitio a los hostiles; al amanecer los zombis y esqueletos expuestos al sol **se queman con partículas de fuego visibles** (campo `Quemando` en el protocolo `Mobs`) y mueren, liberando sitio para los animales. Los hostiles que sobreviven en sombra también se van disipando de día (cupo ~20 %).
- **Bloques nuevos**: `PiedraMadre` (id 31, irrompible, capa y=0) y `Vacio` (id 32, fuera del mundo y pared del borde); caer al vacío mata.
- **Estabilidad del servidor**: cola de salida por conexión (`Channel` + writer asíncrono) para que un cliente que no lee no llene el buffer TCP y congele el servidor entero bajo el lock; `CrecerPlantas` por franjas por mundo, solo procesa franjas cerca de jugadores y solo difunde cambios visibles; búsqueda de agua más barata; mensaje `FijarHora` (depuración/pruebas).
- **Espectadores intocables**: los mobs no golpean ni el ambiente daña a los jugadores en modo espectador (necesario para las esperas largas de la suite).

### Probado
- **Suite automática: PRUEBAS SUPERADAS de forma repetible (4 corridas verdes consecutivas)** tras estabilizar el servidor: mundo grande, lagos profundos, PiedraMadre/Vacio, muerte al vacío, día/noche largo, mobs por hora con ciclo dinámico (quema solar con `Quemando`), radio de spawn, trigo, TNT, crafteo, cofres, hostiles, minerales, multijugador, muerte con causa y respawn.
- **Builds**: Core Release **0 errores**; cliente Windows `net10.0-windows10.0.19041.0` Debug **0 errores**; Android `net10.0-android` Release publish **exit 0** (APK `com.mundovoxel.app-Signed.apk`, 27.7 MB).

### Corregido
- **Congelación del servidor con clientes que no leen**: `Enviar` hacía `Flujo.Write` síncrono bajo `lock(_cerrojo)`; si un cliente acumulaba mensajes sin leerlos (p. ej. durante esperas largas), el buffer TCP se llenaba y el `Write` bloqueaba todos los ciclos y mensajes (los crafteos respondían en >8 s o el trigo no maduraba en 210 s). Ahora cada conexión tiene una cola `Channel` y un writer asíncrono dedicado.
- **Crecimiento de cultivos con mundos grandes**: la franja de cultivos era un campo global compartido (el mundo privado nunca procesaba la suya) → ahora por mundo; además se saltan franjas sin jugadores cerca y se reduce el coste de `HayAguaCerca`.
- **Fallos en cascada de la suite**: el mundo privado empezaba de noche y los hostiles repuestos constantemente mataban a Ana durante las esperas largas (trigo ~210 s) → la suite fija el mundo de día tras el test de ataque hostil (`FijarHora`) y los espectadores son intocables.

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
