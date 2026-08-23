using PersonnelPlatform.Application.Documents;

namespace PersonnelPlatform.Infrastructure.Documents;

public sealed record LocalFileStorageOptions(string RootPath);

public sealed class LocalFileStorage(LocalFileStorageOptions options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.RootPath);
    public string ProviderCode => "LOCAL";

    public async Task WriteAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("Storage key is required.", nameof(storageKey));
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_root, normalized));
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal)) throw new InvalidOperationException("Storage key escapes the configured storage root.");
        return fullPath;
    }
}
