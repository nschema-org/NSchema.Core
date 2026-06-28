using Microsoft.Extensions.Options;

namespace NSchema.State.File;

/// <summary>
/// An <see cref="ISchemaStateStore"/> that persists the schema snapshot to a file on the local filesystem.
/// </summary>
/// <param name="options">The absolute or relative path of the state file.</param>
/// <remarks>
/// Useful for local development and for backends that surface state as a mounted file.
/// </remarks>
internal sealed class FileSchemaStateStore(IOptions<FileSchemaStateStoreOptions> options) : ISchemaStateStore
{
    /// <inheritdoc />
    public async Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default)
    {
        if (!System.IO.File.Exists(options.Value.Path))
        {
            return null;
        }

        return await System.IO.File.ReadAllBytesAsync(options.Value.Path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(options.Value.Path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a sibling temp file and atomically rename it into place, so a concurrent reader (e.g. a plan reading
        // the recorded state while an apply captures new state) never observes a half-written file. The temp must live
        // in the same directory as the target so the move is a same-volume rename rather than a copy.
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await System.IO.File.WriteAllBytesAsync(temp, state, cancellationToken);
            System.IO.File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    // Best-effort cleanup of a temp file left behind by a failed write; a stray temp file is harmless either way.
    private static void TryDelete(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch
        {
            // Ignore: we are already unwinding from the original failure, which is the one that matters.
        }
    }
}
