using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PersonnelPlatform.Api;
using PersonnelPlatform.Api.Auth;
using PersonnelPlatform.Api.Middleware;
using PersonnelPlatform.Application.Identity;
using PersonnelPlatform.Infrastructure;
using PersonnelPlatform.Infrastructure.Identity;
using PersonnelPlatform.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddScoped<AuthService>();
builder.Services.AddPlatformAuthentication(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("development", policy => policy
        .WithOrigins("http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCors("development");
    app.MapOpenApi();
    await app.Services.ApplyMigrationsAsync();
    await app.Services.SeedDevelopmentIdentityAsync(app.Configuration);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.MapGet("/api/v1/system/ping", (HttpContext context) => Results.Ok(new
{
    service = "PersonnelPlatform.Api",
    status = "ok",
    utc = DateTimeOffset.UtcNow,
    traceId = context.TraceIdentifier
}));

app.MapIdentityEndpoints();

await app.RunAsync();

public partial class Program;
