# ============================================================
# DockerizeAPI — Dockerfile Multi-Stage
# Construye la API y la ejecuta en un contenedor con Docker CLI instalado.
# La API necesita Docker CLI y Git para ejecutar builds de imágenes.
# ============================================================

# --- STAGE 1: BUILD ---
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Copiar archivos de proyecto primero para cache de restore
COPY src/DockerizeAPI/DockerizeAPI.csproj ./DockerizeAPI/
RUN dotnet restore DockerizeAPI/DockerizeAPI.csproj -r linux-x64

# Copiar todo el código fuente
COPY src/DockerizeAPI/ ./DockerizeAPI/

# Compilar y publicar
RUN dotnet publish DockerizeAPI/DockerizeAPI.csproj \
    -r linux-x64 \
    -c Release \
    -o /app \
    --no-restore

# --- STAGE 2: RUNTIME ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Instalar Docker CLI y Git
# El contenedor necesita ejecutar git clone y docker build/push
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        docker.io \
        git \
        ca-certificates \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Crear directorio temporal para builds
RUN mkdir -p /tmp/dockerize-builds

# Crear usuario no-root para ejecución segura
RUN groupadd -r dockerize && useradd -r -g dockerize -m dockerize

# Agregar usuario al grupo docker para acceso al socket
RUN usermod -aG docker dockerize

# Copiar aplicación compilada
WORKDIR /app
COPY --from=build /app .

# Asignar permisos
RUN chown -R dockerize:dockerize /app /tmp/dockerize-builds

# Variables de entorno
ENV DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

# Cambiar a usuario no-root
USER dockerize

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl --fail http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "DockerizeAPI.dll"]
