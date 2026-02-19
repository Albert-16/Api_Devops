# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Summary

**DockerizeAPI** — Minimal API .NET 10 para construcción y publicación automatizada de imágenes Docker usando **Buildah** (no Docker daemon). Clona repos Git, detecta .csproj, genera Dockerfiles desde plantillas embebidas, y publica al Gitea Container Registry de Davivienda Honduras.

- Solution file: `DockerizeAPI.slnx` (no `.sln`)
- API runs on `http://localhost:5050` in dev
- Registry: `repos.daviviendahn.dvhn/davivienda-banco/`

## Commands

```bash
# Build
dotnet build DockerizeAPI.slnx

# Run (development)
cd src/DockerizeAPI && dotnet run

# Test - all
dotnet test

# Test - specific file
dotnet test tests/DockerizeAPI.Tests/DockerizeAPI.Tests.csproj --filter "FullyQualifiedName~BuildStore"

# Test with watch
dotnet watch test

# Publish (Linux)
dotnet publish src/DockerizeAPI/DockerizeAPI.csproj -r linux-x64 -c Release -o ./bin/Release/publish

# Docker Compose
docker-compose up -d
docker-compose logs -f dockerize-api
```

## Architecture

### Pipeline de Build (BackgroundService)

```
HTTP POST /api/builds (202 Accepted)
    → BuildService.CreateBuildAsync
    → BuildStore (ConcurrentDictionary)
    → BuildChannel (System.Threading.Channels, capacity 100)
    → BuildProcessorService (BackgroundService)
        1. GitService.CloneAsync (token injected in URL)
        2. GitService.DetectCsprojAsync (auto-detect or explicit)
        3. GitService.ExtractAssemblyName (parse XML)
        4. DockerfileGenerator.Generate (template substitution)
        5. BuildahService.BuildImageAsync (buildah bud)
        6. BuildahService.LoginAsync (buildah login)
        7. BuildahService.PushImageAsync (buildah push)
        8. BuildahService.CleanupImageAsync (buildah rmi)
    → BuildLogBroadcaster (SSE streaming en tiempo real)
```

### Concurrencia

- `SemaphoreSlim` limita builds simultáneos (default: 3, configurable `Build:MaxConcurrentBuilds`)
- `CancellationTokenSource` por build para timeout individual (default: 10 min)
- `ConcurrentDictionary` en BuildStore y CancellationToken tracking
- Canal con `BoundedChannelFullMode.Wait` para backpressure

### DI — Todos los servicios son Singleton

Crítico: `BuildProcessorService` es un `BackgroundService` (Singleton). Todos los servicios que consume deben ser Singleton también para evitar DI lifetime mismatch.

### Storage

In-memory únicamente (`BuildStore`). Sin base de datos. Dos `ConcurrentDictionary`:
- `<Guid, BuildRecord>` — estado de builds
- `<Guid, ConcurrentBag<BuildLog>>` — logs por build

### SSE Streaming de Logs

`BuildLogBroadcaster` expone logs en tiempo real via `GET /api/builds/{id}/logs`. Si el build terminó, retorna JSON; si está en progreso, retorna `text/event-stream`.

## Key Files

| Archivo | Propósito |
|---|---|
| `src/DockerizeAPI/BackgroundServices/BuildProcessorService.cs` | Ejecutor del pipeline completo |
| `src/DockerizeAPI/Services/ProcessRunner.cs` | Ejecuta git/buildah con sanitización de tokens |
| `src/DockerizeAPI/Services/BuildahService.cs` | Wrapper de comandos buildah (incluye WSL path conversion) |
| `src/DockerizeAPI/Services/GitService.cs` | Clone, detección de .csproj, extracción de metadata |
| `src/DockerizeAPI/Data/BuildStore.cs` | Store in-memory thread-safe |
| `src/DockerizeAPI/Extensions/ServiceCollectionExtensions.cs` | Registro de DI (todo Singleton) |
| `src/DockerizeAPI/Templates/` | Embedded resources: `Dockerfile.alpine.template`, `Dockerfile.odbc.template` |

## Configuration

**`appsettings.Development.json`** tiene `"UseWsl": true` para ejecutar buildah en Windows via WSL2.

**`BuildSettings`** (`Build:` en appsettings):
- `MaxConcurrentBuilds`: 3
- `TimeoutMinutes`: 10
- `TempDirectory`: `/tmp/dockerize-builds` (dev: `./tmp/dockerize-builds`)
- `UseWsl`: false (dev: true)

**`RegistrySettings`** (`Registry:`):
- `Url`: `repos.daviviendahn.dvhn`
- `Owner`: `davivienda-banco`

## Known Patterns & Constraints

- `InjectTokenInUrl` solo modifica URLs con `http://` o `https://` — paths locales se saltean
- `ProcessRunner.SanitizeForLogging` redacta tokens/passwords en logs (Regex sobre args)
- `ToWslPath()`: convierte `D:\path` → `/mnt/d/path` cuando `UseWsl=true`
- Templates como embedded resources — si se agregan nuevas plantillas, declarar en `.csproj` con `<EmbeddedResource>`
- `BuildRecord.OriginalRequestJson` permite retry completo serializando el request original
- No usar `.WithOpenApi()` — deprecado en .NET 10

## Dockerfile Templates

- **`Dockerfile.alpine.template`**: Apps ASP.NET sin ODBC (más ligero)
- **`Dockerfile.odbc.template`**: Apps con conexión AS400/ODBC (base Debian, incluye unixodbc + ibm-iaccess)

Placeholders: `{{csprojPath}}`, `{{csprojDir}}`, `{{assemblyName}}`

## Testing

44 tests xUnit. Tests de integración usan `WebApplicationFactory<Program>` y comparten estado Singleton (relevante para orden de tests).

```bash
# Repo de prueba de campo
# D:/Proyectos/test-microservice
```

## WSL / Windows Notes

- Windows 10 Pro sin Docker/Podman
- WSL2 requerido para ejecutar buildah localmente
- Script post-instalación: `scripts/setup-wsl-buildah.sh`
- En Linux/contenedor: buildah corre directamente sin WSL
