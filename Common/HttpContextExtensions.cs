using Microsoft.AspNetCore.Http;
using System.Linq;

namespace RampaSegura.Api.Common
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// IP real del cliente. Si hay un proxy/IIS delante (X-Forwarded-For),
        /// usa la primera IP de esa lista; si no, la IP de la conexión TCP.
        /// </summary>
        public static string GetClientIp(this HttpContext context)
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',').First().Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
