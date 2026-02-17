using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DockerizeAPI.Endpoints;

/// <summary>
/// Endpoints para gestionar los templates de Dockerfile.
/// Permite consultar y actualizar los templates Alpine y ODBC.
/// </summary>
public static class TemplateEndpoints
{
    /// <summary>
    /// Registra todos los endpoints de templates en la aplicacion.
    /// </summary>
    /// <param name="app">Instancia de WebApplication.</param>
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/templates")
            .WithTags("Templates")
            .WithOpenApi();

        // GET /api/templates/alpine - Obtener template Alpine actual
        group.MapGet("/alpine", GetAlpineTemplate)
            .WithName("GetAlpineTemplate")
            .WithSummary("Obtener template Alpine")
            .WithDescription(
                "Retorna el contenido actual del Dockerfile template para builds Alpine (sin ODBC). " +
                "Este template se usa cuando includeOdbcDependencies es false.")
            .Produces<TemplateResponse>();

        // GET /api/templates/odbc - Obtener template ODBC/Debian actual
        group.MapGet("/odbc", GetOdbcTemplate)
            .WithName("GetOdbcTemplate")
            .WithSummary("Obtener template ODBC/Debian")
            .WithDescription(
                "Retorna el contenido actual del Dockerfile template para builds Debian con ODBC. " +
                "Este template se usa cuando includeOdbcDependencies es true.")
            .Produces<TemplateResponse>();

        // PUT /api/templates/{templateName} - Actualizar un template
        group.MapPut("/{templateName}", UpdateTemplate)
            .WithName("UpdateTemplate")
            .WithSummary("Actualizar un template de Dockerfile")
            .WithDescription(
                "Modifica el contenido de un template existente. " +
                "templateName puede ser 'alpine' u 'odbc'. " +
                "El contenido puede incluir placeholders: {{csprojPath}}, {{csprojDir}}, {{assemblyName}}")
            .Produces<TemplateResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Retorna el template Alpine actual.
    /// </summary>
    private static IResult GetAlpineTemplate(TemplateService templateService)
    {
        var response = templateService.GetTemplateResponse("alpine");
        return response is not null
            ? Results.Ok(response)
            : Results.NotFound(new ErrorResponse { Message = "Template 'alpine' no encontrado." });
    }

    /// <summary>
    /// Retorna el template ODBC actual.
    /// </summary>
    private static IResult GetOdbcTemplate(TemplateService templateService)
    {
        var response = templateService.GetTemplateResponse("odbc");
        return response is not null
            ? Results.Ok(response)
            : Results.NotFound(new ErrorResponse { Message = "Template 'odbc' no encontrado." });
    }

    /// <summary>
    /// Actualiza el contenido de un template existente.
    /// </summary>
    private static IResult UpdateTemplate(
        string templateName,
        [FromBody] UpdateTemplateRequest request,
        TemplateService templateService)
    {
        var validNames = new[] { "alpine", "odbc" };
        if (!validNames.Contains(templateName.ToLowerInvariant()))
        {
            return Results.BadRequest(new ErrorResponse
            {
                Message = $"Template '{templateName}' no es valido. Use 'alpine' u 'odbc'."
            });
        }

        var updated = templateService.UpdateTemplate(templateName, request.Content);
        if (!updated)
        {
            return Results.NotFound(new ErrorResponse
            {
                Message = $"Template '{templateName}' no encontrado."
            });
        }

        var response = templateService.GetTemplateResponse(templateName);
        return Results.Ok(response);
    }
}
