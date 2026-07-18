using RampaSegura.Api.Common;
using RampaSegura.Api.Models.Requests;
using RampaSegura.Api.Repositories;
using RampaSegura.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private const string Module = "AuthController.Login";

        private readonly UserRepository _repository;
        private readonly ErrorLogRepository _errorLogRepository;
        private readonly JwtTokenService _tokenService;

        public AuthController(
            UserRepository repository,
            ErrorLogRepository errorLogRepository,
            JwtTokenService tokenService)
        {
            _repository = repository;
            _errorLogRepository = errorLogRepository;
            _tokenService = tokenService;
        }

        /// <summary>
        /// POST /api/auth/login -- único endpoint sin JWT (por eso [AllowAnonymous]);
        /// sigue exigiendo la X-Api-Key. Devuelve el token que el cliente debe mandar
        /// después en el header "Authorization: Bearer {token}".
        /// </summary>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
        {
            var attemptedUser = request.Login ?? "N/A";

            var user = await _repository.GetByUsernameOrEmailAsync(request.Login!);

            if (user is null)
            {
                await _errorLogRepository.RegisterAsync(Module, StatusCodes.Status401Unauthorized, "USER_NOT_FOUND", HttpContext.GetClientIp(), attemptedUser);
                return Unauthorized(new { error = "USER_NOT_FOUND" });
            }

            if (!user.IsActive)
            {
                await _errorLogRepository.RegisterAsync(Module, StatusCodes.Status403Forbidden, "USER_INACTIVE", HttpContext.GetClientIp(), attemptedUser);
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "USER_INACTIVE" });
            }

            bool passwordOk;
            try
            {
                passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // password_hash guardado no es un hash de BCrypt válido (dato corrupto o legado).
                passwordOk = false;
            }

            if (!passwordOk)
            {
                await _errorLogRepository.RegisterAsync(Module, StatusCodes.Status401Unauthorized, "INVALID_PASSWORD", HttpContext.GetClientIp(), attemptedUser);
                return Unauthorized(new { error = "INVALID_PASSWORD" });
            }

            await _repository.TouchLastLoginAsync(user.UserId);

            var (token, expiresAtUtc) = _tokenService.Create(user);

            return Ok(new
            {
                status = "OK",
                token,
                expiresAt = expiresAtUtc,
                user = new
                {
                    userId = user.UserId,
                    username = user.Username,
                    employeeCode = user.EmployeeCode,
                    fullName = user.FullName,
                    email = user.Email,
                    role = user.RoleCode
                }
            });
        }
    }
}
