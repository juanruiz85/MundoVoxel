# Publicar una release - MundoVoxel

Cómo se generan los binarios y qué secretos hay que configurar. El repositorio es `juanruiz85/MundoVoxel`.

## Cortar una versión

1. Anota los cambios en `changelog.md` (encabezado `## [x.y.z] - AAAA-MM-DD`): de esa sección salen las notas del release.
2. Commit y push a `main`.
3. Crea y publica el tag: `git tag v0.11.23` y `git push origin v0.11.23`.
4. El workflow `release.yml` construye todo (los cuatro jobs en paralelo, unos 20-30 minutos) y crea el release con los binarios y las notas.

## Qué se publica

| Asset | Qué es |
| --- | --- |
| `MundoVoxel-Server-linux-x64-<ver>.tar.gz` | servidor autocontenido para Linux (dentro va la unidad systemd y el INSTALL.txt) |
| `MundoVoxel-Server-linux-x64-<ver>.deb` | lo mismo empaquetado como .deb |
| `MundoVoxel-Server-win-x64-<ver>.zip` | servidor autocontenido para Windows |
| `MundoVoxel-Cliente-Windows-<ver>.zip` | cliente Windows sin instalar (carpeta suelta) |
| `MundoVoxel-<ver>.msix` | paquete de Windows (aparece en el menú Inicio y se desinstala desde Configuración) |
| `MundoVoxel.cer` | certificado con el que va firmado el MSIX (solo si no hay certificado propio en secretos) |
| `MundoVoxel-<ver>.apk` | cliente Android (firmado con tu keystore si están los secretos) |

## El MSIX de Windows

Se genera pidiendo el empaquetado (`-p:WindowsPackageType=MSIX`) a partir de `MundoVoxel.Client/Platforms/Windows/Package.appxmanifest` y sus iconos. El publish normal (el zip) sigue siendo la carpeta suelta de siempre: empaquetar solo se pide en el workflow de release.

Como va firmado con un certificado autofirmado de desarrollo, Windows no lo abre por sí solo. Antes de instalarlo:

1. Descarga también `MundoVoxel.cer`.
2. Clic derecho > **Instalar certificado** > *Equipo local* > **Entidades de certificación raíz de confianza** (o **Personas de confianza**).
3. Abre el `.msix` (o clic derecho > Instalar).

Alternativa: activar el **modo para desarrolladores** de Windows y abrirlo desde ahí.

Si algún día hay un certificado de firma de código de verdad, se sube a los secretos y el workflow lo usa sin tocar el YAML.

### Instalar el MSIX

El MSIX va firmado, pero con un certificado autofirmado de desarrollo Windows solo lo instala si confía en él. No basta con el almacén del usuario: AppX valida contra los almacenes **del equipo**, así que hay que confiar el `.cer` desde una consola **como administrador**:

```powershell
Import-Certificate -FilePath .\MundoVoxel.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Sin ese paso, `Add-AppxPackage` falla con `0x80073CF0` / `0x800B0100` (sin firmar) o con `0x800B0109` (raíz no confiable). Para que el paquete se instale sin tocar nada, hay que firmarlo con un certificado de firma de código real: el PFX en base64 en `MSIX_CERT_B64` y su clave en `MSIX_CERT_PASS`. La versión del paquete se toma del tag (`v0.11.23` -> `0.11.23.0`) y el `versionCode` del APK también (`v0.11.23` -> `1123`), de modo que las releases se pueden actualizar en sitio.
## Secretos (opcionales)

| Secreto | Qué es |
| --- | --- |
| `MSIX_CERT_B64` | el `.pfx` de firma en base64 |
| `MSIX_CERT_PASS` | la clave del `.pfx` |
| `ANDROID_KEYSTORE_B64` | el keystore de Android en base64 |
| `ANDROID_KEYSTORE_PASS` | la clave del keystore |
| `ANDROID_KEY_ALIAS` | el alias de la clave de release |
| `ANDROID_KEY_PASS` | la clave del alias |

Para generar el de Android (una vez; el keystore se guarda fuera del repo):

```
keytool -genkeypair -v -keystore release.keystore -alias mundovoxel -keyalg RSA -keysize 2048 -validity 10000
certutil -encode release.keystore release.b64
```

y se pega el contenido de `release.b64` (sin la primera y la última línea, que son adornos de certutil) en `ANDROID_KEYSTORE_B64`. Ojo: si algún día se pierde ese keystore, las versiones nuevas no podrán actualizar las instaladas (Android solo acepta la misma firma).


## Comprobar la firma del APK

Con las herramientas del SDK (`apksigner`, en `build-tools`) se puede ver con qué certificado va firmado un APK:

```
apksigner verify --print-certs MundoVoxel-0.11.23.apk
```

Sale el nombre del firmante (`Signer #1 certificate DN`) y, con `-v`, los esquemas de firma usados (v1, v2 y v3). Si el APK se firmó con el keystore propio, ahí aparece su nombre; si se firmó con la clave de depuración, aparece `Android Debug`.

## Comprobaciones antes de taggear

- `dotnet build MundoVoxel.Pruebas` y la suite en local (o dejar que la haga el CI del push).
- Que `changelog.md` tenga la sección de esa versión: si no, el release sale sin notas.
- Un tag viejo re-ejecutado sube los binarios otra vez al mismo release (mejor cortar una versión nueva).