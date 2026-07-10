using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.State.Model;

/// <summary>
/// The recorded state: the captured schema snapshot plus the run-once script executions. The schema half is a
/// rebuildable cache of the live database; the execution ledger is the one part a refresh cannot reconstruct,
/// so writers must carry it over rather than replace it.
/// </summary>
/// <param name="Schema">The captured database schema.</param>
/// <param name="ExecutedScripts">The recorded run-once script executions.</param>
internal sealed record SchemaState(DatabaseSchema Schema, IReadOnlyList<ScriptExecutionRecord> ExecutedScripts)
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
    /// Records executions of the given scripts into the ledger, replacing any earlier execution recorded
    /// under the same name.
    /// </summary>
    /// <param name="scripts">The scripts that were executed.</param>
    /// <param name="executedUtc">When the executions are recorded.</param>
    public SchemaState RecordExecutions(IReadOnlyList<ScriptHash> scripts, DateTimeOffset executedUtc)
    {
        if (scripts.Count == 0)
        {
            return this;
        }

        var executions = ExecutedScripts
            .Where(e => !scripts.Any(s => string.Equals(s.Name, e.Name, StringComparison.OrdinalIgnoreCase)))
            .Concat(scripts.Select(s => new ScriptExecutionRecord(s.Name, s.Hash, executedUtc)))
            .ToList();
        return this with { ExecutedScripts = executions };
    }
}
