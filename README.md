# DockerizeAPI

API REST (.NET 10) para construccion automatizada de imagenes Docker y despliegue de containers. Clona repositorios Git, genera Dockerfiles desde plantillas, construye imagenes, las publica al Gitea Container Registry y despliega containers con gestion completa del ciclo de vida.

## Inicio rapido

```bash
# Clonar y compilar
git clone <repo-url>
cd Api_Devops
dotnet build DockerizeAPI.slnx

# Ejecutar en desarrollo (puerto 5050)
cd src/DockerizeAPI
dotnet run

# Ejecutar tests
dotnet test

# Docker Compose (produccion, puerto 8080)
docker-compose up -d
```

La API estara disponible en:
- **Desarrollo:** `http://localhost:5050`
- **Produccion:** `http://<server-ip>:8080`
- **Swagger UI:** `http://localhost:5050/swagger`

---

## Modulo Build — Construccion de imagenes

Pipeline asincrono: clone → detect .csproj → generate Dockerfile → docker build → docker login → docker push → cleanup.

### Crear un build

```bash
POST /api/builds
```

```json
{
  "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/ms23-auth.git",
  "branch": "main",
  "gitToken": "tu-token-gitea",
  "imageConfig": {
    "includeOdbcDependencies": false,
    "customTag": "v1.0.0"
  }
}
```

**Respuesta (202 Accepted):**

```json
{
  "buildId": "a1b2c3d4-...",
  "status": "Queued",
  "repositoryUrl": "https://repos.daviviendahn.dvhn/...",
  "branch": "main",
  "createdAt": "2026-02-26T10:00:00Z"
}
```

### Otros endpoints de Build

| Metodo | Ruta | Descripcion |
|--------|------|-------------|
| `GET` | `/api/builds` | Listar builds (paginado). Query params: `page`, `pageSize`, `status`, `branch`, `repositoryUrl` |
| `GET` | `/api/builds/{buildId}` | Detalle de un build con logs |
| `GET` | `/api/builds/{buildId}/logs` | Logs en tiempo real (SSE) o JSON si ya termino |
| `DELETE` | `/api/builds/{buildId}` | Cancelar build en progreso |
| `POST` | `/api/builds/{buildId}/retry` | Reintentar build fallido |
| `POST` | `/api/builds/preview-dockerfile` | Preview del Dockerfile sin ejecutar build |

### Templates de Dockerfile

```bash
GET /api/templates/alpine    # Template ligero (sin ODBC)
GET /api/templates/odbc      # Template con dependencias AS400/ODBC
```

---

## Modulo Deploy — Despliegue de containers en linux

Pipeline asincrono: docker login → docker pull → (stop + rm si existe) → docker run → verificar → Running.

Incluye auto-rollback: si el container no arranca y hay una imagen anterior, revierte automaticamente.

### Crear un deploy

```bash
POST /api/deploys
```

**Request minimo:**

```json
{
  "imageName": "repos.daviviendahn.dvhn/davivienda-banco/ms23-auth:v1.0.0",
  "gitToken": "tu-token-gitea",
  "containerName": "ms23-auth"
}
```

**Request completo con todas las opciones:**

```json
{
  "imageName": "repos.daviviendahn.dvhn/davivienda-banco/ms23-auth:v1.0.0",
  "gitToken": "tu-token-gitea",
  "containerName": "ms23-auth",
  "volumes": ["/host/data:/app/data", "/host/logs:/app/logs"],
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Production",
    "ConnectionStrings__Default": "Server=db;Database=auth;..."
  },
  "restartPolicy": "UnlessStopped",
  "onFailureMaxRetries": 3,
  "detached": true,
  "interactive": false
}
```

**Respuesta (202 Accepted):**

```json
{
  "deployId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "Queued",
  "imageName": "repos.daviviendahn.dvhn/davivienda-banco/ms23-auth:v1.0.0",
  "containerName": "ms23-auth",
  "deployVersion": 1,
  "isRollback": false,
  "createdAt": "2026-02-26T10:00:00Z",
  "completedAt": null,
  "containerId": null
}
```

### Campos del request

| Campo | Tipo | Requerido | Default | Descripcion |
|-------|------|-----------|---------|-------------|
| `imageName` | string | Si | — | Imagen completa con registry y tag |
| `gitToken` | string | Si | — | Token Gitea para autenticacion al registry |
| `containerName` | string | Si | — | Nombre del container (solo `[a-zA-Z0-9_.-]`, inicia con alfanumerico) |
| `ports` | string[] | No | `[]` | Mapeo de puertos `"host:container"` |
| `volumes` | string[] | No | `[]` | Montajes de volumenes `"/host:/container"` |
| `environmentVariables` | object | No | `{}` | Variables de entorno clave-valor |
| `network` | string? | No | `null` | Red Docker (`--network`). Si no se especifica, no se agrega el flag |
| `restartPolicy` | enum | No | `"Always"` | Politica de reinicio: `No`, `Always`, `UnlessStopped`, `OnFailure` |
| `onFailureMaxRetries` | int | No | `0` | Reintentos maximos para politica `OnFailure` |
| `detached` | bool | No | `true` | Ejecutar en modo detached (`-d`) |
| `interactive` | bool | No | `false` | Ejecutar en modo interactivo (`-i`) |

### Network y Ports

`network` es opcional. Si no se especifica, no se agrega `--network` al comando `docker run` y Docker usa su default (bridge). La idea es simple: **si mandas `ports`, no necesitas especificar `network`**. Los campos son independientes y mapean directamente a flags de `docker run`.

```bash
# Con puertos — controlas en que puerto del host escucha (3050:8080 = host:container)
{
  "containerName": "ms23-auth",
  "ports": ["3050:8080"]
}
# Genera: docker run -d --name ms23-auth -p 3050:8080 imagen

# Con network host — el container usa la red del host directamente, sin mapeo
{
  "containerName": "ms23-auth",
  "network": "host"
}
# Genera: docker run -d --name ms23-auth --network host imagen

# Sin ports ni network — sin flags de red
{
  "containerName": "ms23-auth"
}
# Genera: docker run -d --name ms23-auth imagen
```

### Ciclo de vida del deploy

```
Queued → LoggingIn → Pulling → Deploying → Running
                                    ↓
                                  Failed (con auto-rollback si hay imagen anterior)
```

**Estados posibles:**

| Estado | Valor | Descripcion |
|--------|-------|-------------|
| `Queued` | 0 | En cola, esperando ser procesado |
| `LoggingIn` | 1 | Autenticando contra el Container Registry |
| `Pulling` | 2 | Descargando la imagen |
| `Deploying` | 3 | Ejecutando `docker run` |
| `Running` | 4 | Container corriendo exitosamente |
| `Stopped` | 5 | Container detenido |
| `Failed` | 6 | Fallo en algun paso (ver `errorMessage`) |
| `Cancelled` | 7 | Cancelado por el usuario |

### Container existente

Si ya existe un container con el mismo nombre, el pipeline automaticamente:
1. Guarda la imagen y configuracion actual como `previousImageName` / `previousConfig`
2. Incrementa el `deployVersion`
3. Detiene el container (`docker stop`)
4. Elimina el container (`docker rm`)
5. Crea el nuevo container con `docker run`

### Consultar estado de un deploy

```bash
GET /api/deploys/{deployId}
```

**Respuesta (200 OK):**

```json
{
  "deployId": "f47ac10b-...",
  "status": "Running",
  "imageName": "repos.daviviendahn.dvhn/davivienda-banco/ms23-auth:v1.0.0",
  "containerName": "ms23-auth",
  "errorMessage": null,
  "containerId": "a1b2c3d4e5f6",
  "restartPolicy": "UnlessStopped",
  "network": "bridge",
  "ports": ["8080:80"],
  "volumes": ["/host/data:/app/data"],
  "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Production" },
  "deployVersion": 2,
  "previousImageName": "repos.daviviendahn.dvhn/davivienda-banco/ms23-auth:v0.9.0",
  "isRollback": false,
  "createdAt": "2026-02-26T10:00:00Z",
  "startedAt": "2026-02-26T10:00:01Z",
  "completedAt": "2026-02-26T10:00:15Z",
  "retryCount": 0,
  "logs": [
    { "message": "Autenticando en registry: repos.daviviendahn.dvhn", "level": "info", "timestamp": "..." },
    { "message": "Imagen descargada exitosamente", "level": "info", "timestamp": "..." },
    { "message": "Container iniciado: ms23-auth (ID: a1b2c3d4e5f6)", "level": "info", "timestamp": "..." }
  ]
}
```

### Listar deploys

```bash
GET /api/deploys?page=1&pageSize=20&status=Running&containerName=ms23-auth
```

**Query params opcionales:**

| Param | Tipo | Descripcion |
|-------|------|-------------|
| `page` | int | Pagina (default: 1) |
| `pageSize` | int | Elementos por pagina (default: 20) |
| `status` | enum | Filtrar por estado: `Queued`, `Running`, `Failed`, etc. |
| `containerName` | string | Filtrar por nombre de container |
| `imageName` | string | Filtrar por nombre de imagen |

### Logs en tiempo real (SSE)

```bash
GET /api/deploys/{deployId}/logs
```

- Si el deploy esta **en progreso** (`Queued`, `LoggingIn`, `Pulling`, `Deploying`): retorna **Server-Sent Events** en tiempo real.
- Si el deploy ya **termino** (`Running`, `Stopped`, `Failed`, `Cancelled`): retorna los logs almacenados como **JSON**.

**Ejemplo con curl (SSE):**

```bash
curl -N http://localhost:5050/api/deploys/{deployId}/logs
```

```
data: Autenticando en registry: repos.daviviendahn.dvhn
data: Autenticacion exitosa en registry: repos.daviviendahn.dvhn
data: Descargando imagen: repos.daviviendahn.dvhn/davivienda-banco/ms23-auth:v1.0.0
data: Container iniciado: ms23-auth (ID: a1b2c3d4e5f6)
```

### Gestionar containers

```bash
# Detener container (solo si esta Running)
POST /api/deploys/{deployId}/stop

# Reiniciar container (si esta Running o Stopped)
POST /api/deploys/{deployId}/restart

# Eliminar container (stop + rm)
DELETE /api/deploys/{deployId}

# Inspeccionar container (docker inspect)
GET /api/deploys/{deployId}/inspect
```

**Stop y Restart** retornan `204 No Content` si es exitoso.

**Inspect** retorna el JSON crudo de `docker inspect`:

```json
{
  "deployId": "f47ac10b-...",
  "containerName": "ms23-auth",
  "inspectJson": "[{\"Id\": \"a1b2c3...\", \"State\": {\"Status\": \"running\"}, ...}]"
}
```

### Rollback

Revierte a la imagen anterior del deploy. Solo funciona si el deploy tiene un `previousImageName` guardado (es decir, se desplego sobre un container existente).

```bash
POST /api/deploys/{deployId}/rollback
```

**Respuesta (200 OK):** Retorna un nuevo `DeployResponse` con `isRollback: true`.

**Errores posibles:**
- `404` — Deploy no encontrado
- `409` — No hay version anterior disponible para rollback

**Auto-rollback:** Si durante el deploy el container no arranca (falla `docker run` o no pasa la verificacion `IsContainerRunning`), y hay una imagen anterior guardada, el sistema ejecuta rollback automaticamente sin intervencion manual.

---

## Flujo completo: Build + Deploy

Ejemplo de como construir una imagen y luego desplegarla:

```bash
# 1. Construir la imagen
curl -X POST http://localhost:5050/api/builds \
  -H "Content-Type: application/json" \
  -d '{
    "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/ms23-auth.git",
    "branch": "main",
    "gitToken": "tu-token"
  }'
# Respuesta: { "buildId": "aaa-bbb-ccc", "status": "Queued", ... }

# 2. Monitorear el build (SSE)
curl -N http://localhost:5050/api/builds/aaa-bbb-ccc/logs

# 3. Cuando el build termine (status: Completed), desplegar
#    network: host por default — la app escucha directamente en el puerto del host
curl -X POST http://localhost:5050/api/deploys \
  -H "Content-Type: application/json" \
  -d '{
    "imageName": "repos.daviviendahn.dvhn/davivienda-banco/ms23-auth:latest",
    "gitToken": "tu-token",
    "containerName": "ms23-auth",
    "restartPolicy": "UnlessStopped"
  }'
# Respuesta: { "deployId": "ddd-eee-fff", "status": "Queued", ... }

# 4. Monitorear el deploy (SSE)
curl -N http://localhost:5050/api/deploys/ddd-eee-fff/logs

# 5. Verificar que esta corriendo
curl http://localhost:5050/api/deploys/ddd-eee-fff
# status: "Running", containerId: "a1b2c3..."

# 6. Actualizar a nueva version (automaticamente reemplaza el container anterior)
curl -X POST http://localhost:5050/api/deploys \
  -H "Content-Type: application/json" \
  -d '{
    "imageName": "repos.daviviendahn.dvhn/davivienda-banco/ms23-auth:v2.0.0",
    "gitToken": "tu-token",
    "containerName": "ms23-auth",
    "restartPolicy": "UnlessStopped"
  }'
# deployVersion: 2, previousImageName: "...ms23-auth:latest"

# 7. Si algo sale mal, rollback a la version anterior
curl -X POST http://localhost:5050/api/deploys/ggg-hhh-iii/rollback
```

---

## Configuracion

### appsettings.json

```json
{
  "Server": {
    "Urls": "http://+:8080",
    "EnableSwagger": true
  },
  "Registry": {
    "Url": "repos.daviviendahn.dvhn",
    "Owner": "davivienda-banco"
  },
  "Build": {
    "MaxConcurrentBuilds": 3,
    "TimeoutMinutes": 10,
    "TempDirectory": "/tmp/dockerize-builds",
    "SharedFilesPath": "/usr/share/containershareds",
    "UseWsl": false
  },
  "Deploy": {
    "MaxConcurrentDeploys": 3,
    "TimeoutMinutes": 5
  }
}
```

### Variables de entorno (override)

Cualquier configuracion se puede sobreescribir con variables de entorno usando `__` como separador:

```bash
Server__Urls=http://+:9090
Build__MaxConcurrentBuilds=5
Deploy__MaxConcurrentDeploys=2
Deploy__TimeoutMinutes=10
Registry__Url=mi-registry.ejemplo.com
```

### Desarrollo en Windows (WSL)

En `appsettings.Development.json`, `"UseWsl": true` ejecuta los comandos Docker a traves de WSL2. Esto convierte automaticamente rutas Windows (`D:\path`) a rutas WSL (`/mnt/d/path`).

---

## Docker Compose (produccion)

```bash
docker-compose up -d
docker-compose logs -f dockerize-api
```

El container:
- Expone puerto `8080`
- Monta el Docker socket (`/var/run/docker.sock`) para ejecutar docker build/push/run
- Se ejecuta como usuario no-root
- Health check en `/health/live`
- Restart policy: `unless-stopped`

---

## Health check

```bash
GET /api/health
```

Retorna `200 OK` con `{ "status": "healthy", "timestamp": "..." }`.

---

## Codigos de error comunes

| Codigo | Significado |
|--------|-------------|
| `202` | Operacion aceptada y en cola (build o deploy) |
| `204` | Operacion exitosa sin contenido (stop, restart, remove) |
| `400` | Request invalido (validacion fallida) |
| `404` | Build, deploy o container no encontrado |
| `409` | Conflicto de estado (ej: intentar detener un deploy que no esta Running, rollback sin imagen anterior) |
| `500` | Error interno del servidor |

Todos los errores usan formato `ProblemDetails` (RFC 7807):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Recurso no encontrado",
  "status": 404,
  "detail": "Deploy f47ac10b-... no encontrado."
}
```

---

## Tests

```bash
# Todos los tests (91)
dotnet test

# Solo tests de deploy
dotnet test --filter "FullyQualifiedName~Deploy"

# Solo tests de build
dotnet test --filter "FullyQualifiedName~Build"

# Test especifico
dotnet test --filter "FullyQualifiedName~DeployStoreTests"
```
