#!/bin/bash
# =============================================================================
# HefestoForge - Script de Instalacion Automatizada
# API de Automatizacion CI/CD - Banco Davivienda Honduras
# Equipo de Arquitectura
# =============================================================================

set -euo pipefail

# ─── Colores ──────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()    { echo -e "${BLUE}[INFO]${NC}  $1"; }
log_ok()      { echo -e "${GREEN}[OK]${NC}    $1"; }
log_warn()    { echo -e "${YELLOW}[WARN]${NC}  $1"; }
log_error()   { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# ─── Configuracion ────────────────────────────────────────────────────────────
INSTALL_USER="${INSTALL_USER:-$(whoami)}"
INSTALL_DIR="/home/${INSTALL_USER}/HefestoForge"
REPO_URL="http://166.178.5.253/HefestoForge.git"
SERVICE_NAME="HefestoForge"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
DOTNET_INSTALL_DIR="/usr/local/dotnet"
SHARED_DIR="/usr/share/containershareds"
BUILD_TMP_DIR="/var/tmp/dockerize-builds"
PORT=5050

# ─── Banner ───────────────────────────────────────────────────────────────────
echo ""
echo -e "${BLUE}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║        HefestoForge  -  Instalacion              ║${NC}"
echo -e "${BLUE}║   API CI/CD  |  Davivienda Honduras              ║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════════════╝${NC}"
echo ""

# ─── Verificar ejecucion como root ────────────────────────────────────────────
if [[ $EUID -ne 0 ]]; then
    log_error "Este script debe ejecutarse como root (sudo $0)"
fi

# ─── Confirmar usuario de instalacion ─────────────────────────────────────────
echo -e "Usuario de despliegue detectado: ${YELLOW}${INSTALL_USER}${NC}"
read -rp "¿Es correcto? (Enter para confirmar o escribe otro usuario): " INPUT_USER
if [[ -n "$INPUT_USER" ]]; then
    INSTALL_USER="$INPUT_USER"
    INSTALL_DIR="/home/${INSTALL_USER}/HefestoForge"
fi

if ! id "$INSTALL_USER" &>/dev/null; then
    log_error "El usuario '${INSTALL_USER}' no existe en el sistema."
fi
log_ok "Usuario de despliegue: ${INSTALL_USER}"

# ─── Paso 1: Verificar prerrequisitos del sistema ─────────────────────────────
echo ""
log_info "=== Paso 1: Verificando prerrequisitos ==="

# SELinux
SELINUX_STATUS=$(getenforce 2>/dev/null || echo "Disabled")
if [[ "$SELINUX_STATUS" == "Enforcing" ]]; then
    log_ok "SELinux en modo enforcing"
elif [[ "$SELINUX_STATUS" == "Permissive" ]]; then
    log_warn "SELinux en modo permissive. Se recomienda enforcing en produccion."
else
    log_warn "SELinux no activo. El script continuara pero los contextos SELinux no aplicaran."
fi

# firewalld
if systemctl is-active --quiet firewalld; then
    log_ok "firewalld activo"
else
    log_warn "firewalld no esta activo. El puerto ${PORT} puede no estar expuesto."
fi

# ─── Paso 2: Instalar dependencias ────────────────────────────────────────────
echo ""
log_info "=== Paso 2: Instalando dependencias ==="

log_info "Instalando git..."
dnf install -y git &>/dev/null && log_ok "git instalado"

# policycoreutils para semanage (solo si SELinux esta activo)
if [[ "$SELINUX_STATUS" != "Disabled" ]]; then
    if ! command -v semanage &>/dev/null; then
        log_info "Instalando policycoreutils-python-utils (requerido para semanage)..."
        dnf install -y policycoreutils-python-utils &>/dev/null \
            && log_ok "policycoreutils-python-utils instalado"
    else
        log_ok "semanage ya disponible"
    fi
fi

# ─── Paso 3: Verificar / instalar .NET 10 Runtime ─────────────────────────────
echo ""
log_info "=== Paso 3: Verificando .NET 10 Runtime ==="

DOTNET_BIN=""
if command -v dotnet &>/dev/null; then
    DOTNET_VERSION=$(dotnet --version 2>/dev/null || true)
    log_ok ".NET ya instalado: ${DOTNET_VERSION}"
    DOTNET_BIN=$(command -v dotnet)
elif [[ -f "${DOTNET_INSTALL_DIR}/dotnet" ]]; then
    log_ok ".NET encontrado en ${DOTNET_INSTALL_DIR}"
    DOTNET_BIN="${DOTNET_INSTALL_DIR}/dotnet"
else
    log_info ".NET no encontrado. Intentando via dnf..."
    if dnf install -y dotnet-runtime-10.0 &>/dev/null; then
        log_ok ".NET 10 Runtime instalado via dnf"
        DOTNET_BIN=$(command -v dotnet)
    else
        log_warn "dnf no pudo instalar .NET 10. Instalando via script oficial..."
        wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
        chmod +x /tmp/dotnet-install.sh
        /tmp/dotnet-install.sh --runtime dotnet --version 10.0.0 \
            --install-dir "$DOTNET_INSTALL_DIR" &>/dev/null
        echo "export DOTNET_ROOT=${DOTNET_INSTALL_DIR}" > /etc/profile.d/dotnet.sh
        echo "export PATH=\$PATH:${DOTNET_INSTALL_DIR}" >> /etc/profile.d/dotnet.sh
        export DOTNET_ROOT="${DOTNET_INSTALL_DIR}"
        export PATH="$PATH:${DOTNET_INSTALL_DIR}"
        DOTNET_BIN="${DOTNET_INSTALL_DIR}/dotnet"
        log_ok ".NET 10 Runtime instalado en ${DOTNET_INSTALL_DIR}"
    fi
fi

# ─── Paso 4: Descargar y descomprimir el binario ──────────────────────────────
echo ""
log_info "=== Paso 4: Clonando HefestoForge ==="

# Detener servicio si ya existe
if systemctl is-active --quiet "${SERVICE_NAME}.service" 2>/dev/null; then
    log_info "Deteniendo servicio existente..."
    systemctl stop "${SERVICE_NAME}.service"
fi

# Limpiar instalacion anterior si existe
if [[ -d "$INSTALL_DIR" ]]; then
    log_warn "Directorio ${INSTALL_DIR} ya existe. Haciendo backup..."
    mv "$INSTALL_DIR" "${INSTALL_DIR}_bak_$(date +%Y%m%d_%H%M%S)"
fi

log_info "Clonando desde ${REPO_URL} ..."
if ! git clone "$REPO_URL" "$INSTALL_DIR"; then
    log_error "No se pudo clonar ${REPO_URL}. Verifica conectividad al servidor de build."
fi
log_ok "Repositorio clonado correctamente"

if [[ ! -f "${INSTALL_DIR}/DockerizeAPI.dll" ]]; then
    log_warn "DockerizeAPI.dll no encontrado en la raiz. El repo puede requerir publicacion previa."
fi
chown -R "${INSTALL_USER}:${INSTALL_USER}" "$INSTALL_DIR"
log_ok "Repositorio listo en ${INSTALL_DIR}"

# ─── Paso 5: Crear directorios de trabajo ─────────────────────────────────────
echo ""
log_info "=== Paso 5: Creando directorios de trabajo ==="

mkdir -p "$BUILD_TMP_DIR"
chmod 777 "$BUILD_TMP_DIR"
log_ok "Directorio de builds temporales: ${BUILD_TMP_DIR}"

mkdir -p "$SHARED_DIR"
log_ok "Directorio containershareds: ${SHARED_DIR}"

# Advertencia sobre archivos requeridos
echo ""
log_warn "IMPORTANTE: Debes copiar manualmente los siguientes archivos a ${SHARED_DIR}:"
echo -e "  ${YELLOW}►${NC} ca-davivienda.crt    (certificado CA corporativo - OBLIGATORIO)"
echo -e "  ${YELLOW}►${NC} nuget.config         (feed NuGet interno de Gitea - OBLIGATORIO)"
echo -e "  ${YELLOW}►${NC} wget                 (paquete .deb de wget - solo si ODBC=true)"
echo ""
read -rp "¿Deseas copiarlos ahora? (s/N): " COPY_SHARED
if [[ "${COPY_SHARED,,}" == "s" ]]; then
    read -rp "  Ruta de ca-davivienda.crt: " SRC_CERT
    [[ -f "$SRC_CERT" ]] && cp "$SRC_CERT" "${SHARED_DIR}/ca-davivienda.crt" \
        && log_ok "ca-davivienda.crt copiado" \
        || log_warn "Archivo no encontrado: ${SRC_CERT}"

    read -rp "  Ruta de nuget.config: " SRC_NUGET
    [[ -f "$SRC_NUGET" ]] && cp "$SRC_NUGET" "${SHARED_DIR}/nuget.config" \
        && log_ok "nuget.config copiado" \
        || log_warn "Archivo no encontrado: ${SRC_NUGET}"

    read -rp "  Ruta de paquete wget .deb (Enter para omitir): " SRC_WGET
    if [[ -n "$SRC_WGET" && -f "$SRC_WGET" ]]; then
        cp "$SRC_WGET" "${SHARED_DIR}/wget" && log_ok "wget copiado"
    fi
fi

# ─── Paso 6: Contexto SELinux para containershareds ──────────────────────────
echo ""
log_info "=== Paso 6: Aplicando contexto SELinux a containershareds ==="

if [[ "$SELINUX_STATUS" != "Disabled" ]] && command -v semanage &>/dev/null; then
    semanage fcontext -a -t var_t "/usr/share/containershareds(/.*)?" 2>/dev/null \
        || semanage fcontext -m -t var_t "/usr/share/containershareds(/.*)?" 2>/dev/null \
        || true
    restorecon -Rv "$SHARED_DIR" &>/dev/null
    log_ok "Contexto SELinux aplicado a ${SHARED_DIR}"
else
    log_warn "SELinux no activo o semanage no disponible. Contexto SELinux omitido."
fi

# ─── Paso 7: Crear el servicio systemd ────────────────────────────────────────
echo ""
log_info "=== Paso 7: Creando servicio systemd ==="

# Determinar ruta absoluta de dotnet para systemd
DOTNET_EXEC="$DOTNET_BIN"
if [[ "$DOTNET_EXEC" != /* ]]; then
    DOTNET_EXEC=$(command -v dotnet 2>/dev/null || echo "${DOTNET_INSTALL_DIR}/dotnet")
fi

cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=Api .NET Hefesto Forge
After=network.target

[Service]
WorkingDirectory=${INSTALL_DIR}
ExecStart=${DOTNET_EXEC} ${INSTALL_DIR}/DockerizeAPI.dll
Restart=always
RestartSec=2
KillSignal=SIGQUIT
User=${INSTALL_USER}
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:${PORT}
Environment=DOTNET_ROOT=${DOTNET_INSTALL_DIR}
Environment=PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:${DOTNET_INSTALL_DIR}

[Install]
WantedBy=multi-user.target
EOF

# Verificar que el archivo no tiene caracteres extraños al inicio
FIRST_CHAR=$(head -c 1 "$SERVICE_FILE" | xxd -p)
if [[ "$FIRST_CHAR" != "5b" ]]; then
    log_error "El archivo .service tiene caracteres inesperados al inicio."
fi
log_ok "Archivo de servicio creado en ${SERVICE_FILE}"

# ─── Paso 8: Contexto SELinux para el binario ─────────────────────────────────
echo ""
log_info "=== Paso 8: Aplicando contexto SELinux al binario ==="

if [[ "$SELINUX_STATUS" != "Disabled" ]] && command -v semanage &>/dev/null; then
    semanage fcontext -a -t bin_t "/home/${INSTALL_USER}/HefestoForge(/.*)?" 2>/dev/null \
        || semanage fcontext -m -t bin_t "/home/${INSTALL_USER}/HefestoForge(/.*)?" 2>/dev/null \
        || true
    restorecon -Rv "$INSTALL_DIR" &>/dev/null
    log_ok "Contexto SELinux (bin_t) aplicado a ${INSTALL_DIR}"
else
    log_warn "SELinux no activo. Contexto SELinux omitido para el binario."
fi

# ─── Paso 9: Habilitar y arrancar el servicio ─────────────────────────────────
echo ""
log_info "=== Paso 9: Habilitando e iniciando el servicio ==="

systemctl daemon-reload
systemctl enable "${SERVICE_NAME}.service"
systemctl start "${SERVICE_NAME}.service"

sleep 3

if systemctl is-active --quiet "${SERVICE_NAME}.service"; then
    log_ok "Servicio ${SERVICE_NAME} activo y corriendo"
else
    log_error "El servicio no arranco. Revisa los logs: sudo journalctl -u ${SERVICE_NAME}.service -n 50"
fi

# ─── Paso 10: Abrir puerto en firewall ────────────────────────────────────────
echo ""
log_info "=== Paso 10: Configurando firewall ==="

if systemctl is-active --quiet firewalld; then
    if firewall-cmd --list-ports | grep -q "${PORT}/tcp"; then
        log_ok "Puerto ${PORT}/tcp ya estaba abierto"
    else
        firewall-cmd --permanent --add-port="${PORT}/tcp" &>/dev/null
        firewall-cmd --reload &>/dev/null
        log_ok "Puerto ${PORT}/tcp abierto en firewalld"
    fi
else
    log_warn "firewalld no activo. Puerto ${PORT} no configurado."
fi

# ─── Paso 11: Verificacion final ──────────────────────────────────────────────
echo ""
log_info "=== Paso 11: Verificacion del despliegue ==="

sleep 2
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    --max-time 5 "http://localhost:${PORT}/health" 2>/dev/null || echo "000")

if [[ "$HTTP_CODE" == "200" ]]; then
    log_ok "Health check OK (HTTP ${HTTP_CODE}) -> http://localhost:${PORT}/health"
else
    log_warn "Health check retorno HTTP ${HTTP_CODE}. Puede necesitar unos segundos para iniciar."
    log_warn "Verifica manualmente: curl http://localhost:${PORT}/health"
fi

# ─── Resumen ──────────────────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║           Instalacion completada                 ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "  Binario:      ${INSTALL_DIR}"
echo -e "  Servicio:     ${SERVICE_FILE}"
echo -e "  Puerto:       ${PORT}"
echo -e "  Usuario:      ${INSTALL_USER}"
echo -e "  Shared files: ${SHARED_DIR}"
echo -e "  Build tmp:    ${BUILD_TMP_DIR}"
echo ""
echo -e "  ${YELLOW}Comandos utiles:${NC}"
echo -e "  sudo systemctl status ${SERVICE_NAME}.service"
echo -e "  sudo journalctl -u ${SERVICE_NAME}.service -f"
echo -e "  curl http://localhost:${PORT}/health"
echo ""
