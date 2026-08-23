using PersonnelPlatform.Application.Administration;

namespace PersonnelPlatform.Worker;

public sealed class AdministrativeReminderWorker(IServiceScopeFactory scopeFactory, ILogger<AdministrativeReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<AdministrativeReminderProcessor>();
                var result = await processor.RunAsync(stoppingToken);
                logger.LogInformation("Administrative reminders processed. Candidates={Candidates}, Created={Created}, Duplicates={Duplicates}", result.Candidates, result.Created, result.Duplicates);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Administrative reminder processing failed."); }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
