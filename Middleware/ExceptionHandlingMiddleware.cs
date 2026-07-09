using RampaSegura.Api.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace RampaSegura.Api.Middleware
{
    /// <summary>
    /// Convierte cualquier DataAccessException lanzada desde un repository en una
    /// respuesta HTTP consistente. Si el error viene de una validación de coherencia
    /// (SIGNAL SQLSTATE en el stored procedure) responde 409 en vez de 500, para que
    /// el consumidor (app de Luis o el dashboard) pueda distinguir "regla de negocio"
    /// de "algo se rompió de verdad". También atrapa cualquier excepción no controlada
    /// (bug real, no fallo de datos) para nunca devolver un 500 sin JSON.
    /// Todo lo que pasa por aquí queda registrado en la base de errores compartida
    /// vía ErrorLogRepository (best-effort: si ese registro falla, no afecta la respuesta).
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ErrorLogRepository errorLogRepository)
        {
            var module = $"{context.Request.Method} {context.Request.Path}";

            try
            {
                await _next(context);
            }
            catch (DataAccessException ex)
            {
                _logger.LogError(ex, "Error de acceso a datos");

                var statusCode = ex.IsBusinessRuleViolation
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status500InternalServerError;

                await errorLogRepository.RegisterAsync(
                    module,
                    statusCode,
                    ex.InnerException?.Message ?? ex.Message,
                    context.GetClientIp(),
                    "N/A");

                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado");

                await errorLogRepository.RegisterAsync(
                    module,
                    StatusCodes.Status500InternalServerError,
                    ex.InnerException?.Message ?? ex.Message,
                    context.GetClientIp(),
                    "N/A");

                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "INTERNAL_ERROR" });
            }
        }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseDataAccessExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
