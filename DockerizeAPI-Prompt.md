# DockerizeAPI - Prompt de Proyecto (v3 - Final)

Eres un arquitecto de software senior especializado en .NET, Docker/Buildah y CI/CD con Gitea.

## Proyecto: DockerizeAPI - Servicio de Dockerizacion Automatizada para Davivienda Honduras

### Contexto Institucional

API interna (sin autenticacion) para ambientes de desarrollo y UAT en Davivienda Honduras. El sistema de control de versiones es Gitea 1.22.6 auto-hospedado. Las imagenes de contenedor se publican en el Container Registry integrado de Gitea y aparecen en la seccion Paquetes de la organizacion como tipo Container (OCI/Docker).

### Infraestructura Actual (datos reales del entorno)

- Registry URL: repos.daviviendahn.dvhn
- Organizacion Gitea: davivienda-banco
- Base images propias en el registry (NO de MCR directamente):
  - Alpine: repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine y dotnet-aspnet:10.0-alpine
  - Debian: repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0 y dotnet-aspnet:10.0
- Build tool: Buildah 1.41.8 (NO Docker daemon - usan Buildah/Podman)
- Plataforma: linux/amd64
- Runtime target: linux-x64
- Tamano tipico de imagen: ~86 MiB (Alpine sin ODBC)
- ENTRYPOINT estandar: ["dotnet", "Microservices.dll"]
- Gitea version: 1.22.6
- Certificado CA corporativo: ca-davivienda.crt (debe instalarse en ambos stages)
- NuGet config personalizado: nuget.config (incluido en el repo)
- Paquetes NuGet locales: carpeta .tmp/nuget/
- Certificados locales: carpeta .tmp/certificates/ca/
- Repositorio Debian privado en Gitea para paquetes ODBC: https://repos.daviviendahn.dvhn/api/packages/Davivienda-Banco/debian

### Stack Tecnologico de la API

- .NET 10 con Minimal API (REST)
- Buildah para construccion de imagenes (NO Docker daemon)
- Podman como alternativa para gestion de contenedores
- Gitea Actions para CI/CD (compatible con GitHub Actions syntax)
- Serilog para logging estructurado

### Descripcion General

API REST que recibe la URL de un repositorio Gitea, clona el codigo fuente, selecciona y adapta el Dockerfile template correcto segun parametros (Alpine sin ODBC o Debian con ODBC), construye la imagen con Buildah, la publica en el Gitea Container Registry (repos.daviviendahn.dvhn/davivienda-banco/), y queda visible como paquete de contenedor en la seccion Paquetes de Gitea.

---

## GLOSARIO DE CONCEPTOS

Esta seccion explica los conceptos clave del proyecto para que cualquier persona pueda entender el sistema, incluso sin experiencia en Docker o .NET.

### Que es Docker / Contenedores?

Un contenedor es como una "caja" que empaqueta una aplicacion junto con todo lo que necesita para funcionar (codigo, librerias, configuraciones). Esto garantiza que la aplicacion funcione igual en cualquier servidor, sin importar que software tenga instalado ese servidor. Docker es la herramienta mas popular para crear y gestionar contenedores.

### Que es Buildah?

Buildah es una alternativa a Docker para CONSTRUIR imagenes de contenedor. La diferencia principal es que Buildah no necesita un "daemon" (un servicio corriendo en segundo plano) para funcionar. En Davivienda usamos Buildah 1.41.8 en lugar de Docker daemon.

### Que es una Imagen Docker?

Una imagen es la "receta" o "plantilla" del contenedor. Es un archivo que contiene todo lo necesario: el codigo compilado de la aplicacion, el runtime de .NET, las librerias del sistema operativo, certificados, etc. Cuando ejecutas una imagen, se crea un contenedor (una instancia en ejecucion).

### Que es un Dockerfile?

Es un archivo de texto con instrucciones paso a paso para construir una imagen. Es como una receta de cocina: "toma esta base, copia estos archivos, instala estas dependencias, compila el codigo, y ejecuta esto al iniciar". Cada instruccion crea una "capa" (layer) en la imagen.

### Que es Multi-Stage Build?

Es una tecnica donde el Dockerfile tiene dos etapas:
- Stage 1 (build): Usa una imagen grande con todas las herramientas de desarrollo (SDK) para compilar el codigo.
- Stage 2 (final): Usa una imagen pequena solo con lo necesario para ejecutar (runtime). Copia SOLO el resultado compilado del Stage 1.

Resultado: imagenes finales mucho mas pequenas y seguras (no incluyen herramientas de desarrollo).

### Que es Alpine vs Debian?

Son dos distribuciones de Linux que se usan como base para las imagenes:
- Alpine: Muy pequena (~5 MB), rapida, pero tiene limitaciones de paquetes disponibles.
- Debian: Mas grande (~120 MB), pero compatible con mas software, como los drivers ODBC de IBM.

En Davivienda usamos:
- Alpine: Para microservicios que NO necesitan conectarse a AS400/RPG via ODBC.
- Debian: Para microservicios que SI necesitan drivers ODBC (ibm-iaccess solo existe para Debian).

### Que es ODBC?

ODBC (Open Database Connectivity) es un estandar para conectarse a bases de datos. En Davivienda lo necesitamos para:
- Conectar microservicios .NET a SQL Server
- Conectar microservicios .NET al sistema AS400/IBM i (via IBM iAccess)

### Que es un Container Registry?

Es como un "almacen" de imagenes Docker. Similar a como GitHub almacena codigo, un registry almacena imagenes de contenedor. En Davivienda usamos el Container Registry integrado de Gitea (repos.daviviendahn.dvhn). Las imagenes aparecen en la seccion "Paquetes" del repositorio en Gitea.

### Que es Gitea Actions?

Es el sistema de CI/CD (Integracion Continua / Despliegue Continuo) de Gitea. Permite automatizar tareas cuando ocurren eventos en el repositorio, como un push a una rama. Es compatible con la sintaxis de GitHub Actions. En este proyecto lo usamos para que automaticamente se construya y publique una nueva imagen Docker cada vez que se hace push a ciertas ramas.

### Que son los Certificados CA?

CA = Certificate Authority (Autoridad Certificadora). Davivienda tiene su propia CA interna (ca-davivienda.crt). Este certificado se necesita dentro de los contenedores para que las aplicaciones puedan hacer llamadas HTTPS a servicios internos de Davivienda sin errores de SSL/TLS.

### Que es NuGet?

NuGet es el gestor de paquetes de .NET (similar a npm para JavaScript o pip para Python). Los archivos en .tmp/nuget/ son librerias internas de Davivienda que no estan disponibles publicamente y se necesitan para compilar los microservicios.

### Que es un Build Argument (ARG)?

Es un parametro que se pasa al momento de construir la imagen. Por ejemplo, REPO_TOKEN es un build argument que contiene el token de autenticacion para descargar paquetes del repositorio Debian privado de Gitea. Solo existe durante la construccion, no queda en la imagen final.

### Que son los Layers (Capas)?

Cada instruccion en un Dockerfile (FROM, RUN, COPY) crea una "capa". Docker/Buildah almacena estas capas en cache. Si una capa no cambia entre builds, se reutiliza del cache en vez de reconstruirla. Por eso el ORDEN de las instrucciones importa: se ponen primero las cosas que cambian menos (dependencias) y al final las que cambian mas (codigo fuente).

### Que es SSE (Server-Sent Events)?

Es una tecnologia web que permite al servidor enviar datos al cliente en tiempo real. En este proyecto se usa para que el endpoint de logs pueda enviar lineas de log del build conforme van apareciendo, sin que el cliente tenga que estar preguntando repetidamente.

---

## ENDPOINTS

### 1. POST /api/builds - Iniciar nuevo build

Este es el endpoint principal. Recibe toda la configuracion necesaria para clonar un repositorio, construir una imagen Docker y publicarla en el registry.

Body:

```json
{
  "repositoryUrl": "string (requerido) - URL del repo en Gitea",
  "branch": "string (default: main) - Rama a clonar",
  "gitToken": "string (requerido) - Token de acceso Gitea (tambien usado como REPO_TOKEN para paquetes Debian)",
  "imageConfig": {
    "imageName": "string (opcional, default: nombre del repo)",
    "tag": "string (opcional, default: nombre de la rama, ej: sapp-dev)",
    "platform": "string (default: linux/amd64)",
    "includeOdbcDependencies": "bool (default: false) - Si es true usa Debian con drivers ODBC, si es false usa Alpine ligero",
    "multiStage": "bool (default: true) - Usar compilacion en dos etapas (recomendado siempre)",
    "buildArgs": "Dictionary<string,string> (opcional) - Argumentos adicionales para el build",
    "labels": "Dictionary<string,string> (opcional) - Etiquetas metadata para la imagen",
    "network": "enum: Host | Bridge | None (default: Bridge) - Tipo de red durante el build",
    "noCache": "bool (default: false) - Si es true, reconstruye todo desde cero sin cache",
    "pull": "bool (default: false) - Si es true, siempre descarga la imagen base mas reciente",
    "progress": "enum: Auto | Plain | Tty (default: Auto) - Nivel de detalle en la salida del build",
    "secrets": "List<SecretEntry> (opcional) - Secretos disponibles durante el build",
    "quiet": "bool (default: false) - Si es true, muestra salida minima"
  },
  "registryConfig": {
    "registryUrl": "string (default: repos.daviviendahn.dvhn) - URL del registry de contenedores",
    "owner": "string (default: davivienda-banco) - Organizacion en Gitea",
    "repository": "string (opcional, default: nombre del repo) - Nombre del repositorio para el paquete"
  }
}
```

Como funciona includeOdbcDependencies:
- Cuando es FALSE: Usa template Alpine (imagen ligera ~86 MiB). Ideal para microservicios que solo usan APIs REST.
  - Base SDK: repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine
  - Base Runtime: repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine
- Cuando es TRUE: Usa template Debian (imagen mas grande, ~200+ MiB). Necesaria cuando el microservicio se conecta a AS400 o SQL Server via ODBC.
  - Base SDK: repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0
  - Base Runtime: repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0
  - Requiere REPO_TOKEN como build arg para autenticarse al repositorio Debian privado de Gitea
  - Paquetes ODBC instalados: unixodbc, unixodbc-common, unixodbc-dev, libodbc2, libodbcinst2, libodbccr2, odbcinst, libltdl7, libreadline8, readline-common, ibm-iaccess

Imagen resultante: repos.daviviendahn.dvhn/davivienda-banco/{imageName}:{tag}
Ejemplo: repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web:sapp-dev

Respuesta: { buildId, status: "Queued", createdAt }

### 2. GET /api/builds/{buildId} - Consultar estado de un build

Permite ver en que paso esta un build. Los estados posibles son:
- Queued: En cola, esperando ser procesado
- Cloning: Clonando el repositorio desde Gitea
- Building: Construyendo la imagen con Buildah
- Pushing: Subiendo la imagen al Container Registry
- Completed: Finalizado exitosamente
- Failed: Fallo en alguno de los pasos

Retorna: status, logs parciales, timestamps de cada paso, y la URL de la imagen si se completo.

### 3. GET /api/builds - Historial de builds

Lista todos los builds realizados con filtros y paginacion.
Query params: page, pageSize, status, branch, repositoryUrl

### 4. DELETE /api/builds/{buildId} - Cancelar build en progreso

Cancela un build que esta en ejecucion y limpia los recursos temporales.

### 5. GET /api/builds/{buildId}/logs - Logs completos del build

Obtiene los logs completos de un build. Soporta SSE (Server-Sent Events) para ver los logs en tiempo real mientras el build esta en progreso.

### 6. POST /api/builds/{buildId}/retry - Reintentar build fallido

Reintenta un build que fallo previamente, usando la misma configuracion original.

### 7. GET /api/templates/alpine - Obtener template Alpine actual

Retorna el contenido actual del Dockerfile template para builds sin ODBC (Alpine).

### 8. GET /api/templates/odbc - Obtener template ODBC/Debian actual

Retorna el contenido actual del Dockerfile template para builds con ODBC (Debian).

### 9. PUT /api/templates/{templateName} - Actualizar un template

Permite modificar el contenido de un template. templateName puede ser "alpine" u "odbc".

### 10. POST /api/builds/preview-dockerfile - Preview del Dockerfile

Genera el Dockerfile que se usaria para un build determinado SIN ejecutarlo. Util para verificar que el Dockerfile generado es correcto antes de construir. Selecciona automaticamente Alpine u ODBC segun los parametros enviados.

### 11. GET /api/health - Health check

Retorna el estado de salud de la API. Util para monitoreo y verificar que el servicio esta activo.

---

## DOCKERFILE TEMPLATE SYSTEM

Existen DOS templates base que se seleccionan automaticamente segun el parametro includeOdbcDependencies. Ambos templates estan OPTIMIZADOS para aprovechar el cache de Docker/Buildah.

### Principio de Optimizacion: Cache de Layers

Las instrucciones en un Dockerfile se ejecutan en orden y cada una crea una "capa" (layer). Docker/Buildah guarda estas capas en cache. Si una capa no cambio desde el ultimo build, se reutiliza instantaneamente.

Por eso el orden importa:
1. PRIMERO: Copiar solo archivos de dependencias (.csproj, nuget.config)
2. SEGUNDO: Hacer dotnet restore (instalar dependencias)
3. TERCERO: Copiar el resto del codigo fuente
4. CUARTO: Compilar y publicar

Si solo cambio el codigo fuente (lo mas comun), los pasos 1 y 2 se reutilizan del cache y el build es mucho mas rapido.

### Template 1: Alpine (includeOdbcDependencies = false)

Para microservicios que NO necesitan drivers ODBC. Produce imagenes pequenas (~86 MiB).

```dockerfile
# ============================================================
# STAGE 1: BUILD (Compilacion)
# Usa la imagen SDK completa para compilar el proyecto
# ============================================================
FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine AS build

WORKDIR /src

# --- Paso 1: Copiar dependencias y certificados ---
# Se copian PRIMERO los archivos de dependencias para aprovechar cache.
# Si las dependencias no cambian, el restore se reutiliza del cache.
COPY .tmp/nuget/ ./.tmp/nuget/
COPY .tmp/certificates/ca/ /tmp/certificates/ca/
COPY nuget.config ./
COPY {{csprojPath}} ./{{csprojDir}}/

# --- Paso 2: Instalar certificado CA corporativo de Davivienda ---
# Necesario para que dotnet restore pueda descargar paquetes de feeds HTTPS internos
RUN cat /tmp/certificates/ca/ca-davivienda.crt >> /etc/ssl/certs/ca-certificates.crt

# --- Paso 3: Restaurar dependencias NuGet ---
# Usa el nuget.config personalizado que incluye los feeds internos
# -r linux-x64: Restaura para runtime especifico de la plataforma
RUN dotnet restore {{csprojPath}} -r linux-x64 --configfile ./nuget.config

# --- Paso 4: Copiar TODO el codigo fuente ---
# Este paso se invalida cada vez que cambia cualquier archivo de codigo,
# pero los pasos anteriores (1-3) se mantienen en cache
COPY . .

# --- Paso 5: Limpiar archivos temporales ---
RUN rm -f -r /tmp/*

# --- Paso 6: Compilar y publicar la aplicacion ---
# -c Release: Compilacion optimizada para produccion
# -o /app: Directorio de salida
# --no-restore: No restaurar de nuevo (ya se hizo en paso 3)
RUN dotnet publish {{csprojPath}} -r linux-x64 -c Release -o /app --no-restore

# ============================================================
# STAGE 2: RUNTIME (Ejecucion)
# Usa la imagen ASP.NET ligera, solo lo necesario para ejecutar
# ============================================================
FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine AS final

# --- Instalar certificado CA corporativo ---
# Tambien necesario en runtime para llamadas HTTPS a servicios internos
COPY .tmp/certificates/ca/ /tmp/certificates/ca/
RUN cat /tmp/certificates/ca/ca-davivienda.crt >> /etc/ssl/certs/ca-certificates.crt \
    && rm -rf /tmp/*

# --- Copiar aplicacion compilada desde Stage 1 ---
WORKDIR /app
COPY --from=build /app .

# --- Punto de entrada: ejecutar la aplicacion ---
ENTRYPOINT ["dotnet", "{{assemblyName}}.dll"]
```

### Template 2: Debian con ODBC (includeOdbcDependencies = true)

Para microservicios que SI necesitan conectarse a AS400/RPG o SQL Server via ODBC. Usa Debian porque el paquete ibm-iaccess no esta disponible para Alpine.

```dockerfile
# ============================================================
# STAGE 1: BUILD (Compilacion)
# Usa la imagen SDK Debian para compilar el proyecto
# ============================================================
FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0 AS build

WORKDIR /src

# --- Paso 1: Copiar dependencias y certificados ---
COPY .tmp/nuget/ ./.tmp/nuget/
COPY .tmp/certificates/ca/ /tmp/certificates/ca/
COPY nuget.config ./
COPY {{csprojPath}} ./{{csprojDir}}/

# --- Paso 2: Instalar certificado CA corporativo ---
RUN cat /tmp/certificates/ca/ca-davivienda.crt >> /etc/ssl/certs/ca-certificates.crt

# --- Paso 3: Restaurar dependencias NuGet ---
RUN dotnet restore {{csprojPath}} -r linux-x64 --configfile ./nuget.config

# --- Paso 4: Copiar TODO el codigo fuente ---
COPY . .

# --- Paso 5: Limpiar archivos temporales ---
RUN rm -f -r /tmp/*

# --- Paso 6: Compilar y publicar ---
RUN dotnet publish {{csprojPath}} -r linux-x64 -c Release -o /app --no-restore

# ============================================================
# STAGE 2: RUNTIME con ODBC (Ejecucion)
# Usa Debian porque ibm-iaccess solo existe para Debian
# ============================================================
FROM repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0 AS final

# ARG: Token de autenticacion para el repositorio Debian privado de Gitea.
# Solo existe durante el build, NO queda en la imagen final.
ARG REPO_TOKEN

# --- Copiar certificados y herramienta wget ---
COPY .tmp/certificates/ca/ /tmp/certificates/ca/
COPY .tmp/wget/ /tmp/wget/

# --- Instalar certificado CA + wget + configurar repositorio Debian de Gitea ---
# Se consolidan en un solo RUN para reducir layers y tamano de imagen.
# Pasos internos:
#   1. Instalar certificado CA de Davivienda
#   2. Instalar wget desde .deb local (necesario para descargar la key del repo)
#   3. Descargar la key publica del repositorio Debian de Gitea
#   4. Configurar autenticacion al repositorio Debian
#   5. Agregar el repositorio Debian de Gitea como fuente de paquetes
RUN cat /tmp/certificates/ca/ca-davivienda.crt >> /etc/ssl/certs/ca-certificates.crt \
    && dpkg -i /tmp/wget/*.deb || true \
    && mkdir -p /etc/apt/keyrings \
    && wget --header "Authorization: token $REPO_TOKEN" \
       -O /etc/apt/keyrings/gitea-Davivienda-Banco.asc \
       https://repos.daviviendahn.dvhn/api/packages/Davivienda-Banco/debian/repository.key \
    && echo "machine repos.daviviendahn.dvhn login token password ${REPO_TOKEN}" \
       > /etc/apt/auth.conf.d/gitea.conf \
    && chmod 600 /etc/apt/auth.conf.d/gitea.conf \
    && echo "deb [signed-by=/etc/apt/keyrings/gitea-Davivienda-Banco.asc] https://repos.daviviendahn.dvhn/api/packages/Davivienda-Banco/debian bookworm main" \
       > /etc/apt/sources.list.d/gitea-davivienda.list

# --- Instalar drivers ODBC ---
# Paquetes necesarios para conectarse a bases de datos via ODBC:
#   - unixodbc, unixodbc-common, unixodbc-dev: Framework ODBC para Linux
#   - libodbc2, libodbcinst2, libodbccr2, odbcinst: Librerias ODBC core
#   - libltdl7: Libreria de carga dinamica (dependencia de ODBC)
#   - libreadline8, readline-common: Libreria de linea de comandos (dependencia de ibm-iaccess)
#   - ibm-iaccess: Driver ODBC de IBM para conectarse a AS400/IBM i
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        unixodbc \
        unixodbc-common \
        unixodbc-dev \
        libodbc2 \
        libodbcinst2 \
        libodbccr2 \
        odbcinst \
        libltdl7 \
        libreadline8 \
        readline-common \
        ibm-iaccess \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# --- Validacion opcional de drivers ODBC ---
# Descomentar para verificar que los drivers se instalaron correctamente
#RUN odbcinst -q -d

# --- Copiar aplicacion compilada desde Stage 1 ---
WORKDIR /app
COPY --from=build /app .

# --- Punto de entrada ---
ENTRYPOINT ["dotnet", "{{assemblyName}}.dll"]
```

### Placeholders del Template System

El sistema detecta y reemplaza automaticamente los siguientes placeholders:

- {{csprojPath}}: Ruta relativa al archivo .csproj del proyecto principal.
  Ejemplo: "1.1-Presentation/Microservices.csproj"
  Se detecta automaticamente escaneando el repositorio clonado.

- {{csprojDir}}: Directorio donde esta el .csproj (extraido de csprojPath).
  Ejemplo: "1.1-Presentation/"
  Se usa para copiar solo el .csproj al directorio correcto durante la fase de cache.

- {{assemblyName}}: Nombre del assembly de salida (el .dll que se ejecuta).
  Ejemplo: "Microservices"
  Se extrae del tag AssemblyName en el .csproj, o se usa el nombre del archivo .csproj sin extension como fallback.

### Logica de Deteccion del Proyecto

Al clonar el repositorio, la API debe:
1. Buscar todos los archivos .csproj en el repositorio
2. Si hay un solo .csproj, usarlo directamente
3. Si hay multiples .csproj, aplicar estas reglas en orden:
   a. Buscar el que tenga <OutputType>Exe</OutputType> (es el ejecutable principal)
   b. Buscar el que este en una carpeta con nombre que contenga "Presentation" o "API"
   c. Si no hay coincidencia, preguntar al usuario o usar el primero encontrado
4. Extraer el AssemblyName del .csproj (o usar el nombre del archivo sin extension)
5. Construir la ruta relativa para {{csprojPath}} y el directorio {{csprojDir}}

---

## PREREQUISITOS DEL REPOSITORIO

Cada repositorio que se va a dockerizar debe contener estos archivos en su raiz. Sin ellos, el build fallara.

### Archivos obligatorios (siempre)

| Archivo/Carpeta | Proposito | Por que se necesita |
|---|---|---|
| .tmp/nuget/ | Paquetes NuGet locales (.nupkg) | Librerias internas de Davivienda que no estan en nuget.org. El dotnet restore las busca aqui. |
| .tmp/certificates/ca/ca-davivienda.crt | Certificado CA corporativo | Sin este certificado, las llamadas HTTPS a servicios internos fallan con error SSL. |
| nuget.config | Configuracion de NuGet | Le dice a dotnet restore donde buscar paquetes (feeds internos + carpeta local). |
| *.csproj | Archivo de proyecto .NET | Define las dependencias y configuracion del proyecto a compilar. |

### Archivos obligatorios (solo si includeOdbcDependencies = true)

| Archivo/Carpeta | Proposito | Por que se necesita |
|---|---|---|
| .tmp/wget/ | Paquetes .deb de wget | Se necesita wget para descargar la key del repo Debian de Gitea. Se incluye como .deb porque la imagen base no lo trae y no se puede instalar con apt-get antes de configurar el repo (problema circular). |

---

## BUILD PIPELINE CON BUILDAH

La construccion usa Buildah (no Docker daemon). Este es el flujo completo que ejecuta la API internamente:

```bash
# ============================================================
# PASO 1: Clonar el repositorio desde Gitea
# ============================================================
# --branch: Clona solo la rama especificada
# --single-branch: No descarga el historial de otras ramas (mas rapido)
git clone --branch {branch} --single-branch {repoUrl} /tmp/build-{id}

# ============================================================
# PASO 2: Generar el Dockerfile
# ============================================================
# La API selecciona el template correcto (Alpine u ODBC) segun parametros
# y reemplaza los placeholders ({{csprojPath}}, {{assemblyName}}, etc.)
# El Dockerfile generado se escribe en el directorio del build

# ============================================================
# PASO 3: Construir la imagen con Buildah
# ============================================================
# NOTA: Para template ODBC, se pasa REPO_TOKEN como build-arg
#       para que el Dockerfile pueda autenticarse al repo Debian de Gitea
buildah bud \
  --tag repos.daviviendahn.dvhn/davivienda-banco/{imageName}:{tag} \
  --platform linux/amd64 \
  --layers \
  --build-arg REPO_TOKEN={gitToken} \
  {--no-cache si noCache=true} \
  {--pull si pull=true} \
  {--quiet si quiet=true} \
  {--network {network}} \
  {--build-arg KEY=VALUE para cada buildArg adicional} \
  {--label KEY=VALUE para cada label} \
  -f Dockerfile \
  /tmp/build-{id}

# Explicacion de flags:
# --tag: Nombre completo de la imagen (registry/org/nombre:tag)
# --platform: Arquitectura de CPU destino
# --layers: Habilita cache por capas (builds mas rapidos)
# --build-arg REPO_TOKEN: Token para acceder al repo Debian (solo ODBC)
# --no-cache: Reconstruir todo sin cache (util si hay problemas)
# --pull: Siempre descargar la imagen base mas reciente
# --quiet: Salida minima
# --network: Tipo de red durante el build
# -f: Ruta al Dockerfile generado

# ============================================================
# PASO 4: Autenticarse en el Container Registry de Gitea
# ============================================================
buildah login -u {user} -p {token} repos.daviviendahn.dvhn

# ============================================================
# PASO 5: Subir la imagen al Container Registry
# ============================================================
# Despues de este paso, la imagen aparece en:
# Gitea -> Organizacion -> Paquetes -> Container -> {imageName}:{tag}
buildah push repos.daviviendahn.dvhn/davivienda-banco/{imageName}:{tag}

# ============================================================
# PASO 6: Limpieza de recursos temporales
# ============================================================
# Eliminar el repositorio clonado y la imagen local para liberar espacio
rm -rf /tmp/build-{id}
buildah rmi {imageId}
```

---

## GITEA ACTIONS WORKFLOWS

Generar DOS archivos de workflow compatibles con Gitea Actions. Estos archivos van dentro del repositorio de la API y se ejecutan automaticamente cuando se detecta un push a las ramas configuradas.

### a) .gitea/workflows/auto-deploy-sapp.yml

- Trigger: Se ejecuta automaticamente al hacer push a ramas sapp-dev y sapp-uat
- sapp-prd EXCLUIDA: El deploy a produccion se hace manualmente por seguridad

### b) .gitea/workflows/auto-deploy-bel.yml

- Trigger: Se ejecuta automaticamente al hacer push a ramas bel-dev y bel-uat
- bel-prd EXCLUIDA: El deploy a produccion se hace manualmente por seguridad

### Comportamiento de ambos workflows

1. Se activan automaticamente al detectar push a las ramas configuradas (excluyen -prd)
2. Extraen el nombre de la rama (ej: "sapp-dev") para usarlo como tag de la imagen
3. Hacen un POST /api/builds con la configuracion del ambiente
4. Pasan el GITEA_TOKEN que tambien sirve como REPO_TOKEN para paquetes Debian
5. Hacen polling cada 10 segundos a GET /api/builds/{buildId} hasta que el build termine o falle
6. Usan Gitea Secrets para manejar credenciales de forma segura: GITEA_TOKEN, REGISTRY_URL
7. El tag de la imagen es el nombre de la rama para mantener el formato actual:
   repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web:sapp-dev
8. Incluyen un comentario indicando que las ramas -prd requieren deploy manual
9. Tienen un step final de notificacion del resultado (exito o fallo)

---

## ARQUITECTURA INTERNA DE LA API

### Flujo de Procesamiento

La API usa un Background Service (servicio en segundo plano) con un Channel<BuildRequest> para procesar builds de forma asincrona. Esto significa que cuando llega un request de build, la API responde inmediatamente con un buildId y procesa el build en segundo plano.

Pipeline de ejecucion:
1. Recibir request y validar parametros
2. Encolar el build en el Channel
3. Background Service toma el build de la cola
4. Clonar repo desde Gitea (usando git token)
5. Detectar proyecto .NET (buscar .csproj, extraer ruta y nombre del assembly)
6. Seleccionar template correcto (Alpine si includeOdbcDependencies=false, Debian/ODBC si true)
7. Reemplazar placeholders: {{csprojPath}}, {{csprojDir}}, {{assemblyName}}
8. Escribir Dockerfile generado en directorio de build
9. Ejecutar Buildah build (Process.Start con redireccion de stdout/stderr para capturar logs)
   - Si ODBC: pasar --build-arg REPO_TOKEN={gitToken}
10. Buildah login + push al Gitea Container Registry
11. Cleanup: eliminar directorio temporal e imagenes locales

### Manejo de Logs en Tiempo Real

Los logs del proceso Buildah se capturan via stdout/stderr redirigido y se almacenan conforme van apareciendo. El endpoint de logs soporta SSE para que el cliente pueda ver los logs en tiempo real.

### Timeout y Limites

- Timeout configurable por build (default: 10 minutos)
- Maximo de builds simultaneos configurable (default: 3)

---

## REQUISITOS TECNICOS

- Sin autenticacion (API interna solo para ambientes dev/uat)
- La propia API debe estar dockerizada con Buildah + Podman (o docker-compose)
- Buildah debe estar instalado en el contenedor donde corre la API
- Logging con Serilog (Console + File) para trazabilidad
- Validacion de requests con DataAnnotations
- Swagger/OpenAPI documentado para facilitar el uso de la API
- Configuracion por appsettings.json + variables de entorno
- Cleanup robusto de recursos temporales (repos clonados, imagenes temp)
- Rate limiting basico (maximo N builds simultaneos configurable)
- Persistencia de historial de builds (SQLite o en memoria segun prefieras)
- Cada servicio, endpoint, modelo y funcion debe estar documentado con comentarios XML para que cualquier desarrollador entienda su proposito

---

## CONFIGURACION POR DEFECTO (appsettings.json)

```json
{
  "Registry": {
    "Url": "repos.daviviendahn.dvhn",
    "Owner": "davivienda-banco"
  },
  "Build": {
    "DefaultPlatform": "linux/amd64",
    "DefaultRuntime": "linux-x64",
    "AlpineBaseImages": {
      "Sdk": "repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine",
      "Aspnet": "repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine"
    },
    "DebianBaseImages": {
      "Sdk": "repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0",
      "Aspnet": "repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0"
    },
    "MaxConcurrentBuilds": 3,
    "TimeoutMinutes": 10,
    "TempDirectory": "/tmp/dockerize-builds"
  },
  "OdbcPackages": {
    "DebianRepoUrl": "https://repos.daviviendahn.dvhn/api/packages/Davivienda-Banco/debian",
    "DebianKeyUrl": "https://repos.daviviendahn.dvhn/api/packages/Davivienda-Banco/debian/repository.key",
    "Distribution": "bookworm",
    "Component": "main",
    "Packages": [
      "unixodbc",
      "unixodbc-common",
      "unixodbc-dev",
      "libodbc2",
      "libodbcinst2",
      "libodbccr2",
      "odbcinst",
      "libltdl7",
      "libreadline8",
      "readline-common",
      "ibm-iaccess"
    ]
  }
}
```

---

## ESTRUCTURA DEL PROYECTO

Proponer estructura de carpetas para Minimal API siguiendo buenas practicas:
- Endpoints/ - Definicion de todos los endpoints de la API
- Services/ - Logica de negocio (BuildService, TemplateService, DockerfileGenerator, etc.)
- Models/ - Clases de request, response, DTOs y enums
- Templates/ - Archivos template de Dockerfile embebidos como recursos
- BackgroundServices/ - Servicios en segundo plano (BuildProcessorService)
- Extensions/ - Metodos de extension para configuracion y registro de servicios
- Middleware/ - Middleware personalizado (error handling, logging, rate limiting)

---

## ENTREGABLES

Implementa el proyecto completo con codigo funcional y COMPLETAMENTE DOCUMENTADO:

1. Solucion .NET completa con todos los archivos, cada clase y metodo con comentarios XML explicativos
2. Dockerfile de la propia API (usando Alpine + Buildah)
3. docker-compose.yml para levantar la API
4. Los 2 workflow files de Gitea Actions (.gitea/workflows/)
5. README.md completo con:
   - Descripcion del proyecto
   - Prerequisitos
   - Instrucciones de instalacion y despliegue
   - Guia de uso con ejemplos
   - Explicacion de cada endpoint con ejemplos curl
   - Troubleshooting / problemas comunes
   - Glosario de terminos
6. Ambos templates Dockerfile (Alpine y ODBC) incluidos como recursos embebidos
7. appsettings.json con la configuracion del entorno
8. Ejemplo de request curl para cada endpoint
9. Documentacion inline en todo el codigo para facilitar mantenimiento futuro
