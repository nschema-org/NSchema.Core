using NSchema.Project.Domain.Models;

namespace NSchema.Current;

/// <summary>
/// Provides the current database schema from the online (live database) or offline (recorded snapshot) source.
/// </summary>
public interface ICurrentSchemaProvider
{
    /// <summary>
    /// Reads the current schema from the given source, restricted to <paramref name="scope"/>.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="scope">The schemas to include.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<DatabaseSchema>> GetSchema(SchemaSourceMode source, SchemaScope scope, CancellationToken cancellationToken = default);
}
