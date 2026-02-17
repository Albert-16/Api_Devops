# DockerizeAPI

Servicio de Dockerizacion Automatizada para Davivienda Honduras.

API REST interna (sin autenticacion) para ambientes de desarrollo y UAT que automatiza la construccion y publicacion de imagenes Docker en el Gitea Container Registry.

## Descripcion

DockerizeAPI recibe la URL de un repositorio Gitea, clona el codigo fuente, selecciona y adapta el Dockerfile template correcto segun los parametros (Alpine sin ODBC o Debian con ODBC), construye la imagen con Buildah y la publica en el Gitea Container Registry (`repos.daviviendahn.dvhn/davivienda-banco/`).

### Caracteristicas principales

- **Dos templates optimizados**: Alpine (ligero ~86 MiB) y Debian+ODBC (~200+ MiB para AS400/SQL Server)
- **Build asincrono**: Procesamiento en segundo plano con Channel y Background Service
- **Logs en tiempo real**: Soporte SSE (Server-Sent Events) para ver logs de build en vivo
- **Deteccion automatica**: Encuentra el proyecto .NET principal y extrae AssemblyName
- **Rate limiting**: Maximo de N builds simultaneos configurable
- **Buildah nativo**: No requiere Docker daemon, usa Buildah 1.41.8

## Prerequisitos

- **Buildah** >= 1.41.8
- **Podman** (opcional, como alternativa para gestion de contenedores)
- **Git** (para clonar repositorios)
- **.NET 10 SDK** (solo para desarrollo local)
- **Docker/Podman** (para ejecutar la API dockerizada)

## Estructura del Proyecto

```
Api_Devops/
├── DockerizeAPI.sln                         # Solucion .NET
├── Dockerfile                               # Dockerfile de la propia API
├── docker-compose.yml                       # Docker Compose para despliegue
├── .gitea/workflows/
│   ├── auto-deploy-sapp.yml                 # Workflow Gitea Actions SAPP
│   └── auto-deploy-bel.yml                  # Workflow Gitea Actions BEL
└── src/DockerizeAPI/
    ├── DockerizeAPI.csproj                   # Archivo de proyecto
    ├── Program.cs                            # Punto de entrada de la API
    ├── appsettings.json                      # Configuracion principal
    ├── appsettings.Development.json          # Configuracion para desarrollo
    ├── Endpoints/
    │   ├── BuildEndpoints.cs                 # Endpoints de builds (CRUD + logs + retry)
    │   ├── TemplateEndpoints.cs              # Endpoints de templates (get + update)
    │   └── HealthEndpoints.cs                # Health check
    ├── Services/
    │   ├── BuildService.cs                   # Servicio central de gestion de builds
    │   ├── BuildahService.cs                 # Interaccion con Buildah (build, push, login)
    │   ├── GitService.cs                     # Clonacion de repositorios
    │   ├── TemplateService.cs                # Gestion de templates de Dockerfile
    │   ├── DockerfileGenerator.cs            # Generacion de Dockerfiles desde templates
    │   └── ProjectDetector.cs                # Deteccion del proyecto .NET principal
    ├── Models/
    │   ├── BuildInfo.cs                      # Entidad principal de un build
    │   ├── Configuration/
    │   │   ├── BuildSettings.cs              # Configuracion de builds
    │   │   ├── RegistrySettings.cs           # Configuracion del registry
    │   │   └── OdbcSettings.cs               # Configuracion de paquetes ODBC
    │   ├── Enums/
    │   │   ├── BuildStatus.cs                # Estados del build
    │   │   ├── NetworkMode.cs                # Modos de red
    │   │   └── ProgressMode.cs               # Modos de progreso
    │   ├── Requests/
    │   │   ├── CreateBuildRequest.cs          # Request para crear build
    │   │   ├── UpdateTemplateRequest.cs       # Request para actualizar template
    │   │   └── PreviewDockerfileRequest.cs    # Request para preview de Dockerfile
    │   └── Responses/
    │       ├── BuildResponse.cs              # Respuesta de build
    │       ├── BuildListResponse.cs          # Respuesta paginada de builds
    │       ├── TemplateResponse.cs           # Respuesta de template
    │       ├── PreviewDockerfileResponse.cs  # Respuesta de preview
    │       ├── HealthResponse.cs             # Respuesta de health check
    │       └── ErrorResponse.cs              # Respuesta de error
    ├── Templates/
    │   ├── Dockerfile.alpine.template        # Template Alpine (sin ODBC)
    │   └── Dockerfile.odbc.template          # Template Debian (con ODBC)
    ├── BackgroundServices/
    │   └── BuildProcessorService.cs          # Procesador de builds en segundo plano
    ├── Extensions/
    │   ├── ServiceExtensions.cs              # Registro de servicios DI
    │   └── EndpointExtensions.cs             # Registro de endpoints
    └── Middleware/
        ├── GlobalExceptionMiddleware.cs      # Manejo global de excepciones
        └── RateLimitingMiddleware.cs         # Rate limiting por builds simultaneos
```

## Instalacion y Despliegue

### Opcion 1: Docker Compose (recomendada)

```bash
# Clonar el repositorio
git clone https://repos.daviviendahn.dvhn/davivienda-banco/Api_Devops.git
cd Api_Devops

# Levantar con docker-compose
docker-compose up -d

# Verificar que esta corriendo
curl http://localhost:8080/api/health
```

### Opcion 2: Buildah + Podman

```bash
# Construir la imagen con Buildah
buildah bud --tag dockerize-api:latest .

# Ejecutar con Podman
podman run -d \
  --name dockerize-api \
  --privileged \
  --security-opt seccomp=unconfined \
  --device /dev/fuse \
  -p 8080:8080 \
  -v ./logs:/app/logs \
  dockerize-api:latest
```

### Opcion 3: Desarrollo local

```bash
cd src/DockerizeAPI
dotnet restore
dotnet run
```

La API estara disponible en `http://localhost:8080`. Swagger UI en `http://localhost:8080/swagger`.

## Endpoints

### 1. POST /api/builds - Iniciar nuevo build

Crea un nuevo build y lo encola para procesamiento asincrono.

```bash
curl -X POST http://localhost:8080/api/builds \
  -H "Content-Type: application/json" \
  -d '{
    "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web",
    "branch": "sapp-dev",
    "gitToken": "TU_GITEA_TOKEN",
    "imageConfig": {
      "includeOdbcDependencies": false
    }
  }'
```

**Build con ODBC (Debian):**

```bash
curl -X POST http://localhost:8080/api/builds \
  -H "Content-Type: application/json" \
  -d '{
    "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/ms45-consulta-as400",
    "branch": "sapp-dev",
    "gitToken": "TU_GITEA_TOKEN",
    "imageConfig": {
      "includeOdbcDependencies": true,
      "noCache": false,
      "pull": true
    }
  }'
```

**Respuesta:**

```json
{
  "buildId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Queued",
  "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web",
  "branch": "sapp-dev",
  "createdAt": "2026-02-17T15:30:00Z"
}
```

### 2. GET /api/builds/{buildId} - Consultar estado

```bash
curl http://localhost:8080/api/builds/a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Respuesta (build completado):**

```json
{
  "buildId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Completed",
  "imageUrl": "repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web:sapp-dev",
  "createdAt": "2026-02-17T15:30:00Z",
  "cloningStartedAt": "2026-02-17T15:30:01Z",
  "buildingStartedAt": "2026-02-17T15:30:05Z",
  "pushingStartedAt": "2026-02-17T15:31:20Z",
  "completedAt": "2026-02-17T15:31:35Z"
}
```

### 3. GET /api/builds - Historial de builds

```bash
# Listar todos los builds
curl "http://localhost:8080/api/builds?page=1&pageSize=10"

# Filtrar por estado
curl "http://localhost:8080/api/builds?status=Completed"

# Filtrar por rama
curl "http://localhost:8080/api/builds?branch=sapp-dev"
```

### 4. DELETE /api/builds/{buildId} - Cancelar build

```bash
curl -X DELETE http://localhost:8080/api/builds/a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

### 5. GET /api/builds/{buildId}/logs - Logs del build

**Logs completos (JSON):**

```bash
curl http://localhost:8080/api/builds/a1b2c3d4-e5f6-7890-abcd-ef1234567890/logs
```

**Logs en tiempo real (SSE):**

```bash
curl -N -H "Accept: text/event-stream" \
  http://localhost:8080/api/builds/a1b2c3d4-e5f6-7890-abcd-ef1234567890/logs
```

### 6. POST /api/builds/{buildId}/retry - Reintentar build fallido

```bash
curl -X POST http://localhost:8080/api/builds/a1b2c3d4-e5f6-7890-abcd-ef1234567890/retry
```

### 7. GET /api/templates/alpine - Template Alpine actual

```bash
curl http://localhost:8080/api/templates/alpine
```

### 8. GET /api/templates/odbc - Template ODBC actual

```bash
curl http://localhost:8080/api/templates/odbc
```

### 9. PUT /api/templates/{templateName} - Actualizar template

```bash
curl -X PUT http://localhost:8080/api/templates/alpine \
  -H "Content-Type: application/json" \
  -d '{
    "content": "FROM alpine:latest\nWORKDIR /app\n..."
  }'
```

### 10. POST /api/builds/preview-dockerfile - Preview del Dockerfile

```bash
curl -X POST http://localhost:8080/api/builds/preview-dockerfile \
  -H "Content-Type: application/json" \
  -d '{
    "includeOdbcDependencies": false,
    "csprojPath": "1.1-Presentation/Microservices.csproj",
    "assemblyName": "Microservices"
  }'
```

### 11. GET /api/health - Health check

```bash
curl http://localhost:8080/api/health
```

**Respuesta:**

```json
{
  "status": "Healthy",
  "timestamp": "2026-02-17T15:30:00Z",
  "version": "1.0.0",
  "buildahAvailable": true,
  "buildahVersion": "buildah version 1.41.8",
  "activeBuilds": 1,
  "maxConcurrentBuilds": 3
}
```

## Prerequisitos del Repositorio

Cada repositorio que se va a dockerizar debe contener estos archivos:

### Siempre requeridos

| Archivo/Carpeta | Proposito |
|---|---|
| `.tmp/nuget/` | Paquetes NuGet locales (.nupkg) de Davivienda |
| `.tmp/certificates/ca/ca-davivienda.crt` | Certificado CA corporativo |
| `nuget.config` | Configuracion de feeds NuGet |
| `*.csproj` | Archivo de proyecto .NET |

### Solo si includeOdbcDependencies = true

| Archivo/Carpeta | Proposito |
|---|---|
| `.tmp/wget/` | Paquetes .deb de wget para descargar key del repo Debian |

## Configuracion

La configuracion se maneja via `appsettings.json` y puede sobreescribirse con variables de entorno:

| Variable de Entorno | Descripcion | Default |
|---|---|---|
| `Registry__Url` | URL del Container Registry | `repos.daviviendahn.dvhn` |
| `Registry__Owner` | Organizacion en Gitea | `davivienda-banco` |
| `Build__MaxConcurrentBuilds` | Builds simultaneos maximos | `3` |
| `Build__TimeoutMinutes` | Timeout por build (minutos) | `10` |
| `Build__TempDirectory` | Directorio para builds temporales | `/tmp/dockerize-builds` |

## Gitea Actions Workflows

Se incluyen dos workflows para despliegue automatico:

- **auto-deploy-sapp.yml**: Trigger en push a `sapp-dev` y `sapp-uat`
- **auto-deploy-bel.yml**: Trigger en push a `bel-dev` y `bel-uat`

Las ramas `-prd` estan excluidas por seguridad (deploy manual).

### Secrets requeridos en Gitea

| Secret | Descripcion |
|---|---|
| `GITEA_TOKEN` | Token de acceso a Gitea (tambien se usa como REPO_TOKEN) |
| `REGISTRY_URL` | URL del Container Registry (ej: `repos.daviviendahn.dvhn`) |
| `DOCKERIZE_API_URL` | URL base de DockerizeAPI (ej: `http://dockerize-api:8080`) |

## Troubleshooting

### El build falla con error SSL/TLS

Verificar que el repositorio incluya el certificado CA corporativo en `.tmp/certificates/ca/ca-davivienda.crt`.

### Error "No se encontro ningun archivo .csproj"

El repositorio no contiene archivos de proyecto .NET. Verificar que el repositorio sea un proyecto .NET valido.

### Buildah no disponible (health check "Degraded")

Buildah no esta instalado en el contenedor. Verificar que se esta usando la imagen correcta con Buildah instalado.

### Error "limite de builds simultaneos"

Se estan ejecutando demasiados builds. Esperar a que terminen o aumentar `Build__MaxConcurrentBuilds`.

### Build timeout

El build tardo mas del limite configurado. Aumentar `Build__TimeoutMinutes` o verificar la conectividad de red.

### Error en builds ODBC: "wget not found"

El repositorio necesita la carpeta `.tmp/wget/` con los paquetes .deb de wget para builds con ODBC.

## Glosario

| Termino | Descripcion |
|---|---|
| **Buildah** | Herramienta de construccion de imagenes sin daemon (alternativa a Docker) |
| **Container Registry** | Almacen de imagenes Docker (en Gitea: seccion Paquetes) |
| **Multi-Stage Build** | Dockerfile con etapa de compilacion (SDK) y etapa de ejecucion (runtime) |
| **Alpine** | Distribucion Linux ligera (~5 MB) usada para imagenes sin ODBC |
| **Debian** | Distribucion Linux usada para imagenes con ODBC (ibm-iaccess) |
| **ODBC** | Estandar de conexion a bases de datos (AS400, SQL Server) |
| **SSE** | Server-Sent Events para streaming de logs en tiempo real |
| **Channel** | Mecanismo de .NET para comunicacion asincrona producer-consumer |
| **NuGet** | Gestor de paquetes de .NET |
| **Build Arg** | Parametro que se pasa al construir la imagen (no queda en imagen final) |

## Stack Tecnologico

- .NET 10 con Minimal API
- Buildah 1.41.8
- Serilog (logging estructurado)
- Swagger/OpenAPI (documentacion interactiva)
- Gitea Actions (CI/CD)
