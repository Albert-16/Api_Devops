# HefestoForge — Checklist de Instalacion en RHEL

> **Prerequisito**: Acceso SSH con `sudo`.

---

## Fase 1: Prerequisitos

```bash
# SO
cat /etc/redhat-release        # RHEL 8+

# Paquetes base
sudo dnf install -y git curl tar

# Docker
sudo dnf config-manager --add-repo https://download.docker.com/linux/rhel/docker-ce.repo
sudo dnf install -y docker-ce docker-ce-cli containerd.io
sudo systemctl enable --now docker
```

- [ ] Git instalado
- [ ] Docker instalado y corriendo (`sudo systemctl status docker`)

**Verificar que Docker es rootful** (si es rootless, otros usuarios no ven los contenedores de la API):

```bash
docker info 2>/dev/null | grep "Docker Root Dir"
# Debe ser: /var/lib/docker
# Si dice /home/.../.local/share/docker → es rootless, NO sirve
```

- [ ] Docker Root Dir = `/var/lib/docker`

> **.NET Runtime**: Solo necesario si se publica framework-dependent.
> Si se publica self-contained, no se necesita .NET en el servidor.

---

## Fase 2: Usuario de Servicio

```bash
sudo useradd --system --shell /usr/sbin/nologin --create-home hefestoforge
sudo usermod -aG docker hefestoforge
```

- [ ] Usuario creado (`id hefestoforge`)
- [ ] En grupo docker (`groups hefestoforge`)

---

## Fase 3: Directorios y Permisos

```bash
# Crear
sudo mkdir -p /opt/hefestoforge/logs
sudo mkdir -p /tmp/dockerize-builds
sudo mkdir -p /usr/share/containershareds

# Owner
sudo chown -R hefestoforge:hefestoforge /opt/hefestoforge
sudo chown -R hefestoforge:hefestoforge /tmp/dockerize-builds
sudo chown -R hefestoforge:hefestoforge /usr/share/containershareds

# setgid: subdirectorios nuevos heredan el grupo del padre
# (sin esto, carpetas que crea la API pueden quedar sin permisos para otros)
sudo chmod 2755 /opt/hefestoforge
sudo chmod 2755 /opt/hefestoforge/logs
sudo chmod 2755 /tmp/dockerize-builds
```

| Directorio | La API necesita |
|------------|----------------|
| `/opt/hefestoforge/logs/` | Escritura (logs Serilog) |
| `/opt/hefestoforge/template-overrides/` | Escritura (se crea al modificar templates) |
| `/tmp/dockerize-builds/` | Escritura (workspaces temporales por build) |
| `/usr/share/containershareds` | Lectura (shared files, vacio por ahora) |
| `/var/run/docker.sock` | Acceso via grupo `docker` |

### SELinux (solo si esta en Enforcing)

```bash
getenforce
# Si dice "Permissive" o "Disabled" → saltar esta seccion
```

```bash
sudo semanage fcontext -a -t tmp_t "/tmp/dockerize-builds(/.*)?"
sudo restorecon -Rv /tmp/dockerize-builds

sudo semanage fcontext -a -t usr_t "/opt/hefestoforge(/.*)?"
sudo restorecon -Rv /opt/hefestoforge

sudo semanage fcontext -a -t usr_t "/usr/share/containershareds(/.*)?"
sudo restorecon -Rv /usr/share/containershareds
```

> **Diagnostico SELinux**: Si todo esta bien pero falla con "Permission denied":
> `sudo ausearch -m avc -ts recent`

### Verificacion de permisos

```bash
sudo -u hefestoforge touch /tmp/dockerize-builds/test && rm /tmp/dockerize-builds/test && echo "OK: tmp"
sudo -u hefestoforge touch /opt/hefestoforge/logs/test && rm /opt/hefestoforge/logs/test && echo "OK: logs"
sudo -u hefestoforge docker version > /dev/null 2>&1 && echo "OK: docker"
```

- [ ] Todos dicen OK

---

## Fase 4: Copiar la Aplicacion

Desde la maquina de desarrollo:

```bash
dotnet publish src/DockerizeAPI/DockerizeAPI.csproj -c Release -r linux-x64 --self-contained -o ./publish
scp -r ./publish/* usuario@IP_SERVIDOR:/tmp/hefestoforge-deploy/
```

En el servidor:

```bash
sudo cp -r /tmp/hefestoforge-deploy/* /opt/hefestoforge/
sudo chmod +x /opt/hefestoforge/DockerizeAPI
sudo chown -R hefestoforge:hefestoforge /opt/hefestoforge
rm -rf /tmp/hefestoforge-deploy
```

- [ ] Binario ejecutable (`ls -la /opt/hefestoforge/DockerizeAPI`)
- [ ] `appsettings.json` presente

---

## Fase 5: Configurar appsettings.json

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

## Fase 6: Servicio systemd

```bash
sudo tee /etc/systemd/system/HefestoForge.service > /dev/null << 'EOF'
[Unit]
Description=HefestoForge - Automated Docker Image Builder
After=network.target docker.service
Requires=docker.service

[Service]
Type=notify
WorkingDirectory=/opt/hefestoforge
ExecStart=/opt/hefestoforge/DockerizeAPI
Restart=always
RestartSec=10

User=hefestoforge
Group=hefestoforge

Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_EnableDiagnostics=0
UMask=0022

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

## Fase 7: Firewall

```bash
sudo firewall-cmd --add-port=5050/tcp --permanent
sudo firewall-cmd --reload
```

- [ ] Puerto 5050 abierto

---

## Fase 8: Iniciar y Verificar

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

**Verificar visibilidad Docker** (que otros usuarios vean contenedores de la API):

```bash
sudo -u hefestoforge docker run -d --name test-vis alpine sleep 60
sudo docker ps | grep test-vis              # debe aparecer
sudo -u hefestoforge docker rm -f test-vis
```

- [ ] Contenedores visibles para todos

**Swagger**: `http://IP_DEL_SERVIDOR:5050/swagger`

- [ ] Swagger carga

---

## Fase 9: Conectividad al Registry

```bash
curl -k https://repos.daviviendahn.dvhn/api/v1/version
sudo -u hefestoforge docker login -u token -p TOKEN repos.daviviendahn.dvhn
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
sudo dnf install -y git curl docker-ce docker-ce-cli containerd.io
sudo systemctl enable --now docker
sudo useradd --system --shell /usr/sbin/nologin --create-home hefestoforge
sudo usermod -aG docker hefestoforge
sudo mkdir -p /opt/hefestoforge/logs /tmp/dockerize-builds /usr/share/containershareds
sudo chown -R hefestoforge:hefestoforge /opt/hefestoforge /tmp/dockerize-builds /usr/share/containershareds
sudo chmod 2755 /opt/hefestoforge /opt/hefestoforge/logs /tmp/dockerize-builds
# ... copiar archivos publicados a /opt/hefestoforge/ ...
sudo chmod +x /opt/hefestoforge/DockerizeAPI
sudo chown -R hefestoforge:hefestoforge /opt/hefestoforge
sudo sed -i 's|http://[^"]*:5050|http://NUEVA_IP:5050|' /opt/hefestoforge/appsettings.json
# ... crear HefestoForge.service (ver Fase 6) ...
sudo systemctl daemon-reload
sudo systemctl enable --now HefestoForge.service
sudo firewall-cmd --add-port=5050/tcp --permanent && sudo firewall-cmd --reload
curl http://localhost:5050/health/live
```
