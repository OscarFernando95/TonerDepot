using Toner.Application.Evidences;

namespace Toner.Application.Tests.TestSupport;

public sealed class InMemoryEvidenceStorage : IEvidenceStorage
{
    public Dictionary<string, (byte[] Bytes, string ContentType)> Blobs { get; } = new();

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        Blobs[key] = (copy.ToArray(), contentType);
        LastModifiedUtc[key] = DateTime.UtcNow;
    }

    public Dictionary<string, DateTime> LastModifiedUtc { get; } = new();

    public async IAsyncEnumerable<string> ListKeysOlderThanAsync(
        DateTime olderThanUtc, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in Blobs.Keys.Where(k => LastModifiedUtc.TryGetValue(k, out var at) && at < olderThanUtc).ToList())
        {
            yield return key;
        }
        await Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(Blobs.TryGetValue(key, out var blob) ? new MemoryStream(blob.Bytes) : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Blobs.Remove(key);
        LastModifiedUtc.Remove(key);
        return Task.CompletedTask;
    }
}
