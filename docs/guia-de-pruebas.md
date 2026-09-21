# Guía de pruebas de MundoVoxel

Cómo compilar, ejecutar y probar el juego desde la terminal, tanto en **Windows**
como en **Android (emulador)**, y cómo jugar **multijugador** con ambos clientes
en el mismo mundo.

---

## 1. Requisitos

- .NET SDK 10 (Windows).
- Para Android: Android SDK + emulador (ver sección 4) o un teléfono con depuración USB.
- El repositorio clonado: `git clone https://github.com/juanruiz85/MundoVoxel.git`

Todo lo demás (paquetes NuGet, plataformas MAUI) se descarga automáticamente la primera vez.

---

## 2. Pruebas automáticas (suite de protocolo)

La suite `MundoVoxel.Pruebas` levanta un servidor real en memoria y valida el
protocolo de extremo a extremo: mundos, bloques, inventario, crafteo, cocina,
mobs, TNT, trigo, día/noche, soltar ítems, oxígeno, lava, muerte y respawn,
con los mundos grandes (256×64×256) y el ciclo dinámico de mobs (día/noche)
activos. Desde las ultimas versiones cubre también el mundo por regiones (streaming por proximidad), la reconexión rápida con delta (se conserva el terreno al volver) y las cuentas por jugador (registro, entrada y cambio de clave).

```powershell
cd MundoVoxel
dotnet run --project MundoVoxel.Pruebas\MundoVoxel.Pruebas.csproj -c Release
```

Resultado esperado (última línea):

```
PRUEBAS SUPERADAS
```

> Es una prueba con red local real (TCP en 127.0.0.1), así que tarda un poco
> (unos 5-6 minutos en Release; compilada en Debug cuesta bastante más, alrededor de 20 en un portátil). No cierres la ventana antes de
> ver el resultado. La suite es repetible: debe salir verde en corridas
> consecutivas. Si alguna vez falla el trigo o un crafteo, espera y vuelve a
> ejecutar (la carga del servidor con 2 mundos grandes puede atrasar respuestas;
> los espectadores son intocables para que Ana sobreviva a las esperas largas).

> **Entorno recortado**: si el shell no define `ProgramFiles(x86)` ni
> `ProgramData` (algunos entornos de automatización), el SDK de .NET falla al
> restaurar paquetes con `error : Value cannot be null. (Parameter 'path1')`
> en `NuGet.targets` (NuGet las necesita para la configuración global). Fija
> las variables antes de compilar o ejecutar:
>
> ```powershell
> $env:ProgramFiles      = 'C:\Program Files'
> $env:ProgramFiles(x86) = 'C:\Program Files (x86)'
> $env:ProgramData       = 'C:\ProgramData'
> $env:CommonProgramFiles = 'C:\Program Files\Common Files'
> ```

---

## 3. Probar el cliente en Windows

### 3.1 Compilar y ejecutar desde la terminal

```powershell
cd MundoVoxel
dotnet build MundoVoxel.Client\MundoVoxel.Client.csproj -f net10.0-windows10.0.19041.0 -c Debug
dotnet run --project MundoVoxel.Client\MundoVoxel.Client.csproj -f net10.0-windows10.0.19041.0 -c Debug
```

Se abre la ventana del juego (WinUI 3). Desde el menú:

1. Escribe tu nombre y pulsa **«Jugar solo»** (levanta un servidor local y entras
   a un mundo nuevo) **o** pulsa **«Conectar a un servidor»** para unirte a un
   servidor dedicado (ver sección 5).
2. Controles: **clic izquierdo** romper/atacar, **clic derecho** colocar,
   **WASD** mover, **espacio** saltar, **Q** soltar ítem, **E** inventario,
   **G** modo espectador, **ESC** pausa.

### 3.2 Verificación rápida en vivo

En la esquina superior izquierda se ve el HUD: corazones, barra de oxígeno,
coordenadas y hora. Si el mundo se renderiza (cielo, terreno, árboles) y el HUD
responde al mover el ratón, el cliente funciona.

### 3.3 Verificar captura del ratón, espacio y agua/lava

1. **Captura del ratón (FPS)**: dentro de un mundo, haz **clic izquierdo** en la
   vista. El cursor debe **desaparecer y quedar clavado en el centro** de la
   pantalla, y al mover el ratón la **vista gira** con él (el cursor no debe
   salirse del centro). ESC libera el ratón.
2. **Barra espaciadora**: pulsa **☰** (menú, arriba a la derecha) para darle
   foco y ciérralo con ESC o «Reanudar». Pulsa **espacio**: el jugador salta y
   el menú **no** debe abrirse. Después pulsa **T** (chat) y **ESC** (pausa):
   deben seguir funcionando (la tecla no debe "morirse" tras saltar).
3. **Agua/lava**: búscate un lago de agua (o crea un mundo con el nivel de agua
   alto) y sumérgete. Debe verse un **velo azul translúcido** sobre el terreno,
   con los **bloques sólidos visibles a través** (nada de "ver a través de
   todo"). En un lago de lava el velo es **naranja**.

### 3.4 Verificar inventario, crafteo y minerales

1. **Kit inicial visible**: al entrar a un mundo nuevo, pulsa **E** (inventario).
   La cuadrícula debe mostrar el kit: Madera 10, Tierra 10, Piedra 5, Arena 5,
   Palo 8, Antorcha 2, Semillas 4, Mechero 1 y **Pico de madera** (y la hotbar
   inferior muestra los primeros 9 ítems).
2. **Cantidad parcial (clic derecho)**: con el inventario abierto, haz **clic
   derecho** sobre un stack (p. ej. Madera 10): el cursor debe mostrar
   «Madera x 1» y el slot bajar a 9. Un **clic izquierdo** mueve el stack
   completo. El clic derecho también funciona en los slots del grid de crafteo.
3. **Craftear tablones y palos**: con el cursor en «Madera x 1», clic derecho
   sobre el primer slot del grid (2x2, izquierda): la receta **Tablones** debe
   activarse (botón de resultado iluminado con su nombre). Pulsa el resultado
   para craftear. Repite con 2 tablones en vertical para **Palos** (4).
4. **Romper y recoger**: con el **pico de madera** seleccionado en la hotbar,
   rompe piedra o una mena (carbón, hierro, cobre, oro, diamante): el bloque
   debe caer como ítem y sumarse al inventario al recogerlo.
5. **Minerales**: mina bajo tierra (nivel del mar hacia abajo) con el pico:
   carbón (y ≈ 5-40 bajo el nivel del mar), hierro (≈ 5-20), cobre (≈ 5-20),
   oro (≈ 4-12) y diamante (≈ 2-8). Las menas se ven como bloques veteados en
   la piedra.

### 3.5 Verificar hotbar, rueda del ratón y apariencia de los bloques

1. **Hotbar sincronizada con el inventario**: pulsa **E**, mueve un ítem de los
   primeros 9 slots a otro hueco (clic izquierdo = stack completo, clic derecho =
   1 ítem) y cierra con **E**: la barra inferior debe reflejar el cambio al momento.
2. **Rueda del ratón**: dentro del mundo (ratón capturado), gira la rueda: el
   recuadro blanco de la hotbar se mueve y el bloque seleccionado cambia.
3. **Antorcha**: coloca una antorcha (tecla 7, clic derecho mirando al suelo):
   se ve como un **poste con llama** naranja/amarilla con **partículas de fuego**
   que suben y parpadean. De noche ilumina el suelo a su alrededor.
4. **Minerales**: mina con el pico: las menas se ven como **piedra con manchas**
   del color del metal (carbón oscuro, hierro, cobre, oro, diamante).
5. **Mesa y cofre**: craftea 4 tablones (1 tronco → 4) y haz la **Mesa de
   trabajo** (4 tablones en 2x2); colócala y acércate: el grid del inventario
   pasa a **3x3**. Con 8 tablones en anillo craftea el **Cofre** y colócalo: se
   ve la caja con tapa y cerradura.

---

## 4. Probar el cliente en Android (emulador)

### 4.1 Crear el emulador (una sola vez)

```powershell
# Variables de entorno (por si no están)
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"

# Instalar el emulador y una imagen de sistema (ej. API 35)
& "$env:ANDROID_HOME\cmdline-tools\latest\bin\sdkmanager.bat" "emulator" "system-images;android-35;google_apis;x86_64"

# Crear el dispositivo virtual (ej. "mvx" con forma de Pixel 5)
& "$env:ANDROID_HOME\cmdline-tools\latest\bin\avdmanager.bat" create avd -n mvx -k "system-images;android-35;google_apis;x86_64" -d pixel_5
```

> Si `sdkmanager` falla con `HTTP_PROXY` malformado, quita las variables antes:
> `Remove-Item Env:HTTP_PROXY, Env:HTTPS_PROXY`.

### 4.2 Arrancar el emulador

Con ventana (para verlo en vivo):

```powershell
& "$env:ANDROID_HOME\emulator\emulator.exe" -avd mvx -no-snapshot -no-audio -gpu swiftshader_indirect
```

Sin ventana (headless, para pruebas automatizadas):

```powershell
& "$env:ANDROID_HOME\emulator\emulator.exe" -avd mvx -no-snapshot -no-audio -gpu swiftshader_indirect -no-window
```

### 4.3 Compilar el APK (Release, autocontenido)

> Usa **Release**, no Debug: el APK Debug de .NET MAUI aborta al instalarse con
> `adb install` (Fast Deployment). El Release incluye todo en el APK.

```powershell
cd MundoVoxel
dotnet publish MundoVoxel.Client\MundoVoxel.Client.csproj -f net10.0-android -c Release
```

El APK queda en:

```
MundoVoxel.Client\bin\Release\net10.0-android\com.mundovoxel.app-Signed.apk
```

### 4.4 Instalar y lanzar

```powershell
adb install -r "MundoVoxel.Client\bin\Release\net10.0-android\com.mundovoxel.app-Signed.apk"
adb shell am start -n com.mundovoxel.app/crc64f25fc0dc0ba96806.MainActivity
```

> El nombre de la actividad puede variar; si falla, usa:
> `adb shell monkey -p com.mundovoxel.app 1` para abrir la app.

En el emulador el host (tu PC) se ve como **`10.0.2.2`**, no `127.0.0.1`.

### 4.5 Controles táctiles

En Android hay botones en pantalla: **Romper / Colocar / Saltar / Volar / Chat**,
más los controles de movimiento táctil. El menú de pausa e inventario funcionan
igual que en Windows.

---

## 5. Multijugador: Windows + Android en el mismo mundo

La prueba estrella: un **servidor dedicado** + el **cliente Windows** + el
**cliente Android (emulador)** dentro del **mismo mundo**, viéndose los
personajes.

### 5.1 Arrancar el servidor dedicado

```powershell
cd MundoVoxel
dotnet run --project MundoVoxel.Server\MundoVoxel.Server.csproj -c Release
```

Deja esta ventana abierta (verás los logs de conexiones y el puerto 25575).

> Si el servidor y los clientes están en el **mismo PC**, los tres usan la misma
> máquina y no hace falta abrir puertos.

### 5.2 Conectar el cliente Windows

1. En el menú escribe tu nombre (ej. `AnaPC`).
2. IP: `127.0.0.1` · Puerto: `25575`.
3. **«Conectar a un servidor»** → crea un mundo (o entra a uno existente).
4. Verás el mundo renderizado con el HUD completo.

### 5.3 Conectar el cliente Android

1. En el emulador, escribe tu nombre (ej. `BrunoMovil`).
2. IP: **`10.0.2.2`** (el emulador ve tu PC como 10.0.2.2) · Puerto: `25575`.
3. **«Conectar a un servidor»** → entra al **mismo mundo** que creó AnaPC.
4. Si ambos están en el mismo mundo, cada cliente verá al otro personaje
   (con su nombre encima) moverse en tiempo real.

> En un **teléfono físico** en la misma red Wi-Fi, usa la IP local del PC
> (compruébala con `ipconfig`) y el puerto 25575.

### 5.4 Comprobar que se ven

- Cada jugador aparece como un personaje voxel con su **nombre flotante**.
- Al moverte con WASD en Windows, el personaje se mueve en la pantalla de
  Android y viceversa.
- El chat (`T` en Windows / botón **Chat** en Android) llega a ambos.
- Romper/colocar bloques: los cambios se ven en ambas pantallas.

---

## 6. Comandos útiles de `adb` para pruebas

```powershell
adb shell screencap -p /sdcard/pan.png        # captura de pantalla
adb pull /sdcard/pan.png .                    # traerla al PC
adb shell input tap X Y                        # tocar en (X,Y)
adb shell input text "hola"                    # escribir texto
adb shell input keyevent 111                   # tecla ESC
adb logcat -d | Select-String "FATAL|Exception" # ver errores de la app
adb emu kill                                   # cerrar el emulador
```

---

## 7. Solución de problemas

| Problema | Causa probable | Solución |
|---|---|---|
| `adb install` aborta (SIGABRT) | APK Debug (Fast Deployment) | Publicar en **Release** (sección 4.3) |
| Android no conecta a `127.0.0.1` | El emulador tiene su propia red | Usar **`10.0.2.2`** |
| El servidor no acepta conexiones | Puerto ocupado | Cambiar `Servidor:Puerto` en `appsettings.json` del Server |
| `sdkmanager` falla | Proxy malformado | `Remove-Item Env:HTTP_PROXY, Env:HTTPS_PROXY` |
| Disco lleno al compilar | Cachés NuGet/obj | `dotnet clean` + `dotnet nuget locals all --clear` |
| No se ven los personajes | Mundos distintos | Entrar al **mismo** mundo (mismo id/nombre) desde ambos clientes |

---

## 8. Pruebas manuales antes de publicar

Lo que la suite automática no puede juzgar: hace falta pantalla y manos. Son las
cuatro comprobaciones que bloquean las etiquetas `v0.11.20` (streaming),
`v0.11.21` (cuentas), `v0.11.22` (delta) y `v0.11.23` (MSIX).

### 8.1 Streaming por proximidad (v0.11.20)

1. Arranca el servidor dedicado y el cliente Windows (secciones 5.1 y 5.2).
2. En `ajustes.config.json` deja `"RadioRegiones": 1` (el anillo por defecto es 3x3
   regiones de 64x64 columnas).
3. Dentro del mundo, camina en línea recta unos 30 segundos (o usa el modo
   espectador con `G` para volar y avanzar más rápido).
4. Qué mirar: el terreno aparece según te acercas **sin huecos** y no te caes a
   través de lo que aún llega (lo pendiente se trata como sólido). Al volver
   atrás, el trozo que dejas se descarga de la memoria del cliente.
5. No debe haber tirones ni errores en la consola del servidor ni en el cliente.
6. Coste en Android: sube `"RadioRegiones": 3` (7x7) y repite en el emulador. Si
   va fluido, se puede subir; si da tirones, se deja en 1. Con `adb logcat -d`
   se ven los avisos de la app mientras caminas.

### 8.2 Cuentas por jugador (v0.11.21)

1. Para el servidor y edita `ajustes.config.json`: `"CuentasObligatorias": true` y
   `"RegistroAbierto": true` (así puedes crear la primera cuenta). Arranca.
2. En el menú del cliente escribe usuario y clave y entra. Debe registrarte y
   crear el fichero `cuentas.json` junto a la configuración del servidor.
3. Comprueba: sin usuario o sin clave no se entra; con la clave mal no se entra y
   a los 5 intentos por minuto el servidor corta; el nombre conserva la grafía
   guardada (entra cambiando mayúsculas y mira cómo aparece en el chat).
4. Cambio de clave: en la partida, menú de pausa, cambia la clave pidiendo la
   actual. Al volver a entrar, la nueva vale y la vieja no.
5. Cierra `"RegistroAbierto": false` para que solo entren las cuentas ya creadas.

### 8.3 Reconexión rápida con delta (v0.11.22)

1. Entra al mundo y rompe o coloca un bloque para saber por dónde vas.
2. Corta la conexión: para el servidor con `Ctrl+C` en su ventana. El cliente
   avisa de la pérdida y abre el panel de reconexión (intento 1 de 3, con esperas
   de 2, 4 y 8 segundos).
3. Vuelve a arrancar el servidor antes de que se agoten los intentos. El cliente
   debe volver solo, **rápido** (el servidor solo reenvía las regiones que
   cambiaron) y en el mismo sitio, con el bloque que tocaste intacto.
4. Prueba del camino largo: cambia `"MinutosDeltaRapido"` a 0 (o espera más de
   5 minutos fuera) y repite: entonces el servidor reenvía el mundo entero (tarda
   más, pero debe verse igual de bien).
5. Botón **Cancelar** del panel: debe cerrar el panel, cortar el intento, volver
   al menú y dejar el mensaje de reconexión cancelada.
6. Repite en Android (mismo panel y mismo botón táctil).

### 8.4 Instalar el MSIX (v0.11.23)

El paquete va firmado. Con un certificado **autofirmado** de desarrollo, Windows
solo lo instala si confías en él; el almacén del usuario no basta.

1. Consigue el `.msix` y su `.cer` (del release, o compilando con
   `-p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true`, mirando en
   `MundoVoxel.Client\bin\Release\net10.0-windows10.0.19041.0\win-x64\AppPackages\`).
2. Desde una consola **como administrador**:

```powershell
Import-Certificate -FilePath .\MundoVoxel.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage .\mundovoxel.client_0.11.23.0_x64.msix
```

3. Lanza **MundoVoxel** desde el menú de inicio y comprueba en Configuración 
   Aplicaciones que la versión coincide con la etiqueta del release.
4. Desinstalar: `Get-AppxPackage com.mundovoxel.app | Remove-AppxPackage`.
5. Errores típicos: `0x800B0100` (el paquete no va firmado o la firma no se
   reconoce) y `0x800B0109` (la raíz no es de confianza: falta el paso 2). Para
   distribuir sin ese paso hay que firmar con un certificado de firma de código
   real (ver `docs/releases.md`).
