using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Middleware;
using RampaSegura.Api.Repositories;
using RampaSegura.Api.Security;
using RampaSegura.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

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

app.UseAuthorization();

app.MapControllers();

app.Run();
