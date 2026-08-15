# 🌍 MundoVoxel

Un juego de bloques estilo *MinecraftJS* hecho en **.NET MAUI (C# / .NET 10)**, completamente en **español**, con **multijugador** por TCP. Compatible con **Windows** y **Android** (cliente MAUI) y con un servidor que corre como **servicio en Windows y Linux**.

> ⚠️ Nota de plataforma: .NET MAUI no tiene objetivo oficial para escritorio Linux. El **cliente** oficial es Windows y Android; el **servidor multijugador** sí corre en Windows y Linux (como servicio o proceso normal). Toda la lógica del juego vive en la biblioteca compartida `MundoVoxel.Core`, por lo que el mismo código de juego podría portarse a Linux cuando MAUI tenga soporte oficial (o mediante backends comunitarios).

---

## ✨ Funciones

### Jugabilidad
- Mundo de voxels generado proceduralmente (terreno, montañas, playas, agua, árboles).
- Primera persona: moverse, saltar, volar (modo creativo), romper y colocar bloques.
- Barra de selección con 9 tipos de bloque.
- Chat en vivo entre jugadores.
- HUD: coordenadas, FPS, barra de bloques, mira y lista de jugadores.
- Controles de escritorio (teclado + ratón) y táctiles en Android (joystick virtual + botones).

### Multijugador
- Cada jugador puede **crear un mundo nuevo** o **unirse a otro** escribiendo la IP del servidor.
- Mundos **públicos** (cualquiera entra) o **privados** (requieren **clave de 4 dígitos**).
- Los **mundos viven en memoria** del servidor: si un mundo se queda vacío, no se borra; su creador puede **borrarlo** o **dejarlo para volver después**.
- El servidor es una sola aplicación que aloja hasta 40 mundos × 12 jugadores por defecto (configurable).

---

## 📁 Estructura del proyecto

| Proyecto | Descripción |
|---|---|
| `MundoVoxel.Core` | Biblioteca compartida: bloques, generación de terreno, mundo, raycaster, protocolo de red y `GameServer` (lógica multijugador). Sin dependencias de UI. |
| `MundoVoxel.Server` | Programa del servidor. Corre como proceso normal, servicio de Windows o servicio systemd en Linux. |
| `MundoVoxel.Client` | Cliente **.NET MAUI** (Windows + Android). Menús, lista de mundos, partida 3D y chat. |
| `MundoVoxel.Pruebas` | Prueba automática del protocolo multijugador (arranca el servidor, crea mundos, prueba claves, bloques, chat, persistencia y borrado). |

---

## 🚀 Inicio rápido

### Requisitos
- SDK de **.NET 10** (https://dotnet.microsoft.com/download/dotnet/10.0)
- Para el cliente MAUI, además: `dotnet workload install maui`
- Para Android: Android SDK (el workload de MAUI lo solicita)

### 1) Probar el servidor (núcleo multijugador)
```bash
cd MundoVoxel
dotnet run --project MundoVoxel.Server
```
Verás algo como: `Servidor «MundoVoxel» escuchando en el puerto 25575`.

### 2) Ejecutar las pruebas automáticas
```bash
dotnet run --project MundoVoxel.Pruebas
```
Debe terminar con `PRUEBAS SUPERADAS` (59 comprobaciones).

### 3) Compilar el servidor para producción
```bash
# Windows (autocontenido)
dotnet publish MundoVoxel.Server -c Release -r win-x64 --self-contained -o publicar/win

# Linux (autocontenido)
dotnet publish MundoVoxel.Server -c Release -r linux-x64 --self-contained -o publicar/linux
```

### 4) Cliente MAUI (Windows)
```bash
dotnet build MundoVoxel.Client -f net10.0-windows10.0.19041.0
dotnet run --project MundoVoxel.Client -f net10.0-windows10.0.19041.0
```

### 5) Cliente MAUI (Android)
```bash
dotnet build MundoVoxel.Client -f net10.0-android
# o instalar en un dispositivo/emulador:
dotnet build MundoVoxel.Client -f net10.0-android -t:Run
```

---

## 🖥️ El servidor como servicio

### Linux (systemd)
```bash
sudo cp deploy/mundovoxel-server.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now mundovoxel-server
sudo ufw allow 25575/tcp   # abrir el puerto en el cortafuegos
```
Los mundos viven **en memoria**: al reiniciar el servicio se pierden (diseño deliberado).

### Windows (servicio)
```bat
sc.exe create MundoVoxelServer binPath= "C:\ruta\publicar\win\MundoVoxel.Server.exe" start= auto
sc.exe start MundoVoxelServer
```
Los registros se ven en la consola / journal / visor de eventos del servicio.

### Configuración (`MundoVoxel.Server/appsettings.json`)
```json
{
  "Servidor": {
    "Nombre": "MundoVoxel",
    "Puerto": 25575,
    "MaxMundos": 40,
    "MaxJugadoresPorMundo": 12
  }
}
```

---

## 🌐 Cómo jugar en multijugador

1. Un jugador (o un servidor dedicado) levanta `MundoVoxel.Server`.
2. En el cliente: escribe tu **nombre**, la **IP** del servidor (o `127.0.0.1` para jugar solo) y pulsa **Conectar**.
3. En la lista de mundos: **Crear mundo** (público o privado con clave de 4 dígitos) o **Unirse** a uno existente.
   - Si el mundo es privado, te pedirá la clave.
4. Dentro del mundo: construye, rompe y chatea con los demás.
5. **Salir del mundo** te devuelve a la lista; tu mundo sigue en el servidor. Como **creador**, puedes **Borrar** el mundo desde la lista o desde el menú de pausa.

> Para que otros jugadores se conecten desde internet, abre el puerto `25575/tcp` en el router/cortafuegos y usa la IP pública. En una red local basta con la IP local (ej. `192.168.1.10`).

---

## 🎮 Controles

| Acción | Escritorio | Android |
|---|---|---|
| Moverse | `W A S D` | Joystick virtual (mitad izquierda) |
| Saltar | `Espacio` | Botón ⤒ |
| Volar (modo creativo) | `F` (subir `Espacio`, bajar `Shift`) | Botón Volar |
| Mirar | Arrastrar con el ratón | Arrastrar (mitad derecha) |
| Colocar bloque | Clic | Botón Colocar / toque |
| Romper bloque | Doble clic o `R` | Botón Romper / doble toque |
| Seleccionar bloque | `1`–`9` o rueda | — |
| Chat | `T` | Botón Chat |
| Pausa / menú | `Esc` | Botón Menú |

---

## 🈯 Idiomas (archivo `.lang`)

Todos los textos del juego están en un archivo de texto plano fácil de editar:

- Windows: coloca `lang/es.lang` junto al ejecutable del cliente (`MundoVoxel.Client.exe` → carpeta `lang/es.lang`).
- Android: el texto viene incrustado en la app (hay que recompilar para cambiarlo).
- Formato: `clave=texto`, líneas `#` para comentarios, y `{0}`/`{1}` para valores dinámicos.

Ejemplo:
```
menu.jugar_solo=Jugar solo
bloque.tierra=Tierra
chat.entro={0} entró al mundo
```

Si una clave no existe, se muestra la propia clave (fallback), así nunca se rompe la interfaz.

---

## 🗺️ Próximos pasos

Ver `CHANGELOG.md` y la sección *Próximos pasos* al final de `docs/ARQUITECTURA.md`.

Ideas a corto plazo:
- Persistencia opcional de mundos en disco (hoy son solo en memoria, por diseño).
- Sistema de inventario y supervivencia (salud, hambre, caídas).
- Más tipos de bloques, iluminación por día/noche y biomas.
- Lista de servidores favoritos y reconexión automática.
- Anti-cheat básico y moderación de chat.
- Portar el cliente a Linux cuando MAUI tenga soporte oficial.

---

<!-- IA-USO-INICIO -->
## 🤖 Uso de IA en el desarrollo

> Sección actualizada automáticamente en cada commit con docs/actualizar-stats-ia.ps1.
> Los datos salen de los archivos de sesión del gateway (AutoClaw/OpenClaw): tokens,
> modelos y costos reportados por el proveedor, más los prompts escritos por el
> desarrollador (marcados como solicitudes de usuario).

### Resumen

| Métrica | Valor |
|---|---|
| Período de desarrollo | 2026-07-22 → 2026-08-15 |
| Sesiones de IA | 13 |
| Prompts del desarrollador | 35 |
| Respuestas generadas por IA | 1,196 |
| Tokens de entrada (prompts + contexto) | 4,134,147 |
| Tokens de salida (generación) | 1,524,562 |
| **Tokens totales** | **5,658,709** |
| Tokens de caché leídos | 213,199,488 |
| Costo real registrado | $0.00 (modelo ZAI sin cargo reportado) |
| Costo estimado a tarifas de mercado | ~41.78 USD |
| Agentes de IA con uso | main |

### Promedios

- Tokens por prompt: ~118,118 de entrada / ~43,559 de salida.
- Costo estimado por prompt: ~1.19 USD (a tarifas de mercado).

### Modelos utilizados

| Modelo | Respuestas | % del total |
|---|---|---|
| zai_auto (ruteo automático) | 1054 | 88.1% |
| dpskpro_deepseek-v4-flash (DeepSeek V4 Flash) | 140 | 11.7% |
| gateway-injected (mensaje interno) | 1 | 0.1% |
| zai_glm-5-turbo (GLM-5 Turbo) | 1 | 0.1% |

### Plataforma

- **OpenClaw / AutoClaw** (gateway local), API compatible openai-completions.
- Los modelos se sirven vía **ZAI** (ruteador zai_auto elige el modelo según la tarea; también se usaron DeepSeek V4 Flash y GLM-5 Turbo).
- Herramientas auxiliares de IA: AutoGLM (reconocimiento visual de capturas) y scripts UIA locales.

### Nota metodológica

- "Tokens de entrada" incluye el contexto completo reenviado en cada turno (por eso es
  muy superior a los tokens de salida). "Caché leída" son tokens reutilizados del contexto
  previo (tarifa reducida en proveedores comerciales).
- El **costo real registrado es $0.00** porque el proveedor ZAI no reporta cargos para
  estos modelos; la columna "estimado a tarifas de mercado" usa $2/M entrada,
  $8/M salida y $0.10/M caché (referencia típica de modelos de razonamiento) solo como
  orientación.
<!-- IA-USO-FIN -->
