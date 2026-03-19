namespace DockerizeAPI.Sandbox;

/// <summary>
/// Templates de logs realistas para simulación sandbox.
/// Los logs imitan la salida real de git y docker para una experiencia de demo convincente.
/// </summary>
public static class SandboxLogTemplates
{
    /// <summary>Genera logs simulados de git clone.</summary>
    public static IReadOnlyList<string> GetCloneLogs(string repositoryUrl, string branch)
    {
        return
        [
            $"Cloning into '/tmp/dockerize-builds/sandbox'...",
            "remote: Enumerating objects: 847, done.",
            "remote: Counting objects: 100% (847/847), done.",
            "remote: Compressing objects: 100% (412/412), done.",
            "remote: Total 847 (delta 389), reused 756 (delta 312), pack-reused 0",
            "Receiving objects: 100% (847/847), 2.41 MiB | 12.05 MiB/s, done.",
            "Resolving deltas: 100% (389/389), done.",
            $"Branch '{branch}' set up to track remote branch '{branch}' from 'origin'."
        ];
    }

    /// <summary>Genera logs simulados de detección de .csproj.</summary>
    public static IReadOnlyList<string> GetDetectCsprojLogs(string csprojPath, string assemblyName)
    {
        return
        [
            $"Detectado .csproj: {csprojPath}",
            $"AssemblyName extraído: {assemblyName}"
        ];
    }

    /// <summary>Genera logs simulados de copia de shared files.</summary>
    public static IReadOnlyList<string> GetSharedFilesLogs(bool includeOdbc)
    {
        List<string> logs =
        [
            "Copiando archivos compartidos al workspace...",
            "  → .tmp/certificate/ca/ca-davivienda.crt",
            "  → .tmp/nuget/nuget.config"
        ];

        if (includeOdbc)
        {
            logs.Add("  → .tmp/wget/ (paquetes ODBC)");
        }

        logs.Add("Archivos compartidos copiados exitosamente.");
        return logs;
    }

    /// <summary>Genera logs simulados de docker build.</summary>
    public static IReadOnlyList<string> GetDockerBuildLogs(string imageTag, bool includeOdbc)
    {
        string baseImage = includeOdbc
            ? "mcr.microsoft.com/dotnet/sdk:10.0"
            : "mcr.microsoft.com/dotnet/sdk:10.0-alpine";

        return
        [
            "Dockerfile generado y escrito en el workspace",
            $"#1 [internal] load build definition from Dockerfile",
            "#2 [internal] load metadata for mcr.microsoft.com/dotnet/aspnet:10.0",
            $"#3 [internal] load metadata for {baseImage}",
            "#4 [build 1/7] FROM mcr.microsoft.com/dotnet/sdk:10.0",
            "#5 [build 2/7] WORKDIR /src",
            "#6 [build 3/7] COPY *.csproj ./src/",
            "#7 [build 4/7] RUN dotnet restore",
            "#8 [build 5/7] COPY . .",
            "#9 [build 6/7] RUN dotnet publish -c Release -o /app/publish",
            "#10 [runtime 1/3] FROM mcr.microsoft.com/dotnet/aspnet:10.0",
            "#11 [runtime 2/3] WORKDIR /app",
            "#12 [runtime 3/3] COPY --from=build /app/publish .",
            "#13 exporting to image",
            $"#13 naming to {imageTag} done"
        ];
    }

    /// <summary>Genera logs simulados de docker push.</summary>
    public static IReadOnlyList<string> GetPushLogs(string imageTag)
    {
        return
        [
            $"The push refers to repository [{imageTag.Split(':')[0]}]",
            "5f70bf18a086: Preparing",
            "a3ed95caeb02: Preparing",
            "e1da644611ce: Preparing",
            "5f70bf18a086: Pushed",
            "a3ed95caeb02: Pushed",
            "e1da644611ce: Pushed",
            $"{imageTag.Split(':').Last()}: digest: sha256:sandbox{Guid.NewGuid():N}...abc size: 3248"
        ];
    }

    /// <summary>Genera logs simulados de docker login.</summary>
    public static IReadOnlyList<string> GetLoginLogs(string registryUrl)
    {
        return
        [
            $"Authenticating to {registryUrl}...",
            "Login Succeeded"
        ];
    }

    /// <summary>Genera logs simulados de docker pull.</summary>
    public static IReadOnlyList<string> GetPullLogs(string imageName)
    {
        return
        [
            $"Pulling from {imageName}",
            "a3ed95caeb02: Pulling fs layer",
            "e1da644611ce: Pulling fs layer",
            "5f70bf18a086: Pulling fs layer",
            "a3ed95caeb02: Download complete",
            "e1da644611ce: Download complete",
            "5f70bf18a086: Download complete",
            $"Digest: sha256:sandbox{Guid.NewGuid():N}...def",
            $"Status: Downloaded newer image for {imageName}"
        ];
    }

    /// <summary>Genera logs simulados de docker run.</summary>
    public static IReadOnlyList<string> GetDockerRunLogs(string containerName, string containerId)
    {
        return
        [
            $"Creating container {containerName}...",
            $"Container {containerId} created.",
            $"Starting container {containerName}...",
            $"Container {containerName} started successfully."
        ];
    }

    /// <summary>Genera logs simulados de fallo en un paso específico.</summary>
    public static IReadOnlyList<string> GetFailureLogs(string step)
    {
        return step.ToLowerInvariant() switch
        {
            "cloning" =>
            [
                "fatal: repository 'https://repos.dvhn/org/repo.git' not found",
                "[SANDBOX] Fallo simulado en paso: Cloning"
            ],
            "building" =>
            [
                "ERROR [build 6/7] RUN dotnet publish -c Release -o /app/publish",
                "  error MSB4018: The \"ResolvePackageAssets\" task failed unexpectedly.",
                "[SANDBOX] Fallo simulado en paso: Building"
            ],
            "pushing" =>
            [
                "error parsing HTTP 403 response body: denied: access forbidden",
                "[SANDBOX] Fallo simulado en paso: Pushing"
            ],
            "loggingin" =>
            [
                "Error response from daemon: Get https://repos.dvhn/v2/: unauthorized",
                "[SANDBOX] Fallo simulado en paso: LoggingIn"
            ],
            "pulling" =>
            [
                "Error response from daemon: manifest for repos.dvhn/org/app:latest not found",
                "[SANDBOX] Fallo simulado en paso: Pulling"
            ],
            "deploying" =>
            [
                "Error response from daemon: Conflict. The container name is already in use.",
                "[SANDBOX] Fallo simulado en paso: Deploying"
            ],
            _ =>
            [
                $"Error inesperado en paso: {step}",
                $"[SANDBOX] Fallo simulado en paso: {step}"
            ]
        };
    }
}
