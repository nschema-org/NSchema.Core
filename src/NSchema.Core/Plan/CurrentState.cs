using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.Plan;

/// <summary>
/// Defines what state currently exists in the context of a migration.
/// </summary>
/// <param name="Schema">The current database schema.</param>
/// <param name="ExecutedScripts">The scripts recorded as executed, by name and body hash.</param>
internal sealed record CurrentState(DatabaseSchema Schema, IReadOnlyList<ScriptHash> ExecutedScripts)
{
    /// <summary>
    /// Creates a state carrying only a schema, with no recorded executions.
    /// </summary>
    public CurrentState(DatabaseSchema schema) : this(schema, []) { }
}
