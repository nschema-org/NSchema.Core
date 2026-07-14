namespace NSchema.Project.Domain.Models.Scripts;

/// <summary>
/// A raw-SQL script and the run semantics common to every script kind.
/// </summary>
/// <param name="Name">The name that identifies the script.</param>
/// <param name="Sql">The raw SQL to run.</param>
/// <param name="ScopeSchema">The schema the run is scoped to, or <see langword="null"/> when the script is global.</param>
public abstract record Script(SqlIdentifier Name, SqlText Sql, SqlIdentifier? ScopeSchema)
{
    /// <summary>
    /// The canonical hash of the script body.
    /// </summary>
    public string Hash => ScriptHashing.Hash(Sql);

    /// <summary>
    /// The script's address: its scope schema and its name.
    /// </summary>
    public ScriptReference Reference => new(ScopeSchema, Name);

    /// <summary>
    /// When true, the script runs outside the migration's transaction.
    /// </summary>
    public bool RunOutsideTransaction { get; init; }

    /// <summary>
    /// When the script runs, relative to occurrences of its event.
    /// </summary>
    public RunCondition RunCondition { get; init; } = RunCondition.Always;

    /// <summary>
    /// The event as written in DDL source.
    /// </summary>
    public abstract string Description { get; }
}
