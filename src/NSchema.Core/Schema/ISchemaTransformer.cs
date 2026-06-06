using NSchema.Schema.Model;

namespace NSchema.Schema;

/// <summary>
/// Defines a contract for transforming the desired database schema before it is diffed against the
/// current state, allowing global adjustments (e.g. naming conventions, injected audit columns) to be
/// applied to the aggregated desired schema.
/// </summary>
public interface ISchemaTransformer
{
    /// <summary>
    /// Transforms the given desired database schema, returning the schema that should be diffed.
    /// </summary>
    /// <param name="schema">The aggregated desired schema to transform.</param>
    /// <returns>The transformed desired schema.</returns>
    DatabaseSchema Transform(DatabaseSchema schema);
}
