#!/bin/bash
# ============================================================
# Script de instalación de DockerizeAPI en Linux
# Uso: sudo bash install-linux.sh
# ============================================================
set -euo pipefail

APP_DIR="/opt/dockerize-api"
SERVICE_USER="dockerize"
SERVICE_NAME="dockerize-api"
TEMP_DIR="/tmp/dockerize-builds"

echo "=== Verificando prerequisitos ==="

# Verificar buildah
if ! command -v buildah &> /dev/null; then
    echo "ERROR: Buildah no está instalado. Instálelo primero:"
    echo "  Ubuntu/Debian: sudo apt-get install -y buildah"
    echo "  RHEL/CentOS:   sudo dnf install -y buildah"
    exit 1
fi
echo "  Buildah: $(buildah --version)"

# Verificar git
if ! command -v git &> /dev/null; then
    echo "ERROR: Git no está instalado. Instálelo primero:"
    echo "  sudo apt-get install -y git"
    exit 1
fi
echo "  Git: $(git --version)"

echo ""
echo "=== Creando usuario de servicio ==="
if id "$SERVICE_USER" &>/dev/null; then
    echo "  Usuario '$SERVICE_USER' ya existe"
else
    sudo useradd --system --shell /usr/sbin/nologin --create-home "$SERVICE_USER"
    echo "  Usuario '$SERVICE_USER' creado"
fi

# Configurar subuid/subgid para Buildah rootless
if ! grep -q "$SERVICE_USER" /etc/subuid 2>/dev/null; then
    sudo usermod --add-subuids 100000-165535 "$SERVICE_USER"
    echo "  subuid configurado para $SERVICE_USER"
fi
if ! grep -q "$SERVICE_USER" /etc/subgid 2>/dev/null; then
    sudo usermod --add-subgids 100000-165535 "$SERVICE_USER"
    echo "  subgid configurado para $SERVICE_USER"
fi

echo ""
echo "=== Configurando directorios ==="
sudo mkdir -p "$APP_DIR"
sudo mkdir -p "$TEMP_DIR"
sudo mkdir -p "$APP_DIR/logs"

echo ""
echo "=== Copiando archivos de la aplicación ==="
# Copiar desde el directorio publish (debe existir en ./publish/)
if [ -d "./publish" ]; then
    sudo cp -r ./publish/* "$APP_DIR/"
    sudo chmod +x "$APP_DIR/DockerizeAPI"
    echo "  Archivos copiados desde ./publish/ a $APP_DIR"
else
    echo "  ADVERTENCIA: No se encontró ./publish/"
    echo "  Asegúrese de copiar los archivos publicados a $APP_DIR manualmente."
    echo "  Comando: dotnet publish src/DockerizeAPI -c Release -r linux-x64 --self-contained -o ./publish"
fi

echo ""
echo "=== Ajustando permisos ==="
sudo chown -R "$SERVICE_USER":"$SERVICE_USER" "$APP_DIR"
sudo chown -R "$SERVICE_USER":"$SERVICE_USER" "$TEMP_DIR"

echo ""
echo "=== Instalando servicio systemd ==="
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
sudo cp "$SCRIPT_DIR/dockerize-api.service" /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable "$SERVICE_NAME"
echo "  Servicio '$SERVICE_NAME' instalado y habilitado"

echo ""
echo "=== Configuración de Buildah para el usuario $SERVICE_USER ==="
sudo -u "$SERVICE_USER" mkdir -p /home/$SERVICE_USER/.config/containers
sudo -u "$SERVICE_USER" bash -c 'cat > /home/'"$SERVICE_USER"'/.config/containers/storage.conf << EOF
[storage]
driver = "overlay"
EOF'
echo "  Storage de Buildah configurado"

echo ""
echo "============================================================"
echo "  Instalación completada"
echo "============================================================"
echo ""
echo "  Comandos útiles:"
echo "    sudo systemctl start $SERVICE_NAME      # Iniciar"
echo "    sudo systemctl stop $SERVICE_NAME       # Detener"
echo "    sudo systemctl status $SERVICE_NAME     # Estado"
echo "    sudo journalctl -u $SERVICE_NAME -f     # Ver logs"
echo ""
echo "  La API estará disponible en: http://localhost:5050"
echo "  Health check: curl http://localhost:5050/api/health"
echo ""
echo "  Si necesita ajustar la configuración:"
echo "    sudo nano $APP_DIR/appsettings.json"
echo "    sudo systemctl restart $SERVICE_NAME"
echo ""
