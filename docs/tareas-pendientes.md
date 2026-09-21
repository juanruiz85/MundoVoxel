# Tareas pendientes — MundoVoxel

Lista viva de lo que falta por hacer. Se actualiza con cada release.
Ultima actualizacion: 2026-09-20 (streaming por proximidad y cuentas por jugador, en curso).

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

## Pendientes (orden sugerido)

### Gameplay / red
- [ ] **Streaming por proximidad** *(en curso)*: Hecho: regiones de 64x64 columnas (`Mundo.LadoRegion`), ensamblador del cliente (`MundoRemoto`), protocolo con cabecera en `Unido` + una `MundoRegion` por mensaje y `MundoOlvida` para soltar, radio de carga (`RadioRegiones` en `ajustes.config.json`, 3x3 por defecto) con reenvio al cambiar de region, entrada con la region de los pies, colision que trata como solido lo pendiente, descarga en el cliente (bloques en aire) y redibujado repartido entre frames (`MallasSucias`). Falta: probarlo en partida de verdad (caminar y ver como se carga y se suelta el terreno) y mirar el coste en Android con un radio grande.
- [ ] **Delta de bloques para reconexiones rapidas** (EN PROGRESO, rama `feature/delta-reconexion`): implementado (TengoMundo + MundoDelta + aplicación en cliente) pero la suite mostro inestabilidad en cadenas de crafteo sin causa raiz identificada; main se mantiene estable. Retomar con depuración paso a paso.
- [x] v0.11.5: mundos mas grandes por defecto (256x64x256) - viable con la transmision troceada; memoria por mundo ~8.4 MB.
- [x] **Estado en vivo de servidores favoritos** (v0.11.14): hecho (latencia + jugadores en linea con `Ping`/`Pong` ligero; sin identificarse).
- [x] v0.11.8: chat privado entre jugadores (comando `/msg`, solo el destinatario y eco al emisor).

### Seguridad (ver docs/auditoria-seguridad.md)
- [x] **Tokens de invitacion para mundos privados** (v0.11.15): hecho (token de 10 caracteres por mundo, visible solo para el dueno, validos en `Unirse` junto al PIN; la reconexión los reusa).
- [x] **TLS opcional del servidor** (v0.11.16, cerrado en v0.11.17): hecho (certificado autofirmado autogenerado + casilla en el cliente con huella recordada + el favorito recuerda el modo y lo marca en la lista). La huella se comprueba en el handshake: si el certificado cambia, la conexión se rechaza antes de entrar, así que no hay ventana a mitad de partida.
- [x] **Clave de acceso del servidor** (v0.11.18): hecho (clave única del servidor en `Ajustes.ClaveServidor`; el cliente la escribe en el menú, la recuerda y la reconexión la reusa; comparación en tiempo constante y el mismo tope de intentos que las claves de mundo). Si algún día se abre a internet, el siguiente paso sería contraseña **por jugador** (cuentas).
- [ ] **Cuentas por jugador** (EN PROGRESO): hechos los pasos 1 y 2. Paso 1, el almacén en Core (`Cuentas`: hash PBKDF2-SHA256 con sal, comparación en tiempo constante, validaciones, cambio de clave y persistencia en `cuentas.json`). Paso 2, el protocolo y el servidor (`Hola` con `Usuario` y `ClaveCuenta`, `CuentasObligatorias` y `RegistroAbierto` en los ajustes, el nombre autenticado con la grafía guardada y el tope de 5 intentos por minuto, con las pruebas de integración por socket). Falta el paso 3, el cliente: campos de usuario y clave en el menú, recordarlos y mandarlos también al reconectar.
- [x] v0.11.6: tope de memoria al deserializar mundos cargados (dimensiones validadas; completa el tope de descompresion).

### Infraestructura
- [ ] MSIX/instalador de Windows en el workflow de release (hoy solo zip).
- [ ] Firma del APK con keystore propio de release (hoy usa la clave de debug).
- [x] v0.11.9: unidad systemd + INSTALL.txt dentro del tar.gz del servidor Linux . (pendiente menor: firma GPG del repositorio apt, no aplica sin repo propio).
- [ ] CI: acelerar el workload MAUI (el cache completo de packs se cuelga; probar cachear solo sdk-manifests/metadata o una imagen con el workload).

### Cliente
- [ ] Probar manualmente la reconexión automática en Android (el test de protocolo cubre el flujo; falta el tactil real).
- [x] **Barra de progreso al minar** (v0.11.13): hecho (barra bajo la mira con golpes/necesarios y mejor herramienta; solo cliente, sin cambios de protocolo).

### Documentacion
- [ ] Manual de uso: capturas actualizadas con las funciones nuevas (favoritos, panel tactil).
- [ ] Mantener la seccion "Uso de IA en el desarrollo" del readme (script con BOM corregido: ya no corrompe acentos ni emoji).
