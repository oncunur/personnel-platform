using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Application.Documents;
using PersonnelPlatform.Infrastructure;
using PersonnelPlatform.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DocumentLifecycleProcessor>();
builder.Services.AddScoped<AdministrativeReminderProcessor>();
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<DocumentStatusWorker>();
builder.Services.AddHostedService<AdministrativeReminderWorker>();

var host = builder.Build();
await host.RunAsync();
