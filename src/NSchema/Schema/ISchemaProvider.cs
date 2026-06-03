using NSchema.Schema.Model;

namespace NSchema.Schema;

/// <summary>
/// Provides a database schema, scoped to a set of schema names.
/// </summary>
public interface ISchemaProvider
{
    /// <summary>
    /// Gets the schema for the specified schema names.
    /// </summary>
    /// <param name="schemaNames">
    /// The names of the schemas to include. When null, the provider should return its full schema.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The schema for the specified schema names.</returns>
    /// <remarks>
    /// Declarative providers should return everything they describe, while live-database providers should return every schema they can see.
    /// If a schema name does not exist, it should not be included in the returned schema.
    /// </remarks>
    Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default);
}
