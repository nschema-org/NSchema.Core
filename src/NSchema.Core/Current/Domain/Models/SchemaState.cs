using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

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
    /// Records the given executions into the ledger, replacing any earlier execution recorded for the same script.
    /// </summary>
    /// <param name="executions">The executions to record.</param>
    public SchemaState RecordExecution(IReadOnlyList<ScriptExecution> executions)
    {
        if (executions.Count == 0)
        {
            return this;
        }

        var merged = Scripts
            .Where(e => executions.All(s => s.Script != e.Script))
            .Concat(executions)
            .ToList();
        return this with { Scripts = merged };
    }

    /// <summary>
    /// Finds the recorded execution for the given script, or <see langword="null"/> when none is recorded.
    /// </summary>
    /// <param name="script">The script's address.</param>
    public ScriptExecution? FindExecution(ScriptReference script) => Scripts.FirstOrDefault(e => e.Script == script);

    /// <summary>
    /// Removes the recorded execution for the given script, so a later plan runs the script again.
    /// </summary>
    /// <param name="script">The script's address.</param>
    public SchemaState RemoveExecution(ScriptReference script)
    {
        var executions = Scripts.Where(e => e.Script != script).ToList();
        return executions.Count == Scripts.Count ? this : this with { Scripts = executions };
    }
}
