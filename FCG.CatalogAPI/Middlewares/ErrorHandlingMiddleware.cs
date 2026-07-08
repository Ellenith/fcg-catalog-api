using FCG.CatalogAPI.Domain.Entidades;
using System.Net;
using System.Text.Json;

namespace FCG.CatalogAPI.Middlewares;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro: {Mensagem}", ex.Message);
            var (statusCode, mensagem) = ex switch
            {
                DomainException d       => (HttpStatusCode.BadRequest, d.Message),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Acesso não autorizado."),
                _                       => (HttpStatusCode.InternalServerError, "Erro interno.")
            };
            context.Response.ContentType = "application/json";
            context.Response.StatusCode  = (int)statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new { statusCode = (int)statusCode, mensagem, timestamp = DateTime.UtcNow },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}