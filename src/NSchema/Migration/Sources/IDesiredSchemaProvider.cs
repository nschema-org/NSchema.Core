using NSchema.Schema;

namespace NSchema.Migration.Sources;

/// <summary>
/// Provides the aggregated desired database schema from all registered <see cref="ISchemaProvider"/> instances.
/// </summary>
public interface IDesiredSchemaProvider
{
    /// <summary>
    /// Gets the aggregated desired schema, optionally scoped to the specified schema names.
    /// </summary>
    Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default);
}
