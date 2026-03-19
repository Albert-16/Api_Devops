# HefestoForge — Checklist de Instalacion en RHEL

> **Prerequisito**: Acceso SSH con `sudo`.
> **Nota**: RHEL usa Podman emulando Docker (`docker` → `podman`). El servicio corre como `root`
> para evitar problemas de visibilidad de contenedores entre usuarios.
> **.NET 10** debe estar instalado en el servidor (`dotnet --version` → 10.x).

---

## Fase 1: Prerequisitos

```bash
cat /etc/redhat-release        # RHEL 8+
dotnet --version               # 10.x
docker --version               # Podman emulando Docker o Docker CE
git --version
```

- [ ] .NET 10 instalado
- [ ] Docker/Podman disponible
- [ ] Git instalado

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
dotnet publish src/DockerizeAPI/DockerizeAPI.csproj -c Release -o ./publish
```

> Framework-dependent: el zip es mas liviano, .NET ya esta en el servidor.

Subir al servidor (zip, scp, o como prefieran):

```bash
scp -r ./publish/* usuario@IP_SERVIDOR:/tmp/hefestoforge-deploy/
```

En el servidor:

```bash
sudo cp -r /tmp/hefestoforge-deploy/* /opt/hefestoforge/
rm -rf /tmp/hefestoforge-deploy
```

- [ ] `DockerizeAPI.dll` presente (`ls /opt/hefestoforge/DockerizeAPI.dll`)
- [ ] `appsettings.json` presente

---

## Fase 4: Configurar appsettings.json

```bash
sudo nano /opt/hefestoforge/appsettings.json
```

**Verificar:**
- `"UseWsl"` no este en `true` (debe ser `false` o no existir)
- La URL se controla en el `.service` (`ASPNETCORE_URLS=http://0.0.0.0:5050`), no en appsettings

- [ ] UseWsl no esta en true

---

## Fase 5: Servicio systemd

```bash
sudo nano /etc/systemd/system/HefestoForge.service
```

Pegar el siguiente contenido:

```ini
[Unit]
Description=Api .NET Hefesto Forge
After=network.target

[Service]
WorkingDirectory=/opt/hefestoforge
ExecStart=/usr/lib64/dotnet/dotnet /opt/hefestoforge/DockerizeAPI.dll
Restart=always
RestartSec=2
KillSignal=SIGQUIT
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5050

[Install]
WantedBy=multi-user.target
```

Guardar: `Ctrl+O` → `Enter` → `Ctrl+X`

```bash
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

**Cambiar IP (si necesario):**

```bash
sudo nano /etc/systemd/system/HefestoForge.service    # editar ASPNETCORE_URLS
sudo systemctl daemon-reload
sudo systemctl restart HefestoForge
```

---

## Resumen Rapido (servidor nuevo)

```bash
sudo mkdir -p /opt/hefestoforge/logs /tmp/dockerize-builds /usr/share/containershareds
# ... copiar archivos publicados a /opt/hefestoforge/ ...
# ... crear HefestoForge.service (ver Fase 5) ...
sudo systemctl daemon-reload
sudo systemctl enable --now HefestoForge.service
sudo firewall-cmd --add-port=5050/tcp --permanent && sudo firewall-cmd --reload
curl http://localhost:5050/health/live
```
