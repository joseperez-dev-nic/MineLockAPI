using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Middleware;
using RampaSegura.Api.Repositories;
using RampaSegura.Api.Security;
using RampaSegura.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Obtener la cadena de conexión (a MySQL, db_minelock_lt_demo)
var connectionString = builder.Configuration.GetConnectionString("RampaSegura");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("No se encontró la cadena de conexión 'RampaSegura'.");
}

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var error = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "VALIDATION_ERROR";

            return new BadRequestObjectResult(new { error });
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Autenticación JWT -------------------------------------------------
// El login (/api/auth/login) emite el token; el resto de endpoints lo exigen
// en el header "Authorization: Bearer {token}". La X-Api-Key sigue vigente
// como capa aparte: protege que solo la app hable con la API.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("No se encontró 'Jwt:Key' en la configuración.");

builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "RampaSeguraAPI",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "RampaSeguraApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            // Sin esto, un token expirado sigue siendo aceptado hasta 5 min.
            ClockSkew = TimeSpan.Zero
        };
    });

// Por defecto TODO exige token: es más seguro olvidarse de poner [Authorize]
// que olvidarse de protegerlo. Lo público se marca con [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

const string FrontendCorsPolicy = "FrontendCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("https://demonicarobotica-001-site8.etempurl.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Data access
builder.Services.AddSingleton<IRampaSeguraConnectionFactory, RampaSeguraConnectionFactory>();
builder.Services.AddSingleton<IRampaSeguraLocalConnectionFactory, RampaSeguraLocalConnectionFactory>();
builder.Services.AddSingleton<IRampaSeguraCloudConnectionFactory, RampaSeguraCloudConnectionFactory>();
builder.Services.AddScoped<LevelRepository>();
builder.Services.AddScoped<AttendanceRepository>();
builder.Services.AddScoped<AttendanceSyncRepository>();
builder.Services.AddScoped<PersonSyncRepository>();
builder.Services.AddScoped<PhotoSyncRepository>();
builder.Services.AddScoped<SyncLogSyncRepository>();
builder.Services.AddScoped<AlertThresholdSyncRepository>();
builder.Services.AddScoped<AppUserSyncRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<PersonRepository>();
builder.Services.AddScoped<MineRepository>();
builder.Services.AddScoped<AlertSettingRepository>();
builder.Services.AddHostedService<PersonSyncBackgroundService>();

// Log de errores compartido (SQL Server, db_errors_log -- pa_registrar_error)
builder.Services.AddSingleton<IErrorLogConnectionFactory, ErrorLogConnectionFactory>();
builder.Services.AddScoped<ErrorLogRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Debe ir antes de la validación de API key: así las peticiones preflight (OPTIONS)
// del navegador reciben los headers de CORS sin toparse con el chequeo de X-Api-Key.
app.UseCors(FrontendCorsPolicy);

// Primero traduce DataAccessException a HTTP, luego valida la API key
app.UseDataAccessExceptionHandling();
app.UseApiKeyAuth();

// UseAuthentication debe ir antes de UseAuthorization: primero se resuelve
// quién es el usuario (a partir del JWT), después si tiene permiso.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
