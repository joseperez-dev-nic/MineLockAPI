using RampaSegura.Api.Common;
using RampaSegura.Api.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RampaSegura.Api.Services
{
    /// <summary>
    /// Corre sp_person_sync_all_from_ncheck cada IntervalMinutes (appsettings:
    /// "PersonSync:IntervalMinutes", default 5) para no depender de que alguien
    /// llame POST /api/person/sync a mano. Ese endpoint se deja para forzar un
    /// sync inmediato cuando no se quiere esperar al siguiente ciclo.
    /// </summary>
    public class PersonSyncBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PersonSyncBackgroundService> _logger;
        private readonly TimeSpan _interval;

        public PersonSyncBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<PersonSyncBackgroundService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var minutes = configuration.GetValue<int?>("PersonSync:IntervalMinutes") ?? 5;
            _interval = TimeSpan.FromMinutes(minutes <= 0 ? 5 : minutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PersonSyncBackgroundService iniciado, intervalo={Interval}", _interval);

            using var timer = new PeriodicTimer(_interval);

            // Sincroniza una vez al arrancar, luego cada tick del timer.
            // WaitForNextTickAsync lanza OperationCanceledException cuando el host
            // apaga el servicio (stoppingToken); eso es lo esperado, no hace falta atraparlo.
            await SyncOnceAsync();
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SyncOnceAsync();
            }
        }

        private async Task SyncOnceAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<PersonRepository>();

                var rowsAffected = await repository.SyncAllFromNcheckAsync();
                _logger.LogInformation("Sync automático de personal desde NCHECK OK, rowsAffected={RowsAffected}", rowsAffected);
            }
            catch (DataAccessException ex)
            {
                _logger.LogError(ex, "Error al sincronizar el personal desde NCHECK (ciclo automático)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en el ciclo automático de sync de personal");
            }
        }
    }
}
