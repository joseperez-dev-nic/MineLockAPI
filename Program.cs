using MineLock.Api.Data;
using MineLock.Api.Middleware;
using MineLock.Api.Repositories;
using MineLock.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Obtener la cadena de conexión (a MySQL, db_minelock_lt_demo)
var connectionString = builder.Configuration.GetConnectionString("MineLock");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("No se encontró la cadena de conexión 'MineLock'.");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Data access
builder.Services.AddSingleton<IMineLockConnectionFactory, MineLockConnectionFactory>();
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
