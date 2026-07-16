using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NSchema.State.Locks.Backends;

/// <summary>
/// An <see cref="IStateLock"/> that holds the lock as a file on the local filesystem.
/// </summary>
internal sealed class FileStateLock(IOptions<FileStateLockOptions> options) : IStateLock
{
    public async Task<IStateLockHandle> Acquire(StateLockRequest request, CancellationToken cancellationToken = default)
    {
        var path = options.Value.Path;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var now = DateTimeOffset.UtcNow;
        var info = new StateLockInfo(
            Id: Guid.NewGuid().ToString("N"),
            Operation: request.Operation,
            Who: $"{Environment.UserName}@{Environment.MachineName}",
            CreatedUtc: now,
            ExpiresUtc: request.TimeToLive is { } ttl ? now + ttl : null
        );

        try
        {
            // FileMode.CreateNew is the atomic "create only if it doesn't already exist" primitive — this is what
            // makes acquisition mutually exclusive across processes on the local machine.
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, info, cancellationToken: cancellationToken);
        }
        catch (IOException) when (File.Exists(path))
        {
            var existing = await TryReadInfo(path, cancellationToken);
            var heldBy = existing is null
                ? "another operation"
                : $"{existing.Who} (operation '{existing.Operation}', since {existing.CreatedUtc:u})";
            throw new StateLockedException(
                $"The state is locked by {heldBy}. Wait for it to complete, or remove the lock file at '{path}' if it is stale.",
                existing);
        }

        return new Handle(path, info);
    }

    public async Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default)
    {
        var path = options.Value.Path;
        return File.Exists(path) ? await TryReadInfo(path, cancellationToken) : null;
    }

    public ValueTask Release(CancellationToken cancellationToken = default)
    {
        var path = options.Value.Path;

        // Forcing release: delete whatever lock file is present, regardless of who wrote it.
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or DirectoryNotFoundException)
        {
            // Best-effort; a missing file means nothing was held, and a leftover can be removed by hand.
        }

        return ValueTask.CompletedTask;
    }

    private static async Task<StateLockInfo?> TryReadInfo(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync<StateLockInfo>(stream, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // The lock file is missing, unreadable, or malformed — surface the conflict without the details.
            return null;
        }
    }

    private sealed class Handle(string path, StateLockInfo info) : IStateLockHandle
    {
        private bool _released;

        public StateLockInfo Info => info;

        public async ValueTask Release(CancellationToken cancellationToken = default)
        {
            if (_released)
            {
                return;
            }
            _released = true;

            // Only delete the file if it still records our lock, so we never remove a lock acquired by someone
            // else after ours was force-removed.
            var current = await TryReadInfo(path, cancellationToken);
            if (current is null || current.Id == info.Id)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // Best-effort release; a leftover file can be removed by hand.
                }
            }
        }
    }
}
