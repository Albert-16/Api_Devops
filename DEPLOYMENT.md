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

### En el servidor Linux (Red Hat / CentOS / Ubuntu)

- **.NET 10 SDK** instalado (para compilar la API)
- **.NET 10 ASP.NET Runtime** (si se despliega como binario publicado)
- **Docker Engine** instalado y funcionando
- **Git** instalado
- Acceso de red a:
  - `repos.daviviendahn.dvhn` (Gitea — repositorios y registry)
  - Feeds NuGet internos (si aplica)
- Espacio en disco suficiente en `/tmp/dockerize-builds` para builds concurrentes
- El usuario que ejecute la API debe pertenecer al grupo `docker`

### En un servidor Windows (alternativa)

- Todo lo anterior, mas:
- **WSL2** con una distribucion Linux (Ubuntu recomendado)
- **Docker Engine** instalado dentro de WSL (o Docker Desktop)
- Configurar `Build:UseWsl = true` en `appsettings.Development.json`

### El GitToken (Token de Gitea)

La API requiere un **token de acceso de Gitea** que se envia en el campo `gitToken` de cada solicitud de build. El usuario solo envia el token en el JSON del request — la API se encarga de usarlo donde sea necesario:

```json
{
  "repositoryUrl": "https://repos.daviviendahn.dvhn/davivienda-banco/mi-servicio.git",
  "gitToken": "tu_token_de_gitea",
  "branch": "main"
}
```

Internamente, la API usa ese `gitToken` en tres operaciones de forma automatica:

| Operacion | Que hace la API internamente |
|-----------|------------------------------|
| Clonar repositorio | Inyecta el token en la URL: `https://<gitToken>@repos.daviviendahn.dvhn/...` |
| Login al registry | Ejecuta: `docker login -u token -p <gitToken> repos.daviviendahn.dvhn` |
| Paquetes Debian (ODBC) | Pasa el token como `--build-arg REPO_TOKEN=<gitToken>` al Dockerfile |

**El usuario nunca necesita poner el token en la URL.** Solo lo envia en el campo `gitToken` del JSON y la API hace el resto.

**El token NO se configura en la API.** Se envia en cada request por el cliente. Esto permite que diferentes usuarios/servicios usen sus propios tokens.

---

## 2. Configuracion del Servidor

### 2.1 Verificar Docker

Confirmar que Docker esta instalado y funcionando:

```
docker --version
docker info
```

Si se ejecuta como usuario no-root, verificar que el usuario pertenezca al grupo `docker`:

```
groups $(whoami) | grep docker
```

Si no esta en el grupo:

```
sudo usermod -aG docker $(whoami)
# Cerrar sesion y volver a entrar para que aplique
```

### 2.2 Verificar conectividad al registry

```
docker login -u token -p <TU_GITEA_TOKEN> repos.daviviendahn.dvhn
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

### 2.5 Abrir puerto en firewall (si aplica)

```
sudo firewall-cmd --add-port=5050/tcp --permanent
sudo firewall-cmd --reload
```

---

## 3. Configuracion de la API

### 3.1 Archivo principal: `appsettings.json`

Este archivo ya viene configurado para el entorno del banco. Los valores importantes:

```json
{
  "Server": {
    "Urls": "http://166.178.5.148:5050",
    "EnableSwagger": true
  },
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
| `Server:Urls` | `http://166.178.5.148:5050` | Cambiar la IP y puerto segun el servidor |
| `Server:EnableSwagger` | `true` | Poner `false` si no se quiere exponer Swagger |
| `Registry:Url` | `repos.daviviendahn.dvhn` | Solo si el registry cambia de URL |
| `Registry:Owner` | `davivienda-banco` | Solo si la organizacion de Gitea cambia |
| `Build:MaxConcurrentBuilds` | `3` | Aumentar si el servidor tiene mas recursos |
| `Build:TimeoutMinutes` | `10` | Aumentar si los builds tardan mas (imagenes grandes) |
| `Build:TempDirectory` | `/tmp/dockerize-builds` | Cambiar si se prefiere otra ubicacion con mas espacio |
| `Build:UseWsl` | `false` (default) | Solo poner `true` si se ejecuta en Windows con WSL |

#### Configuracion de URL y puerto

La URL se configura en `Server:Urls` del `appsettings.json`. Ya no es necesario pasar `ASPNETCORE_URLS` como variable de entorno:

```json
{
  "Server": {
    "Urls": "http://166.178.5.148:5050"
  }
}
```

Ejemplos de valores validos:
- `http://166.178.5.148:5050` — Escucha en IP especifica, puerto 5050
- `http://+:5050` — Escucha en todas las interfaces, puerto 5050
- `http://localhost:5050` — Solo conexiones locales

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

La carpeta `logs/` se crea automaticamente en el directorio donde se ejecuta la API.

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

**Importante:** Los logs nunca contienen tokens ni credenciales. El `ProcessRunner` sanitiza automaticamente cualquier token en los argumentos de los comandos.

---

## 4. Despliegue

### Opcion A: Ejecutar con `dotnet run` (desarrollo/pruebas)

1. Navegar al directorio del proyecto:
   ```
   cd /ruta/a/Api_Devops/src/DockerizeAPI
   ```

2. Ejecutar la API:
   ```
   dotnet run
   ```

   La URL y puerto se leen del `appsettings.json` (`Server:Urls`).

3. La API estara disponible en `http://166.178.5.148:5050`

4. Swagger disponible en `http://166.178.5.148:5050/swagger`

### Opcion B: Publicar como binario (produccion recomendado)

1. Compilar y publicar:
   ```
   dotnet publish src/DockerizeAPI/DockerizeAPI.csproj -c Release -o /opt/dockerize-api
   ```

2. Copiar el `appsettings.json` si se hicieron cambios de configuracion:
   ```
   cp src/DockerizeAPI/appsettings.json /opt/dockerize-api/
   ```

3. Navegar al directorio de publicacion y ejecutar:
   ```
   cd /opt/dockerize-api
   ./DockerizeAPI
   ```

   La URL y puerto se leen automaticamente del `appsettings.json`.

### Opcion C: Ejecutar como servicio systemd (produccion)

1. Publicar como en la Opcion B

2. Crear el archivo de servicio en `/etc/systemd/system/dockerize-api.service`:

   ```ini
   [Unit]
   Description=DockerizeAPI - Automated Docker Image Builder
   After=network.target docker.service
   Requires=docker.service

   [Service]
   Type=notify
   User=builduser
   Group=docker
   WorkingDirectory=/opt/dockerize-api
   ExecStart=/opt/dockerize-api/DockerizeAPI
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_EnableDiagnostics=0
   Restart=on-failure
   RestartSec=10
   TimeoutStartSec=30
   TimeoutStopSec=30

   [Install]
   WantedBy=multi-user.target
   ```

   **Nota:** Ya no se necesita `ASPNETCORE_URLS` como variable de entorno. La URL se lee del `appsettings.json`.

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

5. Verificar que los logs se estan generando:
   ```
   ls -la /opt/dockerize-api/logs/
   tail -f /opt/dockerize-api/logs/dockerize-api-*.log
   ```

---

## 5. Verificacion Post-Despliegue

### 5.1 Health checks

**Liveness probe** (verifica que la app responde):
```
curl http://166.178.5.148:5050/health/live
```

Respuesta esperada: `Healthy`

**Readiness probe** (verifica que la app esta lista):
```
curl http://166.178.5.148:5050/health/ready
```

Respuesta esperada: `Healthy`

**Health check general de la API:**
```
curl http://166.178.5.148:5050/api/health
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

### 5.2 Verificar Swagger

Abrir en el navegador desde cualquier maquina con acceso de red:
```
http://166.178.5.148:5050/swagger
```

Swagger permite probar todos los endpoints de forma interactiva. Esta habilitado por defecto en todos los entornos. Para desactivarlo, cambiar en `appsettings.json`:

```json
{
  "Server": {
    "EnableSwagger": false
  }
}
```

### 5.3 Verificar que el listado de builds funciona

```
curl http://166.178.5.148:5050/api/builds
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

### 5.4 Verificar templates

```
curl http://166.178.5.148:5050/api/templates/alpine
curl http://166.178.5.148:5050/api/templates/odbc
```

Ambos deben devolver el contenido del template con los placeholders `{{csprojPath}}`, `{{csprojDir}}`, `{{assemblyName}}`.

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
| `quiet` | bool | `false` | Salida minima de Docker |
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
curl -X POST http://166.178.5.148:5050/api/builds \
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
4. Construye la imagen con Docker
5. Hace login al registry con el token
6. Publica la imagen al Container Registry
7. Limpia archivos temporales e imagen local

#### Ejemplo 2: Build con ODBC (para servicios que conectan a AS400)

```
curl -X POST http://166.178.5.148:5050/api/builds \
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
curl -X POST http://166.178.5.148:5050/api/builds \
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
curl -X POST http://166.178.5.148:5050/api/builds \
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
curl -X POST http://166.178.5.148:5050/api/builds \
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
curl http://166.178.5.148:5050/api/builds
```

#### Ejemplo: Filtrar builds fallidos

```
curl "http://166.178.5.148:5050/api/builds?status=5"
```

#### Ejemplo: Filtrar por rama con paginacion

```
curl "http://166.178.5.148:5050/api/builds?branch=develop&page=1&pageSize=5"
```

---

### 6.3 GET /api/builds/{buildId} — Detalle de un build

```
curl http://166.178.5.148:5050/api/builds/c1271876-58da-4a05-bd4d-44abd5142e6c
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
curl -N http://166.178.5.148:5050/api/builds/c1271876-58da-4a05-bd4d-44abd5142e6c/logs
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
curl -X DELETE http://166.178.5.148:5050/api/builds/c1271876-58da-4a05-bd4d-44abd5142e6c
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
curl -X POST http://166.178.5.148:5050/api/builds/c1271876-58da-4a05-bd4d-44abd5142e6c/retry
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
curl -X POST http://166.178.5.148:5050/api/builds/preview-dockerfile \
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
  "content": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build\nWORKDIR /src\n...\nENTRYPOINT [\"dotnet\", \"MiServicio.dll\"]\n",
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
curl -X POST http://166.178.5.148:5050/api/builds/preview-dockerfile \
  -H "Content-Type: application/json" \
  -d '{
    "csprojPath": "SoumaIntegration/SoumaIntegration.csproj",
    "includeOdbcDependencies": true
  }'
```

---

### 6.8 GET /api/templates/{nombre} — Ver template

```
curl http://166.178.5.148:5050/api/templates/alpine
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
curl -X PUT http://166.178.5.148:5050/api/templates/alpine \
  -H "Content-Type: application/json" \
  -d '{
    "content": "FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build\nWORKDIR /src\nCOPY .tmp/nuget/ ./.tmp/nuget/\nCOPY nuget.config ./\nCOPY {{csprojPath}} ./{{csprojDir}}\nRUN dotnet restore {{csprojPath}} -r linux-x64 --configfile ./nuget.config\nCOPY . .\nRUN dotnet publish {{csprojPath}} -r linux-x64 -c Release -o /app --no-restore\nFROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine AS final\nWORKDIR /app\nCOPY --from=build /app .\nENTRYPOINT [\"dotnet\", \"{{assemblyName}}.dll\"]\n"
  }'
```

**Importante:** El contenido debe incluir los placeholders `{{csprojPath}}`, `{{csprojDir}}` y `{{assemblyName}}` para que los builds funcionen correctamente.

---

## 7. Monitoreo de Builds

### 7.1 Estados del build

| Codigo | Estado | Descripcion |
|--------|--------|-------------|
| 0 | `Queued` | En cola, esperando procesamiento |
| 1 | `Cloning` | Clonando repositorio |
| 2 | `Building` | Construyendo imagen con Docker |
| 3 | `Pushing` | Publicando al Container Registry |
| 4 | `Completed` | Exitoso |
| 5 | `Failed` | Fallo en algun paso (ver `errorMessage`) |
| 6 | `Cancelled` | Cancelado por el usuario |

### 7.2 Flujo tipico de monitoreo

1. Crear build → guardar el `buildId`
2. Consultar estado periodicamente:
   ```
   curl http://166.178.5.148:5050/api/builds/<BUILD_ID>
   ```
3. O conectarse a logs en tiempo real:
   ```
   curl -N http://166.178.5.148:5050/api/builds/<BUILD_ID>/logs
   ```
4. Cuando `status` sea 4 (Completed): la imagen esta publicada
5. Si `status` es 5 (Failed): revisar `errorMessage` y `logs`
6. Si fallo: reintentar con `POST /api/builds/<BUILD_ID>/retry`

### 7.3 Logs de la aplicacion

Los logs de Serilog se encuentran en:
```
logs/dockerize-api-{fecha}.log
```

Para ver logs en tiempo real:
```
tail -f /opt/dockerize-api/logs/dockerize-api-*.log
```

Si se ejecuta como servicio systemd, tambien se puede usar:
```
journalctl -u dockerize-api -f
```

---

## 8. Gestion de Templates

### 8.1 Ver template actual

```
curl http://166.178.5.148:5050/api/templates/alpine
curl http://166.178.5.148:5050/api/templates/odbc
```

### 8.2 Modificar un template

Los templates se pueden modificar en tiempo de ejecucion sin reiniciar la API:

```
curl -X PUT http://166.178.5.148:5050/api/templates/alpine \
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
- Verificar conectividad: `docker login -u token -p <TOKEN> repos.daviviendahn.dvhn`

### Error: "No se pudo descargar la imagen base"

- Las imagenes base (`dotnet-sdk:10.0-alpine`, `dotnet-aspnet:10.0-alpine`, etc.) deben existir en el registry
- Verificar: `docker pull repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine`

### Error: "permission denied while trying to connect to the Docker daemon socket"

- El usuario que ejecuta la API no tiene permisos de Docker
- Solucion: agregar el usuario al grupo `docker`:
  ```
  sudo usermod -aG docker <usuario>
  ```
  Cerrar sesion y volver a entrar para que aplique.

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

### Logs no muestran detalle

- Los logs de la API capturan stdout/stderr de Docker
- Para mas detalle, revisar los logs de Serilog en `logs/dockerize-api-{fecha}.log`
- Cambiar nivel de log a `Debug` en `appsettings.json` para mas detalle:
  ```json
  {
    "Serilog": {
      "MinimumLevel": {
        "Default": "Debug"
      }
    }
  }
  ```

### Workspace no se limpia (Windows/WSL)

- En Windows, los archivos `.idx` de git pueden quedar bloqueados
- No es critico: el workspace se limpia manualmente o en el siguiente build
- En Linux nativo este problema no ocurre

---

## Requisitos de los Repositorios (en Gitea)

Estos requisitos son para los **repositorios de microservicios en Gitea** que se van a construir con DockerizeAPI (NO son requisitos del servidor donde corre la API).

Para que un repositorio se construya correctamente, debe tener:

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
