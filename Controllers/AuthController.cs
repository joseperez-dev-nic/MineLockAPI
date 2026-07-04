using RampaSegura.Api.Models.Requests;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserRepository _repository;

        public AuthController(UserRepository repository)
        {
            _repository = repository;
        }

        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
        {
            var user = await _repository.GetByUsernameOrEmailAsync(request.Login!);

            if (user is null)
            {
                return Unauthorized(new { error = "USER_NOT_FOUND" });
            }

            if (!user.IsActive)
            {
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
                return Unauthorized(new { error = "INVALID_PASSWORD" });
            }

            await _repository.TouchLastLoginAsync(user.UserId);

            return Ok(new
            {
                status = "OK",
                user = new
                {
                    userId = user.UserId,
                    username = user.Username,
                    employeeCode = user.EmployeeCode,
                    fullName = user.FullName,
                    email = user.Email
                }
            });
        }
    }
}
