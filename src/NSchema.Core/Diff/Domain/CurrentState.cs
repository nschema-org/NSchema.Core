using NSchema.Current.Domain.Models;
using NSchema.Project.Domain.Models;

namespace NSchema.Diff.Domain;

/// <summary>
/// Defines what state currently exists in the context of a migration.
/// </summary>
/// <param name="Schema">The current database schema.</param>
/// <param name="ExecutedScripts">The recorded script executions.</param>
internal sealed record CurrentState(DatabaseSchema Schema, IReadOnlyList<ScriptExecution> ExecutedScripts)
{
    /// <summary>
    /// Creates a state carrying only a schema, with no recorded executions.
    /// </summary>
    public CurrentState(DatabaseSchema schema) : this(schema, []) { }
}
