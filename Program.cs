using RampaSegura.Api.Data;
using RampaSegura.Api.Middleware;
using RampaSegura.Api.Repositories;
using RampaSegura.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Obtener la cadena de conexión (a MySQL, db_minelock_lt_demo)
var connectionString = builder.Configuration.GetConnectionString("RampaSegura");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("No se encontró la cadena de conexión 'RampaSegura'.");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Data access
builder.Services.AddSingleton<IRampaSeguraConnectionFactory, RampaSeguraConnectionFactory>();
builder.Services.AddScoped<LevelRepository>();
builder.Services.AddScoped<AttendanceRepository>();
builder.Services.AddScoped<SyncRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Primero traduce DataAccessException a HTTP, luego valida la API key
app.UseDataAccessExceptionHandling();
app.UseApiKeyAuth();

app.UseAuthorization();

app.MapControllers();

app.Run();
