using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DockerizeAPI.Tests.Fixtures;

/// <summary>
/// Factory personalizada que elimina las URLs configuradas en appsettings
/// para evitar conflictos de puertos entre tests de integración.
/// WebApplicationFactory asigna URLs dinámicas automáticamente.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Eliminar las URLs configuradas en appsettings (ej: localhost:5050)
        // para que WebApplicationFactory use su mecanismo interno de puerto dinámico.
        builder.UseUrls();
    }
}
