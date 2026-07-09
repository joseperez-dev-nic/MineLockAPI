using RampaSegura.Api.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RampaSegura.Api.Security
{
    /// <summary>
    /// Autenticación simple por header "X-Api-Key". No hay login ni identidad de usuario:
    /// cualquier request que traiga la key configurada en appsettings/variables de entorno pasa.
    /// Deja pasar /swagger sin key para poder explorar la API en desarrollo.
    /// La comparación usa FixedTimeEquals para no filtrar información por tiempo de respuesta.
    /// Cada rechazo (401) queda registrado en la base de errores compartida.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private const string HeaderName = "X-Api-Key";
        private readonly RequestDelegate _next;

        public ApiKeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IConfiguration configuration, ErrorLogRepository errorLogRepository)
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }

            var module = $"{context.Request.Method} {context.Request.Path}";

            if (!context.Request.Headers.TryGetValue(HeaderName, out var extractedKey))
            {
                const string message = "Falta el header X-Api-Key.";
                await errorLogRepository.RegisterAsync(module, StatusCodes.Status401Unauthorized, message, context.GetClientIp(), "N/A");

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync(message);
                return;
            }

            var validKey = configuration["ApiKey"];

            if (string.IsNullOrEmpty(validKey) || !IsValidKey(extractedKey!, validKey))
            {
                const string message = "X-Api-Key inválida.";
                await errorLogRepository.RegisterAsync(module, StatusCodes.Status401Unauthorized, message, context.GetClientIp(), "N/A");

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync(message);
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
