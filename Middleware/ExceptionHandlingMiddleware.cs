using RampaSegura.Api.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace RampaSegura.Api.Middleware
{
    /// <summary>
    /// Convierte cualquier DataAccessException lanzada desde un repository en una
    /// respuesta HTTP consistente. Si el error viene de una validación de coherencia
    /// (SIGNAL SQLSTATE en el stored procedure) responde 409 en vez de 500, para que
    /// el consumidor (app de Luis o el dashboard) pueda distinguir "regla de negocio"
    /// de "algo se rompió de verdad".
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

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (DataAccessException ex)
            {
                _logger.LogError(ex, "Error de acceso a datos");

                context.Response.StatusCode = ex.IsBusinessRuleViolation
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status500InternalServerError;

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
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
