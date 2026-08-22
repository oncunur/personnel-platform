namespace PersonnelPlatform.Infrastructure.Health;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 6379;
}
