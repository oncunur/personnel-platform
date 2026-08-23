using PersonnelPlatform.Application.Notification;

namespace PersonnelPlatform.Worker;

public sealed class NotificationCenterWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationCenterWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<NotificationProcessor>();
                var result = await processor.RunAsync(stoppingToken);
                logger.LogInformation("Notification Center processed. SourceEvents={SourceEvents}, RuleMatches={RuleMatches}, Created={Created}, Duplicates={Duplicates}, Escalated={Escalated}", result.SourceEvents, result.RuleMatches, result.Created, result.Duplicates, result.Escalated);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notification Center processing failed."); }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
