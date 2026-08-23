using PersonnelPlatform.Application.Documents;

namespace PersonnelPlatform.Worker;

public sealed class DocumentStatusWorker(IServiceScopeFactory scopeFactory, ILogger<DocumentStatusWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<DocumentLifecycleProcessor>();
                var result = await processor.RunAsync(stoppingToken);
                logger.LogInformation(
                    "Document lifecycle processed. Scanned={Scanned}, Changed={Changed}, Expiring={Expiring}, Expired={Expired}",
                    result.Scanned, result.Changed, result.Expiring, result.Expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Document lifecycle processing failed.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
