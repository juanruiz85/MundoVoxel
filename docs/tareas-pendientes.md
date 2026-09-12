# Tareas pendientes — MundoVoxel

Lista viva de lo que falta por hacer. Se actualiza con cada release.
Ultima actualizacion: 2026-09-09 (tras v0.11.4).

## Hecho recientemente (para contexto)

- [x] v0.10.8: lista de servidores favoritos + reconexion automatica del cliente (retroceso exponencial, re-entrada al mundo)
- [x] v0.10.9: paridad Android/Windows (hotbar tactil, botones Usar/Soltar/Espectador)
- [x] v0.11.0: transmision del mundo troceada (`Unido` + `MundoChunk` de 128 KB)
- [x] v0.11.1: claves de mundos privados de 6 digitos
- [x] v0.11.2: propiedad de mundos por nombre de jugador (bug: el dueno que reconecta la perdía) + test de reconexion a nivel de protocolo
- [x] v0.11.3: autoguardado periodico de mundos en disco (`AutoguardadoSegundos`)
- [x] v0.11.4: posicion del jugador persistida (al reconectar vuelves donde estabas; sin cambios de protocolo)
- [x] CI (GitHub Actions: suite en Linux + builds MAUI) y releases automaticos por tags (`release.yml`)
- [x] Seguridad: anti-autoclick, tope de intentos de clave, mineria por golpes, tope de descompresion (ver docs/auditoria-seguridad.md)

## Pendientes (orden sugerido)

### Gameplay / red
- [ ] **Streaming por proximidad**: enviar al entrar solo los trozos cercanos al jugador y el resto conforme se mueve (la transmision troceada ya esta hecha; falta que el cliente renderice mundos parciales y que el servidor decida que trozos mandar).
- [ ] **Delta de bloques para reconexiones rapidas**: si el jugador reconoce en pocos segundos, reenviar solo los `BloqueCambio` del intervalo en vez del mundo completo.
- [ ] **Mundos mas grandes por defecto** (256x64x256): ya viable con transmision troceada; revisar tiempo de generacion y memoria por mundo (40 mundos x ~8 MB).
- [ ] Lista de servidores favoritos con ping/estado en vivo (jugadores conectados por servidor).
- [ ] Chat privado entre jugadores (comandos `/msg`).

### Seguridad (ver docs/auditoria-seguridad.md)
- [ ] Tokens de invitacion para mundos privados (el PIN de 6 digitos ya frena el fuerza bruta; el token evita compartir claves).
- [ ] TLS opcional del servidor (o documentar tunel SSH/VPN) si se abre a internet.
- [ ] Autenticacion basica de identidad (contrasena por jugador) si se abre a internet.
- [ ] Tope de memoria al deserializar mundos cargados (hoy solo acota la descompresion).

### Infraestructura
- [ ] MSIX/instalador de Windows en el workflow de release (hoy solo zip).
- [ ] Firma del APK con keystore propio de release (hoy usa la clave de debug).
- [ ] Publicar el paquete Linux como .deb/systemd unit ademas del tar.gz.
- [ ] CI: cache del workload MAUI para acelerar los builds.

### Cliente
- [ ] Probar manualmente la reconexion automatica en Android (el test de protocolo cubre el flujo; falta el tactil real).
- [ ] Barra de progreso visual al minar bloques duros (hoy solo hay feedback por golpes).

### Documentacion
- [ ] Manual de uso: capturas actualizadas con las funciones nuevas (favoritos, panel tactil).
- [ ] Mantener la seccion "Uso de IA en el desarrollo" del readme (script con BOM corregido: ya no corrompe acentos ni emoji).