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

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRunTime = DateTime.Today;

                if (now > nextRunTime)
                    nextRunTime = nextRunTime.AddDays(1);

                // Run immediately
                int a = 1; // Change this to 1 for immediate execution, and 0 for production timing
                var delay = a > 0 ? TimeSpan.Zero : nextRunTime - now;

                _logger.LogInformation("Next run scheduled at: {time}", nextRunTime);

                // Wait for the next run time or proceed immediately if `delay` is zero
                await Task.Delay(delay, stoppingToken);

                try
                {
                    _logger.LogInformation("Starting upload process at: {time}", DateTime.Now);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var uploadController = scope.ServiceProvider.GetRequiredService<SyncController>();
                        await uploadController.SyncDatabases(); // Your syncing process
                    }

                    _logger.LogInformation("Sync process completed successfully at: {time}", DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the upload process.");
                }
            }
        }
    }
}