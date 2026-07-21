using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Middleware;
using RampaSegura.Api.Repositories;
using RampaSegura.Api.Security;
using RampaSegura.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Modo de despliegue: "Local" (servidor de la mina) o "Cloud" (nube).
// El código y el binario son idénticos en ambos lados; SOLO cambia el
// appsettings de cada despliegue (o la variable de entorno Deployment__Mode).
// -------------------------------------------------------------------------
var deployment = new DeploymentInfo(builder.Configuration["Deployment:Mode"]);
builder.Services.AddSingleton(deployment);

// La base OPERATIVA de esta instancia: la LOCAL si corre en la mina, la NUBE
// si corre en la nube. Todos los endpoints de negocio la usan.
var operativeName = deployment.IsLocal ? "RampaSeguraLocal" : "RampaSegura";
var operativeConnectionString = builder.Configuration.GetConnectionString(operativeName)
    ?? throw new InvalidOperationException(
        $"No se encontró la cadena de conexión '{operativeName}' (Deployment:Mode={deployment.Mode}).");

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

// --- Acceso a datos: fábrica OPERATIVA (local o nube según el modo) ---
builder.Services.AddSingleton<IRampaSeguraConnectionFactory>(
    _ => new RampaSeguraConnectionFactory(operativeConnectionString));

// Repositorios de NEGOCIO: existen en los dos despliegues.
builder.Services.AddScoped<LevelRepository>();
builder.Services.AddScoped<AttendanceRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<PersonRepository>();
builder.Services.AddScoped<MineRepository>();
builder.Services.AddScoped<AlertSettingRepository>();

// --- Módulo de SINCRONIZACIÓN: SOLO en el despliegue Local ---
// Necesita ver las dos bases (local origen + nube destino) y el ncheck_db local,
// cosas que la nube no tiene. Si no se registra aquí, los endpoints [LocalOnly]
// responden 404 en la nube.
if (deployment.IsLocal)
{
    builder.Services.AddSingleton<IRampaSeguraLocalConnectionFactory, RampaSeguraLocalConnectionFactory>();
    builder.Services.AddSingleton<IRampaSeguraCloudConnectionFactory, RampaSeguraCloudConnectionFactory>();
    builder.Services.AddScoped<AttendanceSyncRepository>();
    builder.Services.AddScoped<PersonSyncRepository>();
    builder.Services.AddScoped<PhotoSyncRepository>();
    builder.Services.AddScoped<SyncLogSyncRepository>();
    builder.Services.AddScoped<AlertThresholdSyncRepository>();
    builder.Services.AddScoped<AppUserSyncRepository>();
    builder.Services.AddScoped<SyncStatusRepository>();
    builder.Services.AddHostedService<PersonSyncBackgroundService>();
}

// Log de errores compartido (SQL Server, db_errors_log -- pa_registrar_error)
builder.Services.AddSingleton<IErrorLogConnectionFactory, ErrorLogConnectionFactory>();
builder.Services.AddScoped<ErrorLogRepository>();

var app = builder.Build();

app.Logger.LogInformation(
    "RampaSeguraAPI iniciando en modo {Mode}. Base operativa: {Operativa}. Módulo de sync: {Sync}.",
    deployment.Mode, operativeName, deployment.IsLocal ? "ACTIVO" : "desactivado (404)");

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
