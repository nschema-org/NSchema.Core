using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.State.Model;

/// <summary>
/// The recorded state: the captured schema snapshot plus the run-once script executions. The schema half is a
/// rebuildable cache of the live database; the execution ledger is the one part a refresh cannot reconstruct,
/// so writers must carry it over rather than replace it.
/// </summary>
/// <param name="Schema">The captured database schema.</param>
/// <param name="Scripts">The recorded script executions.</param>
public sealed record SchemaState(DatabaseSchema Schema, IReadOnlyList<ScriptRecord> Scripts)
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
    /// Records executions of the given scripts into the ledger, replacing any earlier execution recorded under the same name.
    /// </summary>
    /// <param name="scripts">The scripts that were executed.</param>
    /// <param name="executedUtc">When the executions are recorded.</param>
    public SchemaState RecordScripts(IReadOnlyList<ScriptHash> scripts, DateTimeOffset executedUtc)
    {
        if (scripts.Count == 0)
        {
            return this;
        }

        var executions = Scripts
            .Where(e => !scripts.Any(s => string.Equals(s.Name, e.Name, StringComparison.OrdinalIgnoreCase)))
            .Concat(scripts.Select(s => new ScriptRecord(s.Name, s.Hash, executedUtc)))
            .ToList();
        return this with { Scripts = executions };
    }

    /// <summary>
    /// Finds the recorded execution for the given script name, or <see langword="null"/> when none is recorded.
    /// </summary>
    /// <param name="name">The script's declared name.</param>
    public ScriptRecord? FindScript(string name) =>
        Scripts.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Removes the recorded execution for the given script name, so a later plan runs the script again.
    /// </summary>
    /// <param name="name">The script's declared name.</param>
    public SchemaState RemoveScript(string name)
    {
        var executions = Scripts
            .Where(e => !string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return executions.Count == Scripts.Count ? this : this with { Scripts = executions };
    }
}
