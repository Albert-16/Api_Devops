# DockerizeAPI - Guia de Despliegue y Uso

## Indice

1. [Requisitos Previos](#1-requisitos-previos)
2. [Configuracion del Servidor](#2-configuracion-del-servidor)
3. [Configuracion de la API](#3-configuracion-de-la-api)
4. [Despliegue](#4-despliegue)
5. [Verificacion Post-Despliegue](#5-verificacion-post-despliegue)
6. [Uso de la API](#6-uso-de-la-api)
7. [Monitoreo de Builds](#7-monitoreo-de-builds)
8. [Gestion de Templates](#8-gestion-de-templates)
9. [Configuraciones Avanzadas](#9-configuraciones-avanzadas)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Requisitos Previos

### En el servidor Linux

- **.NET 10 SDK** instalado (para compilar la API)
- **.NET 10 ASP.NET Runtime** (si se despliega como binario publicado)
- **Buildah 1.33+** instalado y funcionando en modo rootless
- **Git** instalado
- Acceso de red a:
  - `repos.daviviendahn.dvhn` (Gitea — repositorios y registry)
  - Feeds NuGet internos (si aplica)
- Espacio en disco suficiente en `/tmp/dockerize-builds` para builds concurrentes

### En un servidor Windows (alternativa)

- Todo lo anterior, mas:
- **WSL2** con una distribucion Linux (Ubuntu recomendado)
- **Buildah** instalado dentro de WSL
- Configurar `Build:UseWsl = true` en `appsettings.Development.json`

### El GitToken (Token de Gitea)

La API requiere un **token de acceso de Gitea** que se envia en cada solicitud de build.
Este token se usa para tres operaciones:

| Operacion | Como se usa |
|-----------|-------------|
| Clonar repositorio | Se inyecta en la URL: `https://token@repos.daviviendahn.dvhn/...` |
| Login al registry | `buildah login -u token -p <GitToken> repos.daviviendahn.dvhn` |
| Paquetes Debian (ODBC) | Se pasa como `ARG REPO_TOKEN` al Dockerfile para autenticar con el repo Debian de Gitea |

**El token NO se configura en la API.** Se envia en cada request por el cliente. Esto permite que diferentes usuarios/servicios usen sus propios tokens.

---

## 2. Configuracion del Servidor

### 2.1 Verificar Buildah

Confirmar que buildah esta instalado y funciona en modo rootless:

```
buildah --version
buildah images
```

Si se ejecuta como usuario no-root (rootless), verificar que el storage funciona:

```
buildah info
```

### 2.2 Verificar conectividad al registry

```
buildah login -u token -p <TU_GITEA_TOKEN> repos.daviviendahn.dvhn
```

Si el login es exitoso, el servidor puede publicar imagenes.

### 2.3 Verificar acceso a repositorios Git

```
git clone https://token:<TU_GITEA_TOKEN>@repos.daviviendahn.dvhn/davivienda-banco/<algun-repo>.git /tmp/test-clone
rm -rf /tmp/test-clone
```

### 2.4 Crear directorio temporal

```
mkdir -p /tmp/dockerize-builds
```

Este directorio es donde la API clona repositorios y genera Dockerfiles temporalmente.

---

## 3. Configuracion de la API

### 3.1 Archivo principal: `appsettings.json`

Este archivo ya viene configurado para el entorno del banco. Los valores importantes:

```json
{
  "Registry": {
    "Url": "repos.daviviendahn.dvhn",
    "Owner": "davivienda-banco"
  },
  "Build": {
    "MaxConcurrentBuilds": 3,
    "TimeoutMinutes": 10,
    "TempDirectory": "/tmp/dockerize-builds"
  }
}
```

#### Que ajustar segun el entorno:

| Parametro | Valor actual | Cuando cambiar |
|-----------|-------------|----------------|
| `Registry:Url` | `repos.daviviendahn.dvhn` | Solo si el registry cambia de URL |
| `Registry:Owner` | `davivienda-banco` | Solo si la organizacion de Gitea cambia |
| `Build:MaxConcurrentBuilds` | `3` | Aumentar si el servidor tiene mas recursos |
| `Build:TimeoutMinutes` | `10` | Aumentar si los builds tardan mas (imagenes grandes) |
| `Build:TempDirectory` | `/tmp/dockerize-builds` | Cambiar si se prefiere otra ubicacion con mas espacio |
| `Build:UseWsl` | `false` (default) | Solo poner `true` si se ejecuta en Windows con WSL |

### 3.2 Imagenes base

Las imagenes base estan configuradas para usar el registry interno:

```json
{
  "Build": {
    "AlpineBaseImages": {
      "Sdk": "repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine",
      "Aspnet": "repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine"
    },
    "DebianBaseImages": {
      "Sdk": "repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0",
      "Aspnet": "repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0"
    }
  }
}
```

Estas imagenes deben existir en el Container Registry de Gitea **antes** de usar la API. Si no existen, los builds fallaran en el paso de `FROM`.

### 3.3 Configuracion de logs (Serilog)

Los logs se escriben en:
- **Consola** (siempre)
- **Archivo** en `logs/dockerize-api-{fecha}.log` (rotacion diaria, 30 dias de retencion)

Para cambiar el nivel de log en produccion, modificar en `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

Niveles disponibles: `Debug`, `Information`, `Warning`, `Error`.

---

## 4. Despliegue

### Opcion A: Ejecutar con `dotnet run` (desarrollo/pruebas)

1. Navegar al directorio del proyecto:
   ```
   cd /ruta/a/Api_Devops/src/DockerizeAPI
   ```

2. Ejecutar la API:
   ```
   ASPNETCORE_URLS="http://0.0.0.0:5050" dotnet run
   ```

3. La API estara disponible en `http://<IP_SERVIDOR>:5050`

**Nota:** Para desarrollo, usar `ASPNETCORE_ENVIRONMENT=Development` para habilitar Swagger y logs de debug.

### Opcion B: Publicar como binario (produccion recomendado)

1. Compilar y publicar:
   ```
   dotnet publish src/DockerizeAPI/DockerizeAPI.csproj -c Release -o /opt/dockerize-api
   ```

2. Navegar al directorio de publicacion:
   ```
   cd /opt/dockerize-api
   ```

3. Ejecutar:
   ```
   ASPNETCORE_URLS="http://0.0.0.0:5050" ./DockerizeAPI
   ```

### Opcion C: Ejecutar como servicio systemd (produccion)

1. Publicar como en la Opcion B

2. Crear el archivo de servicio en `/etc/systemd/system/dockerize-api.service`:

   ```ini
   [Unit]
   Description=DockerizeAPI - Automated Docker Image Builder
   After=network.target

   [Service]
   Type=notify
   User=builduser
   WorkingDirectory=/opt/dockerize-api
   ExecStart=/opt/dockerize-api/DockerizeAPI
   Environment=ASPNETCORE_URLS=http://0.0.0.0:5050
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_EnableDiagnostics=0
   Restart=on-failure
   RestartSec=10
   TimeoutStartSec=30
   TimeoutStopSec=30

   [Install]
   WantedBy=multi-user.target
   ```

3. Habilitar e iniciar:
   ```
   systemctl daemon-reload
   systemctl enable dockerize-api
   systemctl start dockerize-api
   ```

4. Verificar estado:
   ```
   systemctl status dockerize-api
   journalctl -u dockerize-api -f
   ```

### Puerto y URL

El puerto por defecto es configurable via la variable de entorno `ASPNETCORE_URLS`:

- `http://0.0.0.0:5050` — Escucha en todas las interfaces, puerto 5050
- `http://localhost:5050` — Solo localhost
- `http://0.0.0.0:80` — Puerto 80 (requiere permisos)

---

## 5. Verificacion Post-Despliegue

### 5.1 Health checks

**Health check general de la API:**
```
curl http://localhost:5050/api/health
```

Respuesta esperada (200 OK):
```json
{
  "status": "Healthy",
  "service": "DockerizeAPI",
  "timestamp": "2026-02-19T06:00:00+00:00",
  "version": "1.0.0"
}
```

**Liveness probe** (verifica que la app responde):
```
curl http://localhost:5050/health/live
```

Respuesta esperada: `Healthy`

**Readiness probe** (verifica que la app esta lista):
```
curl http://localhost:5050/health/ready
```

Respuesta esperada: `Healthy`

### 5.2 Verificar que el listado de builds funciona

```
curl http://localhost:5050/api/builds
```

Respuesta esperada (200 OK):
```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

### 5.3 Verificar templates

```
curl http://localhost:5050/api/templates/alpine
```

Respuesta esperada (200 OK):
```json
{
  "name": "alpine",
  "content": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build\n...",
  "isOverride": false,
  "lastModifiedAt": null
}
```

```
curl http://localhost:5050/api/templates/odbc
```

Respuesta esperada (200 OK):
```json
{
  "name": "odbc",
  "content": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0 AS build\n...",
  "isOverride": false,
  "lastModifiedAt": null
}
```

Ambos deben devolver el contenido del template con los placeholders `{{csprojPath}}`, `{{csprojDir}}`, `{{assemblyName}}`.

### 5.4 Swagger (solo en Development)

Si la API esta en modo Development, la documentacion interactiva esta disponible en:
```
http://localhost:5050/swagger
```

---

## 6. Referencia Completa de Endpoints

### Resumen de todos los endpoints

| Metodo | Ruta | Descripcion |
|--------|------|-------------|
| GET | `/api/health` | Health check general |
| GET | `/health/live` | Liveness probe |
| GET | `/health/ready` | Readiness probe |
| POST | `/api/builds` | Crear nuevo build |
| GET | `/api/builds` | Listar builds (paginado) |
| GET | `/api/builds/{buildId}` | Detalle de un build |
| DELETE | `/api/builds/{buildId}` | Cancelar build |
| GET | `/api/builds/{buildId}/logs` | Logs en tiempo real (SSE) |
| POST | `/api/builds/{buildId}/retry` | Reintentar build fallido |
| POST | `/api/builds/preview-dockerfile` | Previsualizar Dockerfile |
| GET | `/api/templates/alpine` | Ver template Alpine |
| GET | `/api/templates/odbc` | Ver template ODBC |
| PUT | `/api/templates/{templateName}` | Modificar template |

---

### 6.1 POST /api/builds — Crear un build

**Campos del request:**

| Campo | Tipo | Requerido | Default | Descripcion |
|-------|------|-----------|---------|-------------|
| `repositoryUrl` | string | Si | — | URL del repositorio en Gitea |
| `branch` | string | No | `"main"` | Rama a clonar |
| `gitToken` | string | Si | — | Token de acceso Gitea |
| `imageConfig` | object | No | null | Configuracion de imagen (ver abajo) |
| `registryConfig` | object | No | null | Configuracion del registry (ver abajo) |

**Campos de `imageConfig` (todos opcionales):**

| Campo | Tipo | Default | Descripcion |
|-------|------|---------|-------------|
| `imageName` | string | Nombre del repo | Nombre de la imagen en el registry |
| `tag` | string | Nombre de la rama | Tag de la imagen |
| `includeOdbcDependencies` | bool | `false` | `true` = template Debian con ODBC/IBM iAccess |
| `platform` | string | `"linux/amd64"` | Plataforma target |
| `noCache` | bool | `false` | Reconstruir sin cache de capas |
| `pull` | bool | `false` | Siempre descargar imagen base mas reciente |
| `quiet` | bool | `false` | Salida minima de buildah |
| `network` | string | `"Host"` | Red durante build: `Host`, `Bridge`, `None` |
| `labels` | dict | null | Labels metadata `(--label KEY=VALUE)` |
| `buildArgs` | dict | null | Build arguments `(--build-arg KEY=VALUE)` |

**Campos de `registryConfig` (todos opcionales):**

| Campo | Tipo | Default | Descripcion |
|-------|------|---------|-------------|
| `registryUrl` | string | `repos.daviviendahn.dvhn` | URL del Container Registry |
| `owner` | string | `davivienda-banco` | Organizacion en Gitea |

#### Ejemplo 1: Build basico (Alpine, sin ODBC)

```
curl -X POST http://localhost:5050/api/builds \
  -H "Content-Type: application/json" \
  -d '{
    "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-microservicio.git",
    "branch": "main",
    "gitToken": "abc123_tu_token_de_gitea"
  }'
```

Respuesta (202 Accepted):
```json
{
  "buildId": "c1271876-58da-4a05-bd4d-44abd5142e6c",
  "status": 0,
  "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-microservicio.git",
  "branch": "main",
  "imageName": "mi-microservicio",
  "imageTag": "main",
  "includeOdbcDependencies": false,
  "createdAt": "2026-02-19T06:16:50.885+00:00",
  "completedAt": null,
  "imageUrl": null
}
```

Imagen resultante: `repos.daviviendahn.dvhn/davivienda-banco/mi-microservicio:main`

Lo que hace internamente:
1. Clona el repositorio usando el token en la URL
2. Auto-detecta el archivo `.csproj`
3. Genera un Dockerfile Alpine (multi-stage, ligero)
4. Construye la imagen con Buildah
5. Hace login al registry con el token
6. Publica la imagen al Container Registry
7. Limpia archivos temporales e imagen local

#### Ejemplo 2: Build con ODBC (para servicios que conectan a AS400)

```
curl -X POST http://localhost:5050/api/builds \
  -H "Content-Type: application/json" \
  -d '{
    "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/souma-integration.git",
    "branch": "develop",
    "gitToken": "abc123_tu_token_de_gitea",
    "imageConfig": {
      "includeOdbcDependencies": true
    }
  }'
```

Usa template Debian con drivers ODBC/IBM iAccess. El `gitToken` se pasa automaticamente como `REPO_TOKEN` al Dockerfile para autenticar la descarga de paquetes del repositorio Debian de Gitea.

#### Ejemplo 3: Build con nombre de imagen y tag personalizados

```
curl -X POST http://localhost:5050/api/builds \
  -H "Content-Type: application/json" \
  -d '{
    "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-servicio.git",
    "branch": "release/1.0",
    "gitToken": "abc123_tu_token_de_gitea",
    "imageConfig": {
      "imageName": "mi-servicio-custom",
      "tag": "v1.0.0"
    }
  }'
```

Imagen resultante: `repos.daviviendahn.dvhn/davivienda-banco/mi-servicio-custom:v1.0.0`

#### Ejemplo 4: Build con registry diferente

```
curl -X POST http://localhost:5050/api/builds \
  -H "Content-Type: application/json" \
  -d '{
    "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-servicio.git",
    "branch": "main",
    "gitToken": "abc123_tu_token_de_gitea",
    "registryConfig": {
      "registryUrl": "otro-registry.dvhn",
      "owner": "otra-organizacion"
    }
  }'
```

Imagen resultante: `otro-registry.dvhn/otra-organizacion/mi-servicio:main`

#### Ejemplo 5: Build con todas las opciones avanzadas

```
curl -X POST http://localhost:5050/api/builds \
  -H "Content-Type: application/json" \
  -d '{
    "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-servicio.git",
    "branch": "main",
    "gitToken": "abc123_tu_token_de_gitea",
    "imageConfig": {
      "imageName": "mi-servicio",
      "tag": "v2.0.0",
      "includeOdbcDependencies": false,
      "noCache": true,
      "pull": true,
      "labels": {
        "version": "2.0.0",
        "maintainer": "equipo-arquitectura",
        "environment": "production"
      },
      "buildArgs": {
        "ENV_NAME": "production"
      }
    }
  }'
```

---

### 6.2 GET /api/builds — Listar builds

**Query parameters (todos opcionales):**

| Parametro | Tipo | Default | Descripcion |
|-----------|------|---------|-------------|
| `page` | int | 1 | Numero de pagina |
| `pageSize` | int | 20 | Builds por pagina |
| `status` | int | null | Filtrar por estado (0-6) |
| `branch` | string | null | Filtrar por rama |
| `repositoryUrl` | string | null | Filtrar por URL de repositorio |

#### Ejemplo: Listar todos los builds

```
curl http://localhost:5050/api/builds
```

Respuesta (200 OK):
```json
{
  "items": [
    {
      "buildId": "c1271876-58da-4a05-bd4d-44abd5142e6c",
      "status": 4,
      "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-servicio.git",
      "branch": "main",
      "imageName": "mi-servicio",
      "imageTag": "main",
      "includeOdbcDependencies": false,
      "createdAt": "2026-02-19T06:16:50.885+00:00",
      "completedAt": "2026-02-19T06:17:07.146+00:00",
      "imageUrl": "repos.daviviendahn.dvhn/davivienda-banco/mi-servicio:main"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

#### Ejemplo: Filtrar builds fallidos

```
curl "http://localhost:5050/api/builds?status=5"
```

#### Ejemplo: Filtrar por rama con paginacion

```
curl "http://localhost:5050/api/builds?branch=develop&page=1&pageSize=5"
```

---

### 6.3 GET /api/builds/{buildId} — Detalle de un build

```
curl http://localhost:5050/api/builds/c1271876-58da-4a05-bd4d-44abd5142e6c
```

Respuesta (200 OK):
```json
{
  "buildId": "c1271876-58da-4a05-bd4d-44abd5142e6c",
  "status": 4,
  "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-servicio.git",
  "branch": "main",
  "commitSha": "77f92d2150959ec43a55a87ab4134485f94679b",
  "imageName": "mi-servicio",
  "imageTag": "main",
  "includeOdbcDependencies": false,
  "errorMessage": null,
  "generatedDockerfile": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build\n...",
  "csprojPath": "MiServicio/MiServicio.csproj",
  "assemblyName": "MiServicio",
  "createdAt": "2026-02-19T06:16:50.885+00:00",
  "startedAt": "2026-02-19T06:16:50.888+00:00",
  "completedAt": "2026-02-19T06:17:07.146+00:00",
  "imageSizeBytes": null,
  "retryCount": 0,
  "imageUrl": "repos.daviviendahn.dvhn/davivienda-banco/mi-servicio:main",
  "logs": [
    {
      "message": "Estado actualizado a: Cloning",
      "level": "info",
      "timestamp": "2026-02-19T06:16:50.888+00:00"
    },
    {
      "message": "Repositorio clonado exitosamente",
      "level": "info",
      "timestamp": "2026-02-19T06:16:51.320+00:00"
    },
    {
      "message": "Estado actualizado a: Building",
      "level": "info",
      "timestamp": "2026-02-19T06:16:51.392+00:00"
    },
    {
      "message": "Imagen construida exitosamente",
      "level": "info",
      "timestamp": "2026-02-19T06:17:06.823+00:00"
    }
  ]
}
```

Si no existe, retorna 404:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Recurso no encontrado",
  "status": 404,
  "detail": "Build c1271876-... no encontrado."
}
```

---

### 6.4 GET /api/builds/{buildId}/logs — Logs en tiempo real (SSE)

**Si el build esta en progreso**, abre una conexion Server-Sent Events:

```
curl -N http://localhost:5050/api/builds/c1271876-58da-4a05-bd4d-44abd5142e6c/logs
```

Salida en tiempo real (text/event-stream):
```
data: Estado actualizado a: Cloning

data: Clonando repositorio: https://repos.daviviendahn.dvhn/... rama main

data: Repositorio clonado exitosamente

data: Estado actualizado a: Building

data: Iniciando construccion de imagen...

data: Imagen construida exitosamente

```

**Si el build ya termino**, retorna todos los logs como JSON array:
```json
[
  {
    "message": "Estado actualizado a: Cloning",
    "level": "info",
    "timestamp": "2026-02-19T06:16:50.888+00:00"
  },
  {
    "message": "Imagen construida exitosamente",
    "level": "info",
    "timestamp": "2026-02-19T06:17:06.823+00:00"
  }
]
```

---

### 6.5 DELETE /api/builds/{buildId} — Cancelar build

Solo funciona para builds en estado Queued (0), Cloning (1), Building (2) o Pushing (3).

```
curl -X DELETE http://localhost:5050/api/builds/c1271876-58da-4a05-bd4d-44abd5142e6c
```

Respuesta exitosa: **204 No Content** (sin cuerpo)

Si el build ya termino (Completed/Failed/Cancelled), retorna 409:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflicto",
  "status": 409,
  "detail": "No se puede cancelar un build en estado Completed."
}
```

---

### 6.6 POST /api/builds/{buildId}/retry — Reintentar build fallido

Solo funciona para builds en estado Failed (5).

```
curl -X POST http://localhost:5050/api/builds/c1271876-58da-4a05-bd4d-44abd5142e6c/retry
```

Respuesta (200 OK) — retorna un **nuevo build** con nuevo ID:
```json
{
  "buildId": "nuevo-guid-del-retry",
  "status": 0,
  "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-servicio.git",
  "branch": "main",
  "imageName": "mi-servicio",
  "imageTag": "main",
  "includeOdbcDependencies": false,
  "createdAt": "2026-02-19T06:20:00+00:00",
  "completedAt": null,
  "imageUrl": null
}
```

Si el build no esta en Failed, retorna 409:
```json
{
  "title": "Conflicto",
  "status": 409,
  "detail": "Solo se pueden reintentar builds en estado Failed."
}
```

---

### 6.7 POST /api/builds/preview-dockerfile — Previsualizar Dockerfile

Genera el Dockerfile sin ejecutar ningun build. Util para verificar el resultado antes de construir.

```
curl -X POST http://localhost:5050/api/builds/preview-dockerfile \
  -H "Content-Type: application/json" \
  -d '{
    "csprojPath": "MiServicio/MiServicio.csproj",
    "assemblyName": "MiServicio",
    "includeOdbcDependencies": false
  }'
```

Respuesta (200 OK):
```json
{
  "content": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build\nWORKDIR /src\nCOPY .tmp/nuget/ ./.tmp/nuget/\nCOPY nuget.config ./\nCOPY MiServicio/MiServicio.csproj ./MiServicio/\n...\nENTRYPOINT [\"dotnet\", \"MiServicio.dll\"]\n",
  "templateType": "alpine",
  "placeholders": {
    "csprojPath": "MiServicio/MiServicio.csproj",
    "csprojDir": "MiServicio/",
    "assemblyName": "MiServicio"
  }
}
```

Para preview con ODBC:
```
curl -X POST http://localhost:5050/api/builds/preview-dockerfile \
  -H "Content-Type: application/json" \
  -d '{
    "csprojPath": "SoumaIntegration/SoumaIntegration.csproj",
    "includeOdbcDependencies": true
  }'
```

---

### 6.8 GET /api/templates/{nombre} — Ver template

```
curl http://localhost:5050/api/templates/alpine
```

Respuesta (200 OK):
```json
{
  "name": "alpine",
  "content": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build\nWORKDIR /src\n...",
  "isOverride": false,
  "lastModifiedAt": null
}
```

- `isOverride: false` = usando el template embebido original
- `isOverride: true` = usando un override personalizado

---

### 6.9 PUT /api/templates/{nombre} — Modificar template

Modifica un template en caliente sin reiniciar la API. El override persiste entre reinicios.

```
curl -X PUT http://localhost:5050/api/templates/alpine \
  -H "Content-Type: application/json" \
  -d '{
    "content": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build\nWORKDIR /src\nCOPY .tmp/nuget/ ./.tmp/nuget/\nCOPY nuget.config ./\nCOPY {{csprojPath}} ./{{csprojDir}}\nRUN dotnet restore {{csprojPath}} -r linux-x64 --configfile ./nuget.config\nCOPY . .\nRUN dotnet publish {{csprojPath}} -r linux-x64 -c Release -o /app --no-restore\nFROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine AS final\nWORKDIR /app\nCOPY --from=build /app .\nENTRYPOINT [\"dotnet\", \"{{assemblyName}}.dll\"]\n"
  }'
```

Respuesta (200 OK):
```json
{
  "name": "alpine",
  "content": "FROM repos.daviviendahn.dvhn/...",
  "isOverride": true,
  "lastModifiedAt": "2026-02-19T06:20:00+00:00"
}
```

**Importante:** El contenido debe incluir los placeholders `{{csprojPath}}`, `{{csprojDir}}` y `{{assemblyName}}` para que los builds funcionen correctamente.

---

## 7. Monitoreo de Builds

### 7.1 Estados del build

| Codigo | Estado | Descripcion |
|--------|--------|-------------|
| 0 | `Queued` | En cola, esperando procesamiento |
| 1 | `Cloning` | Clonando repositorio |
| 2 | `Building` | Construyendo imagen con Buildah |
| 3 | `Pushing` | Publicando al Container Registry |
| 4 | `Completed` | Exitoso |
| 5 | `Failed` | Fallo en algun paso (ver `errorMessage`) |
| 6 | `Cancelled` | Cancelado por el usuario |

### 7.2 Flujo tipico de monitoreo

1. Crear build → guardar el `buildId`
2. Consultar estado periodicamente:
   ```
   curl http://localhost:5050/api/builds/<BUILD_ID>
   ```
3. O conectarse a logs en tiempo real:
   ```
   curl -N http://localhost:5050/api/builds/<BUILD_ID>/logs
   ```
4. Cuando `status` sea 4 (Completed): la imagen esta publicada
5. Si `status` es 5 (Failed): revisar `errorMessage` y `logs`
6. Si fallo: reintentar con `POST /api/builds/<BUILD_ID>/retry`

---

## 8. Gestion de Templates

### 8.1 Ver template actual

```
curl http://localhost:5050/api/templates/alpine
curl http://localhost:5050/api/templates/odbc
```

### 8.2 Modificar un template

Los templates se pueden modificar en tiempo de ejecucion sin reiniciar la API:

```
curl -X PUT http://localhost:5050/api/templates/alpine \
  -H "Content-Type: application/json" \
  -d '{
    "content": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build\nWORKDIR /src\n... (contenido completo del template)\n"
  }'
```

Los overrides se guardan en `template-overrides/` dentro del directorio de la API y persisten entre reinicios.

### 8.3 Placeholders disponibles

| Placeholder | Se reemplaza con | Ejemplo |
|-------------|-----------------|---------|
| `{{csprojPath}}` | Ruta relativa al .csproj | `MiServicio/MiServicio.csproj` |
| `{{csprojDir}}` | Directorio del .csproj (con `/` final) | `MiServicio/` |
| `{{assemblyName}}` | Nombre del assembly | `MiServicio` |

---

## 9. Configuraciones Avanzadas

### 9.1 Repositorios con estructura no estandar

Si el `.csproj` no esta en la raiz o en un subdirectorio inmediato, se puede especificar manualmente:

```json
{
  "repositoryUrl": "...",
  "gitToken": "...",
  "imageConfig": {
    "csprojPath": "src/SubDirectorio/MiProyecto/MiProyecto.csproj"
  }
}
```

**Nota:** El campo `csprojPath` se envia dentro de `imageConfig` en el JSON del request. La API lo lee del JSON original para detectar el proyecto.

### 9.2 Builds concurrentes

La API procesa hasta `MaxConcurrentBuilds` builds en paralelo (default: 3). Los builds adicionales se encolan automaticamente.

Para ajustar, modificar en `appsettings.json`:

```json
{
  "Build": {
    "MaxConcurrentBuilds": 5
  }
}
```

### 9.3 Timeout de builds

Si los builds tardan mas de 10 minutos (imagenes grandes, red lenta):

```json
{
  "Build": {
    "TimeoutMinutes": 20
  }
}
```

### 9.4 Directorio temporal personalizado

Si `/tmp` no tiene suficiente espacio:

```json
{
  "Build": {
    "TempDirectory": "/data/dockerize-builds"
  }
}
```

Asegurarse de que el usuario que ejecuta la API tenga permisos de escritura.

---

## 10. Troubleshooting

### Error: "Autenticacion al registry fallida"

- Verificar que el `gitToken` sea valido y tenga permisos de escritura al Container Registry
- Verificar conectividad: `buildah login -u token -p <TOKEN> repos.daviviendahn.dvhn`

### Error: "No se pudo descargar la imagen base"

- Las imagenes base (`dotnet-sdk:10.0-alpine`, `dotnet-aspnet:10.0-alpine`, etc.) deben existir en el registry
- Verificar: `buildah pull repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine`

### Error: "cannot use networks as rootless"

- Si buildah se ejecuta como usuario no-root (rootless), el `network` debe ser `Host` o `None`
- El default ya esta configurado como `Host`, pero si el cliente envia `"network": "Bridge"` fallara
- Solucion: no enviar `network` en el request (usara `Host` por defecto)

### Error: "copier: stat: no such file or directory"

- El repositorio debe tener los archivos que el Dockerfile espera:
  - `.tmp/nuget/` (puede estar vacio, pero debe existir — usar `.gitkeep`)
  - `.tmp/certificates/ca/ca-davivienda.crt`
  - `nuget.config`
  - `.tmp/wget/` (solo para template ODBC)

### Error en dotnet publish: "Unable to find fallback package folder"

- El repositorio tiene archivos `obj/` commiteados con paths de Windows
- Solucion: agregar `.gitignore` y `.dockerignore` al repositorio excluyendo `bin/` y `obj/`

### Build tarda demasiado

- La primera ejecucion descarga imagenes base (~300-500MB). Los builds posteriores usan cache
- Si `noCache: true` esta activado, cada build descarga todo de nuevo
- Verificar espacio en disco: `df -h /tmp`

### Logs no muestran detalle de buildah

- Los logs de la API capturan stdout/stderr de buildah
- Para mas detalle, revisar los logs de Serilog en `logs/dockerize-api-{fecha}.log`
- En desarrollo, usar `ASPNETCORE_ENVIRONMENT=Development` para logs nivel Debug

### Workspace no se limpia (Windows/WSL)

- En Windows, los archivos `.idx` de git pueden quedar bloqueados
- No es critico: el workspace se limpia manualmente o en el siguiente build
- En Linux nativo este problema no ocurre

---

## Requisitos de los Repositorios

Para que un repositorio se construya correctamente con DockerizeAPI, debe tener:

### Obligatorio

- Al menos un archivo `.csproj` (se auto-detecta)
- `nuget.config` en la raiz (con los feeds NuGet necesarios)
- `.tmp/nuget/` (directorio, puede estar vacio con un `.gitkeep`)
- `.tmp/certificates/ca/ca-davivienda.crt` (certificado CA corporativo)

### Obligatorio para ODBC

- `.tmp/wget/` con los `.deb` de wget (para descargar la key del repo Debian)

### Altamente recomendado

- `.gitignore` excluyendo `bin/`, `obj/`, `.vs/`
- `.dockerignore` excluyendo `**/bin/`, `**/obj/`, `.git/`
