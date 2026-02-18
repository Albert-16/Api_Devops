using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DockerizeAPI.Endpoints;

/// <summary>
/// Endpoints para gestión de templates Dockerfile.
/// Permite consultar y actualizar los templates Alpine y ODBC.
/// </summary>
public static class TemplateEndpoints
{
    /// <summary>Registra todos los endpoints de templates en /api/templates.</summary>
    public static RouteGroupBuilder MapTemplateEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/templates")
            .WithTags("Templates");

        // GET /api/templates/alpine — Template Alpine actual
        group.MapGet("/alpine", GetAlpineTemplate)
            .WithName("GetAlpineTemplate")
            .WithDescription("Retorna el contenido actual del Dockerfile template para builds sin ODBC (Alpine).")
            .Produces<TemplateResponse>();

        // GET /api/templates/odbc — Template ODBC actual
        group.MapGet("/odbc", GetOdbcTemplate)
            .WithName("GetOdbcTemplate")
            .WithDescription("Retorna el contenido actual del Dockerfile template para builds con ODBC (Debian).")
            .Produces<TemplateResponse>();

        // PUT /api/templates/{templateName} — Actualizar template
        group.MapPut("/{templateName}", UpdateTemplate)
            .WithName("UpdateTemplate")
            .WithDescription("Permite modificar el contenido de un template. templateName: 'alpine' u 'odbc'.")
            .Produces<TemplateResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>GET /api/templates/alpine</summary>
    private static IResult GetAlpineTemplate(ITemplateService templateService)
    {
        TemplateResponse response = templateService.GetTemplate("alpine");
        return TypedResults.Ok(response);
    }

    /// <summary>GET /api/templates/odbc</summary>
    private static IResult GetOdbcTemplate(ITemplateService templateService)
    {
        TemplateResponse response = templateService.GetTemplate("odbc");
        return TypedResults.Ok(response);
    }

    /// <summary>PUT /api/templates/{templateName}</summary>
    private static IResult UpdateTemplate(
        string templateName,
        [FromBody] UpdateTemplateRequest request,
        ITemplateService templateService)
    {
        TemplateResponse response = templateService.UpdateTemplate(templateName, request.Content);
        return TypedResults.Ok(response);
    }
}
