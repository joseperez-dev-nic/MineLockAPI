using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace MineLock.Api.Security
{
    /// <summary>
    /// Autenticación simple por header "X-Api-Key". No hay login ni identidad de usuario:
    /// cualquier request que traiga la key configurada en appsettings pasa.
    /// Deja pasar /swagger sin key para poder explorar la API en desarrollo.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private const string HeaderName = "X-Api-Key";
        private readonly RequestDelegate _next;

        public ApiKeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(HeaderName, out var extractedKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Falta el header X-Api-Key.");
                return;
            }

            var validKey = configuration["ApiKey"];
            if (string.IsNullOrEmpty(validKey) || !string.Equals(extractedKey, validKey, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("X-Api-Key inválida.");
                return;
            }

            await _next(context);
        }
    }

    public static class ApiKeyMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiKeyMiddleware>();
        }
    }
}
