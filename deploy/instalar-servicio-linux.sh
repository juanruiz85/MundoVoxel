#!/usr/bin/env bash
# Instala MundoVoxel.Server como servicio systemd en Linux.
# Uso: sudo bash instalar-servicio-linux.sh [ruta-publicacion]
set -euo pipefail

PUB="${1:-/opt/mundovoxel}"

echo "==> Publicando servidor (linux-x64, autocontenido)..."
dotnet publish ../MundoVoxel.Server -c Release -r linux-x64 --self-contained -o "$PUB"

echo "==> Creando usuario de servicio 'mundovoxel' (si no existe)..."
id -u mundovoxel >/dev/null 2>&1 || useradd -r -s /usr/sbin/nologin mundovoxel

echo "==> Instalando unidad systemd..."
cp mundovoxel-server.service /etc/systemd/system/mundovoxel-server.service
chown -R mundovoxel:mundovoxel "$PUB"
systemctl daemon-reload
systemctl enable --now mundovoxel-server

echo "==> Abriendo puerto 25575/tcp en el cortafuegos (si hay ufw)..."
if command -v ufw >/dev/null 2>&1; then
  ufw allow 25575/tcp || true
fi

echo "==> Listo. Estado:"
systemctl status mundovoxel-server --no-pager || true
echo
echo "Logs: journalctl -u mundovoxel-server -f"
echo "Config: $PUB/appsettings.json  (reinicia con: systemctl restart mundovoxel-server)"
