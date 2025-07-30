using SyncRoutineWS.Controllers;

namespace SyncRoutineWS
{
    public class WorkerService(ILogger<WorkerService> logger, IServiceProvider serviceProvider) : BackgroundService
    {
        private readonly ILogger<WorkerService> _logger = logger;
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            int a = 1; // Set to 1 for immediate run, 0 for only midnight runs
            bool isFirstRun = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                TimeSpan delay;

                if (isFirstRun && a == 1)
                {
                    // Run immediately on first run if a == 1
                    delay = TimeSpan.Zero;
                }
                else
                {
                    // Wait until next midnight
                    var nextRunTime = DateTime.Today.AddDays(1);
                    delay = nextRunTime - now;
                }

                _logger.LogInformation("Next run scheduled after delay: {delay}", delay);

                // Wait for the calculated delay
                await Task.Delay(delay, stoppingToken);

                try
                {
                    _logger.LogInformation("Starting sync process at: {time}", DateTime.Now);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var uploadController = scope.ServiceProvider.GetRequiredService<SyncController>();
                        await uploadController.SyncDatabases(); // Your syncing process
                    }

                    _logger.LogInformation("Sync process completed at: {time}", DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the sync process.");
                }

                isFirstRun = false; // All subsequent runs follow midnight schedule
            }
        }
    }
}