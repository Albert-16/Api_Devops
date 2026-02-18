# ============================================================
# DockerizeAPI — Dockerfile Multi-Stage
# Construye la API y la ejecuta en un contenedor con Buildah instalado.
# La API necesita Buildah y Git para ejecutar builds de imágenes.
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
# Usamos Debian porque Buildah no está disponible en Alpine de forma estable
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Instalar Buildah, Git y dependencias necesarias
# El contenedor necesita ejecutar git clone y buildah bud/push
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        buildah \
        git \
        ca-certificates \
        fuse-overlayfs \
        slirp4netns \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Configurar Buildah para ejecución rootless dentro de contenedor
RUN mkdir -p /etc/containers \
    && echo '[storage]' > /etc/containers/storage.conf \
    && echo 'driver = "overlay"' >> /etc/containers/storage.conf \
    && echo '[storage.options.overlay]' >> /etc/containers/storage.conf \
    && echo 'mount_program = "/usr/bin/fuse-overlayfs"' >> /etc/containers/storage.conf

# Crear directorio temporal para builds
RUN mkdir -p /tmp/dockerize-builds

# Crear usuario no-root para ejecución segura
RUN groupadd -r dockerize && useradd -r -g dockerize -m dockerize

# Configurar subuid/subgid para buildah rootless
RUN echo "dockerize:100000:65536" >> /etc/subuid \
    && echo "dockerize:100000:65536" >> /etc/subgid

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
