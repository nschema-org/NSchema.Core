using NSchema.Project.Domain.Models;

namespace NSchema.Current.Domain.Models;

/// <summary>
/// Represents the recorded state of a deployed database: the captured schema snapshot plus the run-once script executions.
/// </summary>
/// <param name="Schema">The captured database schema.</param>
/// <param name="Scripts">The recorded script executions.</param>
public sealed record SchemaState(DatabaseSchema Schema, IReadOnlyList<ScriptExecution> Scripts)
{
    /// <summary>
    /// Creates a state carrying only a schema, with an empty execution ledger.
    /// </summary>
    public SchemaState(DatabaseSchema schema) : this(schema, []) { }

    /// <summary>
    /// The state before anything has been recorded.
    /// </summary>
    public static SchemaState Empty { get; } = new(new DatabaseSchema());

    /// <summary>
    /// Records the given executions into the ledger, replacing any earlier execution recorded under the same name.
    /// </summary>
    /// <param name="executions">The executions to record.</param>
    public SchemaState RecordExecution(IReadOnlyList<ScriptExecution> executions)
    {
        if (executions.Count == 0)
        {
            return this;
        }

        var merged = Scripts
            .Where(e => !executions.Any(s => string.Equals(s.Name, e.Name, StringComparison.OrdinalIgnoreCase)))
            .Concat(executions)
            .ToList();
        return this with { Scripts = merged };
    }

    /// <summary>
    /// Finds the recorded execution for the given script name, or <see langword="null"/> when none is recorded.
    /// </summary>
    /// <param name="name">The script's declared name.</param>
    public ScriptExecution? FindExecution(string name) =>
        Scripts.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Removes the recorded execution for the given script name, so a later plan runs the script again.
    /// </summary>
    /// <param name="name">The script's declared name.</param>
    public SchemaState RemoveExecution(string name)
    {
        var executions = Scripts
            .Where(e => !string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return executions.Count == Scripts.Count ? this : this with { Scripts = executions };
    }
}
