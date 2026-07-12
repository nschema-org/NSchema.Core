using NSchema.Project.Domain.Models;

namespace NSchema.Current.Backends;

/// <summary>
/// Introspects the live database into the schema model, scoped to a set of schema names.
/// </summary>
public interface ISchemaIntrospector
{
    /// <summary>
    /// Reads the live schema for the specified schema names.
    /// </summary>
    /// <param name="schemaNames">
    /// The names of the schemas to include. When null, the introspector returns every schema it can see.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The schema for the specified schema names.</returns>
    /// <remarks>
    /// If a schema name does not exist, it is not included in the returned schema.
    /// </remarks>
    ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default);
}
