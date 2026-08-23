using PersonnelPlatform.Application.Documents;
using PersonnelPlatform.Infrastructure;
using PersonnelPlatform.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DocumentLifecycleProcessor>();
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<DocumentStatusWorker>();

var host = builder.Build();
await host.RunAsync();
