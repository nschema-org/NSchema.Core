namespace NSchema.Schema.Model.Scripts;

/// <summary>
/// A raw-SQL script, together with the event it runs on.
/// </summary>
/// <param name="Name">The name that identifies the script.</param>
/// <param name="Sql">The raw SQL to run when the event occurs.</param>
/// <param name="Event">The event that will cause the script to be run.</param>
public record Script(string Name, string Sql, ScriptEvent Event)
{
    /// <summary>
    /// The canonical hash of the script body.
    /// </summary>
    public string Hash => ScriptHashing.Hash(Sql);

    /// <summary>
    /// When true, the script runs outside the migration's transaction.
    /// </summary>
    public bool RunOutsideTransaction { get; init; }

    /// <summary>
    /// When the script runs, relative to occurrences of its event.
    /// </summary>
    public RunCondition RunCondition { get; init; } = RunCondition.Always;
}
