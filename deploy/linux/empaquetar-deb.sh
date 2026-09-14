#!/usr/bin/env bash
# Construye el paquete .deb del servidor MundoVoxel a partir del publish.
# Uso: deploy/linux/empaquetar-deb.sh <dir_publicado> <version> [dir_salida]
set -euo pipefail
PUB="$1"
VER="$2"
OUT="${3:-.}"
ROOT="$(mktemp -d)"
mkdir -p "$ROOT/DEBIAN" "$ROOT/opt/mundovoxel" "$ROOT/lib/systemd/system"
cp -r "$PUB/." "$ROOT/opt/mundovoxel/"
cp deploy/linux/mundo-voxel.service "$ROOT/lib/systemd/system/"
sed "s/__VERSION__/$VER/" deploy/linux/deb/DEBIAN/control > "$ROOT/DEBIAN/control"
cp deploy/linux/deb/DEBIAN/postinst deploy/linux/deb/DEBIAN/prerm "$ROOT/DEBIAN/"
chmod 755 "$ROOT/DEBIAN/postinst" "$ROOT/DEBIAN/prerm"
find "$ROOT/opt" -type f -exec chmod 644 {} \;
chmod 755 "$ROOT/opt/mundovoxel/MundoVoxel.Server"
dpkg-deb --build --root-owner-group "$ROOT" "$OUT/MundoVoxel-Server-linux-x64-${VER}.deb"
rm -rf "$ROOT"
echo "OK: $OUT/MundoVoxel-Server-linux-x64-${VER}.deb"