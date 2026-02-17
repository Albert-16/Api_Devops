# ============================================================
# Dockerfile para DockerizeAPI
# ============================================================
# Esta API necesita Buildah instalado dentro del contenedor
# para poder construir imagenes de contenedor.
# Usa Alpine como base por ser ligera, e instala Buildah manualmente.
# ============================================================

# STAGE 1: BUILD
# Compila la aplicacion .NET
FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build

WORKDIR /src

# Copiar archivos de proyecto y restaurar dependencias primero (cache de layers)
COPY src/DockerizeAPI/DockerizeAPI.csproj ./DockerizeAPI/
RUN dotnet restore DockerizeAPI/DockerizeAPI.csproj -r linux-x64

# Copiar todo el codigo fuente y compilar
COPY src/DockerizeAPI/ ./DockerizeAPI/
RUN dotnet publish DockerizeAPI/DockerizeAPI.csproj \
    -r linux-x64 \
    -c Release \
    -o /app \
    --no-restore

# ============================================================
# STAGE 2: RUNTIME
# Imagen final con ASP.NET runtime + Buildah + Git
# ============================================================
FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine AS final

# Instalar Buildah, Podman y Git (necesarios para el pipeline de build)
RUN apk add --no-cache \
    buildah \
    podman \
    git \
    fuse-overlayfs \
    shadow \
    && mkdir -p /var/lib/containers \
    && chmod 755 /var/lib/containers

# Configurar Buildah para ejecutarse sin privilegios (rootless)
# Necesario para que funcione dentro de un contenedor
RUN echo "[storage]" > /etc/containers/storage.conf \
    && echo 'driver = "overlay"' >> /etc/containers/storage.conf \
    && echo '[storage.options.overlay]' >> /etc/containers/storage.conf \
    && echo 'mount_program = "/usr/bin/fuse-overlayfs"' >> /etc/containers/storage.conf

# Crear directorio para logs
RUN mkdir -p /app/logs

# Copiar aplicacion compilada
WORKDIR /app
COPY --from=build /app .

# Crear directorio temporal para builds
RUN mkdir -p /tmp/dockerize-builds

# Exponer puerto de la API
EXPOSE 8080

# Variables de entorno
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Punto de entrada
ENTRYPOINT ["dotnet", "DockerizeAPI.dll"]
