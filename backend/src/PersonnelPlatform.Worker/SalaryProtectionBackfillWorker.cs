using PersonnelPlatform.Application.Payroll;

namespace PersonnelPlatform.Worker;

public sealed class SalaryProtectionBackfillWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SalaryProtectionBackfillWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessAsync(stoppingToken);
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        try
        {
            var totalProcessed = 0;
            while (!ct.IsCancellationRequested)
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ISalaryProtectionRepository>();
                var result = await repository.BackfillLegacyAsync(250, ct);
                totalProcessed += result.Processed;
                if (result.Remaining == 0 || result.Processed == 0)
                {
                    if (totalProcessed > 0)
                        logger.LogInformation("Salary protection backfill encrypted {Processed} compensation rows; {Remaining} remain.", totalProcessed, result.Remaining);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Salary protection backfill failed. Plaintext legacy salary rows were not deleted unless their encrypted secret was persisted successfully.");
        }
    }
}
