#!/bin/bash
# ============================================================
# Setup script para instalar Buildah + Git en WSL Ubuntu
# Ejecutar DESPUÉS de reiniciar Windows y completar WSL setup
#
# Uso desde PowerShell:
#   wsl bash /mnt/d/Proyectos/Api_Devops/scripts/setup-wsl-buildah.sh
# ============================================================

set -euo pipefail

echo "=== Actualizando paquetes ==="
sudo apt-get update -y

echo "=== Instalando Buildah + Git + dependencias ==="
sudo apt-get install -y \
    buildah \
    git \
    fuse-overlayfs \
    slirp4netns \
    uidmap

echo "=== Configurando almacenamiento rootless ==="
mkdir -p ~/.config/containers
cat > ~/.config/containers/storage.conf << 'EOF'
[storage]
driver = "overlay"

[storage.options]
mount_program = "/usr/bin/fuse-overlayfs"
EOF

echo "=== Verificando instalación ==="
echo "Buildah version: $(buildah --version)"
echo "Git version: $(git --version)"

echo ""
echo "=== Setup completado ==="
echo "Para probar: buildah version"
echo "Para usar desde Windows: wsl buildah <comando>"
