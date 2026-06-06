using NSchema.Schema.Model;

namespace NSchema.Schema;

/// <summary>
/// Provides the aggregated desired database schema from all registered <see cref="ISchemaProvider"/> instances.
/// </summary>
public interface IDesiredSchemaProvider
{
    /// <summary>
    /// Gets the aggregated desired schema, optionally scoped to the specified schema names.
    /// </summary>
    ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default);
}
