# Manual de uso de MundoVoxel

Guía paso a paso para jugadores y para quien administre un servidor.

---

## 1. Qué necesitas

- Un dispositivo con **Windows** o **Android** para jugar (cliente MAUI).
- Un ordenador con **Windows** o **Linux** para hospedar el servidor (puede ser el mismo desde el que juegas).
- Los jugadores deben estar en la misma red (o el servidor debe tener el puerto 25575 abierto).

---

## 2. Poner en marcha el servidor

### Opción A: jugar solo (sin instalar nada)
Abre el cliente y pulsa **«Jugar solo»**: el juego levanta un servidor local automáticamente y te conecta. No necesitas el servidor aparte.

### Opción B: servidor dedicado (multijugador)
1. Compila o descarga `MundoVoxel.Server`.
2. Ejecútalo:
   - **Windows**: `MundoVoxel.Server.exe`
   - **Linux**: `./MundoVoxel.Server`
3. Verás el mensaje de confirmación con el puerto (25575 por defecto).
4. Para que otros se conecten desde la misma red: dales tu **IP local** (en Windows: `ipconfig`; en Linux: `ip a`). Desde internet: abre el puerto en el router y da tu **IP pública**.

> Para dejarlo corriendo siempre (como servicio), ver el apartado 6.

---

## 3. Conectarse y crear tu nombre

1. En el menú principal escribe tu **nombre** (se mostrará a los demás).
2. Escribe la **IP** del servidor (`127.0.0.1` si es tu propio equipo) y el **puerto** (25575).
3. Pulsa **«Conectar a un servidor»**.
4. Verás la lista de mundos disponibles.

---

## 4. Crear un mundo

1. Pulsa **«Crear mundo»**.
2. Ponle un **nombre**.
3. Elige:
   - **Público**: cualquiera puede entrar sin clave.
   - **Privado**: pide una **clave de 4 dígitos** (tú la pones; los demás la necesitarán).
4. Pulsa crear: entrarás directamente al mundo.

**Borrar un mundo**: solo el creador puede borrarlo. Desde la lista de mundos pulsa **«Borrar»** (o desde el menú de pausa dentro del juego). Se te pedirá confirmación; no se puede deshacer.

> Los mundos **no se guardan en disco**: viven en la memoria del servidor. Si el servidor se reinicia, se pierden. Mientras el servidor siga encendido, tu mundo queda ahí aunque salgas: puedes volver a entrar cuando quieras.

---

## 5. Unirse a un mundo

1. En la lista, pulsa **«Unirse»** en el mundo que quieras.
2. Si es **privado**, escribe la **clave de 4 dígitos** que te dio el creador.
3. Entrarás en el punto de aparición del mundo.

**Errores posibles**:
- «El mundo está lleno» → hay 12 jugadores (máximo por defecto).
- «Clave incorrecta» → revisa los 4 dígitos.
- «El mundo ya no existe» → el creador lo borró.

---

## 6. Administrar el servidor (servicio)

### Linux con systemd
```bash
sudo cp deploy/mundovoxel-server.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now mundovoxel-server
sudo ufw allow 25575/tcp
```
Logs: `journalctl -u mundovoxel-server -f`

### Windows con servicios
```bat
sc.exe create MundoVoxelServer binPath= "C:\ruta\MundoVoxel.Server.exe" start= auto
sc.exe start MundoVoxelServer
sc.exe stop MundoVoxelServer
sc.exe delete MundoVoxelServer
```
Logs: consola del servicio / visor de eventos.

### Configuración
Edita `appsettings.json` (junto al ejecutable del servidor):

| Clave | Valor por defecto | Qué hace |
|---|---|---|
| `Servidor:Nombre` | `MundoVoxel` | Nombre que ve el jugador al conectar. |
| `Servidor:Puerto` | `25575` | Puerto TCP. |
| `Servidor:MaxMundos` | `40` | Máximo de mundos simultáneos en memoria. |
| `Servidor:MaxJugadoresPorMundo` | `12` | Máximo de jugadores por mundo. |

Reinicia el servicio tras cambiarlo.

---

## 7. Controles

### Escritorio (Windows)
| Acción | Tecla / ratón |
|---|---|
| Moverse | `W A S D` |
| Saltar | `Espacio` |
| Volar (alternar) | `F`; subir `Espacio`, bajar `Shift` |
| Mirar | Arrastrar con el ratón |
| Colocar bloque | Clic |
| Romper bloque | Doble clic o `R` |
| Seleccionar bloque de la barra | `1`–`9` |
| Chat | `T` (escribe y pulsa Enter) |
| Pausa / menú | `Esc` |

### Android
| Acción | Control |
|---|---|
| Moverse | Joystick virtual (arrastra en la mitad izquierda) |
| Mirar | Arrastra en la mitad derecha |
| Saltar | Botón ⤒ |
| Volar | Botón Volar (alterna) |
| Colocar / Romper | Botones Colocar / Romper (también toque y doble toque) |
| Chat | Botón Chat |
| Menú / pausa | Botón ☰ |

---

## 8. Chat

- Pulsa `T` (o el botón Chat) para escribir; `Enter` envía; `Esc` cierra.
- Verás los mensajes de los jugadores y los avisos del sistema (entradas/salidas, mundos borrados…).

---

## 9. Cambiar los textos (archivo `.lang`)

Todos los textos están en `lang/es.lang`:

- **Windows**: copia la carpeta `lang` junto al ejecutable del cliente y edita `es.lang` con cualquier editor (codificación UTF-8). Los cambios se aplican al abrir el juego.
- **Android**: los textos van incrustados; hay que recompilar para cambiarlos.
- Formato: `clave=texto`; `#` para comentarios; `{0}`, `{1}`… para valores dinámicos.
- Si una clave falta o está mal escrita, se muestra la propia clave, así nunca se rompe la interfaz.

---

## 10. Solución de problemas

| Problema | Solución |
|---|---|
| «No se pudo conectar» | ¿Está el servidor corriendo? ¿La IP/puerto son correctos? Prueba `127.0.0.1`. |
| Otros jugadores no entran | Abre el puerto 25575/tcp en el cortafuegos y el router; usa la IP correcta (local o pública). |
| El juego va lento | Reduce la distancia de renderizado en el menú de pausa. |
| Se ve «clave» en la interfaz | Falta esa clave en `es.lang` (o está mal escrita). |
| Al reiniciar el servidor desaparecen los mundos | Es el comportamiento previsto: los mundos viven en memoria. |
