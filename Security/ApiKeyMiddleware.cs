using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MineLock.Api.Security
{
    /// <summary>
    /// Autenticación simple por header "X-Api-Key". No hay login ni identidad de usuario:
    /// cualquier request que traiga la key configurada en appsettings/variables de entorno pasa.
    /// Deja pasar /swagger sin key para poder explorar la API en desarrollo.
    /// La comparación usa FixedTimeEquals para no filtrar información por tiempo de respuesta.
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

            if (string.IsNullOrEmpty(validKey) || !IsValidKey(extractedKey!, validKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("X-Api-Key inválida.");
                return;
            }

            await _next(context);
        }

        /// <summary>
        /// Compara la key recibida contra la configurada en tiempo constante,
        /// para no dar pistas de por dónde falla la comparación (timing attack).
        /// </summary>
        private static bool IsValidKey(string extractedKey, string validKey)
        {
            var extractedBytes = Encoding.UTF8.GetBytes(extractedKey);
            var validBytes = Encoding.UTF8.GetBytes(validKey);

            if (extractedBytes.Length != validBytes.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(extractedBytes, validBytes);
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