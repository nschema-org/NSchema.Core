using NSchema.Project.Domain.Models;
using NSchema.State.Domain.Models;

namespace NSchema.Diff.Domain;

/// <summary>
/// Defines what state currently exists in the context of a migration.
/// </summary>
/// <param name="Database">The current database structure.</param>
/// <param name="ExecutedScripts">The recorded script executions.</param>
internal sealed record CurrentState(Database Database, IReadOnlyList<ScriptExecution> ExecutedScripts)
{
    /// <summary>
    /// Creates a state carrying only the database structure, with no recorded executions.
    /// </summary>
    public CurrentState(Database database) : this(database, []) { }
}
