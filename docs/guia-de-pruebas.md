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
con los mundos grandes (192×64×192) y el ciclo dinámico de mobs (día/noche)
activos.

```powershell
cd MundoVoxel
dotnet run --project MundoVoxel.Pruebas\MundoVoxel.Pruebas.csproj -c Release
```

Resultado esperado (última línea):

```
PRUEBAS SUPERADAS
```

> Es una prueba con red local real (TCP en 127.0.0.1), así que tarda un poco
> (~1-2 min, más si el trigo tarda en madurar). No cierres la ventana antes de
> ver el resultado. La suite es repetible: debe salir verde en corridas
> consecutivas. Si alguna vez falla el trigo o un crafteo, espera y vuelve a
> ejecutar (la carga del servidor con 2 mundos grandes puede atrasar respuestas;
> los espectadores son intocables para que Ana sobreviva a las esperas largas).

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
