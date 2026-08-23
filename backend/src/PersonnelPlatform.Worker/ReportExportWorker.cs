using PersonnelPlatform.Application.Reporting;

namespace PersonnelPlatform.Worker;

public sealed class ReportExportWorker(IServiceScopeFactory scopeFactory, ILogger<ReportExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ReportExportProcessor>();
                var completed = await processor.RunAsync(stoppingToken);
                if (completed > 0) logger.LogInformation("Background report exports completed. Count={Count}", completed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Background report export processing failed."); }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
