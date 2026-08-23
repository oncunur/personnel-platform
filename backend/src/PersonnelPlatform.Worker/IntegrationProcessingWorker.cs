using PersonnelPlatform.Application.Integration;

namespace PersonnelPlatform.Worker;

public sealed class IntegrationProcessingWorker(IServiceScopeFactory scopeFactory, ILogger<IntegrationProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IntegrationProcessor>();
                var result = await processor.RunAsync(stoppingToken);
                if (result.Claimed > 0)
                    logger.LogInformation("Integration staging processed. Claimed={Claimed} Processed={Processed} BusinessErrors={BusinessErrors} TechnicalErrors={TechnicalErrors} DeadLetters={DeadLetters}", result.Claimed, result.Processed, result.BusinessErrors, result.TechnicalErrors, result.DeadLetters);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Integration staging worker iteration failed."); }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
