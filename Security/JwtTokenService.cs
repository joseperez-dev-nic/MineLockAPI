using RampaSegura.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RampaSegura.Api.Security
{
    /// <summary>
    /// Emite el JWT que devuelve el login. El token lleva el rol del usuario
    /// (claim ClaimTypes.Role), que es lo que evalúan los [Authorize(Roles = ...)].
    /// La llave y los tiempos salen de appsettings, sección "Jwt".
    /// </summary>
    public class JwtTokenService
    {
        private readonly string _key;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expiresMinutes;

        public JwtTokenService(IConfiguration configuration)
        {
            _key = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("No se encontró 'Jwt:Key' en la configuración.");

            // HS256 exige una llave de al menos 256 bits (32 bytes / 32 caracteres ASCII).
            if (Encoding.UTF8.GetByteCount(_key) < 32)
            {
                throw new InvalidOperationException("'Jwt:Key' debe tener al menos 32 caracteres.");
            }

            _issuer = configuration["Jwt:Issuer"] ?? "RampaSeguraAPI";
            _audience = configuration["Jwt:Audience"] ?? "RampaSeguraApp";
            _expiresMinutes = configuration.GetValue<int?>("Jwt:ExpiresMinutes") ?? 480;
        }

        /// <summary>
        /// Genera el token y devuelve también cuándo expira, para que el cliente
        /// sepa hasta cuándo le sirve sin tener que decodificarlo.
        /// </summary>
        public (string Token, DateTime ExpiresAtUtc) Create(UserAccount user)
        {
            var expiresAtUtc = DateTime.UtcNow.AddMinutes(_expiresMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.RoleCode)
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
        }
    }
}
