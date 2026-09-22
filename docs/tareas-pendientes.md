# Tareas pendientes — MundoVoxel

Lista viva de lo que falta por hacer. Se actualiza con cada release.
Ultima actualizacion: 2026-09-21 (reconexión rápida, MSIX de Windows, mejoras de CI y pruebas de mobs deterministas).

## Hecho recientemente (para contexto)

- [x] v0.10.8: lista de servidores favoritos + reconexión automática del cliente (retroceso exponencial, re-entrada al mundo)
- [x] v0.10.9: paridad Android/Windows (hotbar tactil, botones Usar/Soltar/Espectador)
- [x] v0.11.0: transmision del mundo troceada (`Unido` + `MundoChunk` de 128 KB)
- [x] v0.11.1: claves de mundos privados de 6 dígitos
- [x] v0.11.2: propiedad de mundos por nombre de jugador (bug: el dueno que reconecta la perdía) + test de reconexión a nivel de protocolo
- [x] v0.11.3: autoguardado periodico de mundos en disco (`AutoguardadoSegundos`)
- [x] v0.11.4: posición del jugador persistida (al reconectar vuelves donde estabas; sin cambios de protocolo)
- [x] CI (GitHub Actions: suite en Linux + builds MAUI) y releases automaticos por tags (`release.yml`)
- [x] Seguridad: anti-autoclick, tope de intentos de clave, mineria por golpes, tope de descompresion (ver docs/auditoria-seguridad.md)
- [x] Pruebas de mobs deterministas (v0.11.23): el ataque de un mob hostil y los drops ya no dependen del azar ni se omiten en CI (mundo nocturno con semilla fija para el ataque; la posición del mob se refresca antes de cada golpe para el drop). Ya no queda ninguna comprobación omitida: la variedad de tipos se mide sobre mundos con semilla fija (uno nocturno y otro diurno) y la suite se comporta igual en local y en CI (237 correctas, 0 fallos).
- [x] Versión de la release derivada del tag: el MSIX toma `Version` del manifiesto, ajustado con el tag (`0.11.23.0`), y el APK el `versionCode` (`1123`), para que las releases se puedan actualizar en sitio.

## Pendientes (orden sugerido)

### Distribución
- [ ] **Firmar las releases con una identidad propia y estable (APK y MSIX)**: falta solo lo que no puedo hacer yo: generar el keystore de Android y el PFX de firma de código, y pegarlos en los secretos del repositorio (`ANDROID_KEYSTORE_B64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASS`, `MSIX_CERT_B64`, `MSIX_CERT_PASS`). Con eso se arreglan dos cosas: el APK deja de firmarse con una clave de depuración nueva en cada ejecución, que es lo que hoy hace que Android rechace la actualización en sitio (comprobado: las firmas de `v0.11.19` y `v0.11.23` no coinciden), y el MSIX se instala sin tener que confiar el `.cer` a mano en cada máquina. La ruta de firma ya está probada en local: `apksigner verify --print-certs` confirma el firmante y los esquemas v1+v2+v3. Ver `docs/releases.md`.
### Gameplay / red
- [x] **Streaming por proximidad** (v0.11.23): Hecho: regiones de 64x64 columnas (`Mundo.LadoRegion`), ensamblador del cliente (`MundoRemoto`), protocolo con cabecera en `Unido` + una `MundoRegion` por mensaje y `MundoOlvida` para soltar, radio de carga (`RadioRegiones` en `ajustes.config.json`, 3x3 por defecto) con reenvio al cambiar de region, entrada con la region de los pies, colision que trata como solido lo pendiente, descarga en el cliente (bloques en aire) y redibujado repartido entre frames (`MallasSucias`). Falta: probarlo en partida de verdad (caminar y ver como se carga y se suelta el terreno) y mirar el coste en Android con un radio grande; las dos son pruebas manuales (sección 8 de la guía de pruebas).
- [x] **Reconexion rapida (delta por regiones)** (v0.11.22, publicada con la release v0.11.23): hecho. Al volver, el cliente dice que aun tiene el mundo (`Unirse.TengoMundo`) y el servidor responde `Unido` con `Delta` y `RegionesDelta` y solo reenvía las regiones que cambiaron mientras estaba fuera; con la ausencia pasada (`MinutosDeltaRapido`), con medio mundo cambiado o sin salida apuntada se manda el mundo entero. Probado por socket (8 comprobaciones). La rama antigua `feature/delta-reconexion` (delta por bloques con `MundoDelta`) queda descartada: su lectura ambigua es el origen del desajuste de mensajes que rompía la suite. Falta verlo en partida de verdad.
- [x] v0.11.5: mundos mas grandes por defecto (256x64x256) - viable con la transmision troceada; memoria por mundo ~8.4 MB.
- [x] **Estado en vivo de servidores favoritos** (v0.11.14): hecho (latencia + jugadores en linea con `Ping`/`Pong` ligero; sin identificarse).
- [x] v0.11.8: chat privado entre jugadores (comando `/msg`, solo el destinatario y eco al emisor).

### Seguridad (ver docs/auditoria-seguridad.md)
- [x] **Tokens de invitacion para mundos privados** (v0.11.15): hecho (token de 10 caracteres por mundo, visible solo para el dueno, validos en `Unirse` junto al PIN; la reconexión los reusa).
- [x] **TLS opcional del servidor** (v0.11.16, cerrado en v0.11.17): hecho (certificado autofirmado autogenerado + casilla en el cliente con huella recordada + el favorito recuerda el modo y lo marca en la lista). La huella se comprueba en el handshake: si el certificado cambia, la conexión se rechaza antes de entrar, así que no hay ventana a mitad de partida.
- [x] **Clave de acceso del servidor** (v0.11.18): hecho (clave única del servidor en `Ajustes.ClaveServidor`; el cliente la escribe en el menú, la recuerda y la reconexión la reusa; comparación en tiempo constante y el mismo tope de intentos que las claves de mundo). Si algún día se abre a internet, el siguiente paso sería contraseña **por jugador** (cuentas).
- [x] **Cuentas por jugador** (v0.11.21, publicada con la release v0.11.23): hechos los tres pasos. Paso 1, el almacén en Core (`Cuentas`: hash PBKDF2-SHA256 con sal, comparación en tiempo constante, validaciones, cambio de clave y persistencia en `cuentas.json`). Paso 2, el protocolo y el servidor (`Hola` con `Usuario` y `ClaveCuenta`, `CuentasObligatorias` y `RegistroAbierto`, el nombre autenticado con la grafía guardada y el tope de 5 intentos por minuto). Paso 3, el cliente (usuario y clave en el menú, recordados y reutilizados en la reconexión). Paso 4, el cambio de clave de la cuenta desde el menú de pausa (con la clave actual; el servidor renueva la sal y el cliente guarda la nueva). Todo apagado por defecto. Queda probarlo a mano con el servidor en cuentas obligatorias.
- [x] v0.11.6: tope de memoria al deserializar mundos cargados (dimensiones validadas; completa el tope de descompresion).

### Infraestructura
- [x] **MSIX de Windows** (v0.11.23): hecho. El release adjunta, además del zip, un paquete MSIX (manifiesto en `Platforms/Windows/Package.appxmanifest` + iconos), firmado con el certificado de los secretos o, si no los hay, con uno autofirmado de desarrollo y su `.cer` al lado. El publish normal sigue siendo la carpeta suelta. Falta probar la instalación a mano en otro Windows (ver `docs/releases.md`).
- [x] v0.11.9: unidad systemd + INSTALL.txt dentro del tar.gz del servidor Linux . (pendiente menor: firma GPG del repositorio apt, no aplica sin repo propio).
- [x] **CI: filtros de ruta** (v0.11.23): hecho. Los push que solo tocan documentación (`**.md`, `docs/**`, `.gitignore`) ya no disparan la suite ni los builds (~9 minutos ahorrados por commit de textos).
- [x] **CI: acelerar los builds** (v0.11.23): hecho lo razonable. Los paquetes NuGet se cachean (clave = hash de los .csproj), un push nuevo cancela el CI que estuviera corriendo del mismo ref y los push de solo documentación no disparan nada. Medido: job de MAUI 8,4 min con la caché en caliente frente a una mediana de 9,2 (rango 6,9-11,9, o sea que la mejora queda dentro del ruido) y la suite 5,3 min como siempre; la primera ejecución con la caché en frío costó 10,4 min (pagando la subida). Descartado cachear los packs del workload: la vez que se probó se colgó y el ahorro no compensa el riesgo.

### Cliente
- [x] Coherencia del idioma comprobada en la suite (v0.11.23): 5 comprobaciones nuevas (claves repetidas, textos vacíos, claves usadas que faltan y textos de bloque huérfanos). De paso se arregla el texto del cofre, que no estaba en el archivo de idioma.
- [x] Restos del control táctil muerto retirados (v0.11.23): el evento de soltar tecla, los dos métodos de simulación y siete constantes de tecla que no usaba nadie. Los comentarios de las dos clases ya no mienten. Android sigue con su joystick y su panel de botones.
- [x] Protocolo sin mensajes muertos (v0.11.23): se retira `MundoBorrado` (nadie lo enviaba ni lo atendía) y la suite comprueba que los 49 mensajes declarados se usan fuera de protocolo.cs.
- [x] Textos de idioma sin usar retirados (v0.11.23): cinco entradas de `es.lang` no las pedía nadie y se retiran; la suite (244) vigila ahora las dos direcciones (toda clave usada existe, y todo texto del archivo se usa).
- [x] Ajustes que no hacían nada (v0.11.23): `SensibilidadRaton` era un ajuste documentado que nadie leía (el cliente traía un `1` a mano); ahora es el valor por defecto y la suite (245) comprueba que todo campo de la configuración lo lee alguien.
- [x] Anti-cheat visible en el archivo de ajustes de ejemplo (v0.11.23): `AntiCheatSaltoMax` y `AntiCheatVelocidadMax` estaban documentados solo en el código. También se unificó el ayudante de búsqueda de palabra completa, que estaba copiado en dos bloques de la suite.
- [x] El ladrillo ya tiene fuente (v0.11.23): era el único bloque colocable imposible de conseguir (sin receta, horno, generación ni botín). Se cuece tierra en el horno (con carbón, como las fundiciones) y la suite (247) comprueba que ningún bloque colocable ni ningún objeto se queda sin fuente.
- [ ] El ladrillo usa el icono de reserva del inventario (bloque de color con borde): no tiene dibujo propio. Es lo único que le falta.
- [x] La grava ya se genera (v0.11.23): el bloque estaba terminado (nombre, color, icono, se rompe con pala) pero el generador nunca lo colocaba, así que era imposible de conseguir. Sale en bolsas bajo tierra reemplazando piedra (90 vetas de 5 a 12 bloques), como el cobre que también faltaba generar.
- [x] API del cliente sin restos (v0.11.23): diez miembros públicos que no llamaba nadie retirados (contador de mallas, dos conversiones de color, color de mobs, "cabeza en lava", "trigo maduro", "es bloque", "requiere pico", la propiedad del joystick y el toque de la hotbar) y comprobación nueva de la suite que exige que todo miembro declarado con visibilidad lo use alguien.
- [x] Release v0.11.23 publicada (v0.11.23): el tag existia pero no llegaba a release: el paso del MSIX fallaba con `MSB1001` porque el array de argumentos se colapsaba a cadena cuando traía un solo elemento y el splat la expandía carácter a carácter. Arreglado y documentado.
- [x] Tipos sin restos (v0.11.23): comprobación nueva de la suite que exige que todo tipo declarado lo use alguien. El barrido previo solo encontró los dos puntos de entrada del sistema, así que el código estaba limpio; queda vigilado. La suite pasa de 248 a 249 comprobaciones.
- [x] Eventos sin disparo (v0.11.23): comprobación nueva que exige que todo evento declarado tenga un sitio donde se dispare (no basta con suscribirse). Los doce del proyecto lo tienen, incluido `AlCancelar`, que fue el bug que dio origen a esta familia de comprobaciones. La suite pasa a 250.
- [x] Claves de Preferences vigiladas (v0.11.23): comprobación nueva que exige que cada clave guardada por el cliente se lea y se escriba, para que un renombrado en un solo lado no deje un ajuste que parece funcionar. Las cuatro claves actuales pasan. La suite pasa a 251.
- [x] Builds sin avisos (v0.11.23): la suite y el cliente compilan con 0 avisos (`DisplayAlertAsync`, nulabilidad y enlace compilado en la lista de mundos). De paso se arregla que cancelar la reconexión no hiciera nada: ahora cierra la conexión, avisa y vuelve al menú.
- [ ] Probar manualmente la reconexión automática en Android (el test de protocolo cubre el flujo; falta el tactil real).
- [x] **Barra de progreso al minar** (v0.11.13): hecho (barra bajo la mira con golpes/necesarios y mejor herramienta; solo cliente, sin cambios de protocolo).

### Documentacion
- [ ] Manual de uso: capturas actualizadas con las funciones nuevas (favoritos, panel tactil).
- [ ] Mantener la seccion "Uso de IA en el desarrollo" del readme (script con BOM corregido: ya no corrompe acentos ni emoji).
