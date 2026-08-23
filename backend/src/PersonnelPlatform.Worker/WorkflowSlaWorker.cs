using PersonnelPlatform.Application.Workflow;

namespace PersonnelPlatform.Worker;

public sealed class WorkflowSlaWorker(IServiceScopeFactory scopeFactory, ILogger<WorkflowSlaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<WorkflowSlaProcessor>();
                var result = await processor.RunAsync(stoppingToken);
                logger.LogInformation("Workflow SLA processed. Candidates={Candidates}, Created={Created}, Duplicates={Duplicates}", result.Candidates, result.Created, result.Duplicates);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Workflow SLA processing failed."); }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
