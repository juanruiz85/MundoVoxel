# Auditoria de seguridad - MundoVoxel

Fecha: 2026-09-05. Alcance: superficie de ataque del protocolo TCP (puerto 25575),
persistencia de mundos, inventario/crafteo y moderacion. Revision estatica del
codigo + verificacion con la suite automatizada (MundoVoxel.Pruebas).

## Hallazgos verificados sin accion requerida

| Area | Estado |
|---|---|
| Ids de mundo | Los archivos .mundo usan el Id interno (GUID generado por el servidor), nunca el nombre escrito por el usuario: sin path traversal por nombre de mundo. La carga usa Path.GetFileNameWithoutExtension. |
| Validacion de acciones | Romper/colocar/usar validan distancia (7 bloques), limites del mundo, bloque objetivo, y que el jugador este vivo y no en espectador. |
| Anti-cheat de colocacion | Colocar exige TENER el bloque (0.10.5); los items no colocables se rechazan en servidor y cliente. |
| Chat | Limpieza de caracteres de control, tope de 200 caracteres y anti-flood (0.10.6). |
| Nombres de jugador | Recortados a 20 caracteres al conectar. |
| Capacidad | Topes de mundos y jugadores por mundo (appsettings); mundo lleno responde LLENO. |
| Anti-cheat de movimiento | Opt-in por config: salto maximo y velocidad sostenida maxima (0.10.6). |

## Endurecido en 0.10.7

1. **Anti-autoclick (GolpearMob)**: cooldown de 250 ms por conexion; los golpes
   dentro del cooldown se ignoran. Antes un cliente modificado podia enviar
   cientos de GolpearMob por segundo y drenar la salud de cualquier mob al
   instante (mataleo instantaneo, incluidos creepers a distancia de 5).
2. **Fuerza bruta de PIN (Unirse)**: maximo 5 claves erradas por minuto y por
   conexion en mundos privados (error MUCHOS_INTENTOS). Antes se podian probar
   las 10.000 claves de 4 digitos sin limite.
3. **Mineria por golpes (Romper)**: los bloques duros exigen varios golpes
   validados por el servidor (piedra 5 a mano, 2 con pico basico, 1 con
   hierro/diamante; madera 3 a mano, 1 con hacha; tierra/arena 2 a mano, 1 con
   pala). Un bot ya no puede vaciar el mapa a razon de un bloque por mensaje.

## Riesgos aceptados y recomendaciones (pendientes)

| Riesgo | Detalle | Mitigacion propuesta |
|---|---|---|
| Descompresion de mundos | Mundo.Descomprimir no acota el tamano descomprimido: un archivo .mundo manipulado localmente podria pedir mucha memoria al cargar. Riesgo LOCAL (los archivos los escribe el propio servidor). | Acotar el tamano descomprimido a un tope (p. ej. 64 MB) al cargar. |
| PIN de 4 digitos | El tope por conexion frena un cliente, pero N conexiones dan N x 5 intentos/minuto. | PIN de 6 digitos o invitaciones con token unico. |
| Transporte sin cifrar | El protocolo es TCP plano: en internet un intermediario puede leer posiciones y chat. | TLS opcional o tunel SSH/VPN para partidas por internet. |
| Sin autenticacion de identidad | Cualquiera puede conectar con cualquier nombre. | Tokens de sesion o contrasenas por jugador si se abre a internet. |
| Cooldown de golpe generoso | 250 ms coincide con el ritmo del cliente legitimo; un bot con delays de 251 ms sigue siendo eficaz. | Revisar junto con una futura animacion/barra de ataque. |
| Progreso de rotura compartido | El contador de golpes es por posicion del mundo: otro jugador puede "ayudar" a romper un bloque ajeno. | Vincular el progreso al jugador si molesta en PvP. |

## Nota de entorno (builds en shells recortados)

El restore de NuGet 7.6 falla si el shell no define las variables de Windows
(ProgramFiles(x86), ProgramData, CommonProgramFiles). Lanzar dotnet con esas
variables prefijadas (ver docs/guia-de-pruebas.md).