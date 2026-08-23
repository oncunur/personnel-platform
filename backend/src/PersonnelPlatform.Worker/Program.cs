using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Application.Documents;
using PersonnelPlatform.Application.Integration;
using PersonnelPlatform.Application.Notification;
using PersonnelPlatform.Application.Reporting;
using PersonnelPlatform.Application.Workflow;
using PersonnelPlatform.Infrastructure;
using PersonnelPlatform.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DocumentLifecycleProcessor>();
builder.Services.AddScoped<AdministrativeReminderProcessor>();
builder.Services.AddScoped<WorkflowSlaProcessor>();
builder.Services.AddScoped<NotificationProcessor>();
builder.Services.AddScoped<ReportExportProcessor>();
builder.Services.AddScoped<IntegrationProcessor>();
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<DocumentStatusWorker>();
builder.Services.AddHostedService<AdministrativeReminderWorker>();
builder.Services.AddHostedService<WorkflowSlaWorker>();
builder.Services.AddHostedService<NotificationCenterWorker>();
builder.Services.AddHostedService<ReportExportWorker>();
builder.Services.AddHostedService<IntegrationProcessingWorker>();

var host = builder.Build();
await host.RunAsync();
