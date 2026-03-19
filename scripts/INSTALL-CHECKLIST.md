# HefestoForge — Checklist de Instalacion en RHEL

> **Prerequisito**: Acceso SSH con `sudo`.
> **Nota**: RHEL usa Podman emulando Docker (`docker` → `podman`). El servicio corre como `root`
> para evitar problemas de visibilidad de contenedores entre usuarios.

---

## Fase 1: Prerequisitos

```bash
cat /etc/redhat-release        # RHEL 8+
sudo dnf install -y git curl tar
```

- [ ] Git instalado
- [ ] `docker --version` responde (Podman emulando Docker o Docker CE)

> **.NET Runtime**: Solo si se publica framework-dependent.
> Con self-contained no se necesita .NET en el servidor.

---

## Fase 2: Directorios

```bash
sudo mkdir -p /opt/hefestoforge/logs
sudo mkdir -p /tmp/dockerize-builds
sudo mkdir -p /usr/share/containershareds
```

| Directorio | La API necesita |
|------------|----------------|
| `/opt/hefestoforge/logs/` | Escritura (logs Serilog) |
| `/opt/hefestoforge/template-overrides/` | Escritura (se crea al modificar templates) |
| `/tmp/dockerize-builds/` | Escritura (workspaces temporales por build) |
| `/usr/share/containershareds` | Lectura (shared files, vacio por ahora) |

- [ ] Directorios creados

### SELinux (solo si esta en Enforcing)

```bash
getenforce
# Si dice "Permissive" o "Disabled" → saltar
```

```bash
sudo semanage fcontext -a -t tmp_t "/tmp/dockerize-builds(/.*)?"
sudo restorecon -Rv /tmp/dockerize-builds

sudo semanage fcontext -a -t usr_t "/opt/hefestoforge(/.*)?"
sudo restorecon -Rv /opt/hefestoforge

sudo semanage fcontext -a -t usr_t "/usr/share/containershareds(/.*)?"
sudo restorecon -Rv /usr/share/containershareds
```

> **Diagnostico**: Si falla con "Permission denied" y los permisos Unix estan bien:
> `sudo ausearch -m avc -ts recent`

---

## Fase 3: Copiar la Aplicacion

Desde la maquina de desarrollo:

```bash
dotnet publish src/DockerizeAPI/DockerizeAPI.csproj -c Release -r linux-x64 --self-contained -o ./publish
scp -r ./publish/* usuario@IP_SERVIDOR:/tmp/hefestoforge-deploy/
```

En el servidor:

```bash
sudo cp -r /tmp/hefestoforge-deploy/* /opt/hefestoforge/
sudo chmod +x /opt/hefestoforge/DockerizeAPI
rm -rf /tmp/hefestoforge-deploy
```

- [ ] Binario ejecutable (`ls -la /opt/hefestoforge/DockerizeAPI`)
- [ ] `appsettings.json` presente

---

## Fase 4: Configurar appsettings.json

```bash
sudo nano /opt/hefestoforge/appsettings.json
```

**Cambiar solo la IP:**

```json
{
  "Server": {
    "Urls": "http://IP_DEL_SERVIDOR:5050"
  }
}
```

**Verificar que no diga `"UseWsl": true`** (debe ser false o no existir).

- [ ] IP configurada
- [ ] UseWsl no esta en true

---

## Fase 5: Servicio systemd

```bash
sudo tee /etc/systemd/system/HefestoForge.service > /dev/null << 'EOF'
[Unit]
Description=HefestoForge - Automated Docker Image Builder
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/hefestoforge
ExecStart=/opt/hefestoforge/DockerizeAPI
Restart=always
RestartSec=10

# Corre como root (Podman rootful, sin problemas de visibilidad de contenedores)
User=root
Group=root

Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_EnableDiagnostics=0

StandardOutput=journal
StandardError=journal
SyslogIdentifier=hefestoforge

LimitNOFILE=65536
TimeoutStartSec=30
TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable HefestoForge.service
```

- [ ] Servicio creado y habilitado

---

## Fase 6: Firewall

```bash
sudo firewall-cmd --add-port=5050/tcp --permanent
sudo firewall-cmd --reload
```

- [ ] Puerto 5050 abierto

---

## Fase 7: Iniciar y Verificar

```bash
sudo systemctl start HefestoForge.service
sudo systemctl status HefestoForge.service
```

- [ ] Estado: active (running)

```bash
sudo journalctl -u HefestoForge -n 30 --no-pager
# Debe decir: Now listening on: http://IP:5050
```

```bash
curl http://localhost:5050/health/live       # Healthy
curl http://localhost:5050/api/health        # JSON con status Healthy
curl http://localhost:5050/api/builds        # items: []
```

- [ ] Health check OK
- [ ] API responde

**Verificar visibilidad de contenedores** (cualquier usuario los ve):

```bash
docker run -d --name test-vis alpine sleep 60
docker ps | grep test-vis                   # debe aparecer
docker rm -f test-vis
```

- [ ] Contenedores visibles para todos

**Swagger**: `http://IP_DEL_SERVIDOR:5050/swagger`

- [ ] Swagger carga

---

## Fase 8: Conectividad al Registry

```bash
curl -k https://repos.daviviendahn.dvhn/api/v1/version
docker login -u token -p TOKEN repos.daviviendahn.dvhn
```

- [ ] Registry accesible

---

## Notas

**Shared files** (`/usr/share/containershareds`): vacio por ahora, se llenara despues.
Mientras este vacio, builds reales fallan pero sandbox funciona.

**Comandos del dia a dia:**

```bash
sudo systemctl status HefestoForge         # estado
sudo systemctl restart HefestoForge        # reiniciar
sudo journalctl -u HefestoForge -f         # logs en vivo
tail -f /opt/hefestoforge/logs/*.log        # logs Serilog
```

**Cambiar IP:**

```bash
sudo nano /opt/hefestoforge/appsettings.json    # editar Server:Urls
sudo systemctl restart HefestoForge
```

---

## Resumen Rapido (servidor nuevo, solo cambia IP)

```bash
sudo dnf install -y git curl tar
sudo mkdir -p /opt/hefestoforge/logs /tmp/dockerize-builds /usr/share/containershareds
# ... copiar archivos publicados a /opt/hefestoforge/ ...
sudo chmod +x /opt/hefestoforge/DockerizeAPI
sudo sed -i 's|http://[^"]*:5050|http://NUEVA_IP:5050|' /opt/hefestoforge/appsettings.json
# ... crear HefestoForge.service (ver Fase 5) ...
sudo systemctl daemon-reload
sudo systemctl enable --now HefestoForge.service
sudo firewall-cmd --add-port=5050/tcp --permanent && sudo firewall-cmd --reload
curl http://localhost:5050/health/live
```
