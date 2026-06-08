using Microsoft.Extensions.Options;

namespace NSchema.State;

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
        if (!File.Exists(options.Value.Path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(options.Value.Path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.Value.Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(options.Value.Path, state, cancellationToken);
    }
}
