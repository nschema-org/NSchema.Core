namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A top-level statement in an NSchema source file.
/// </summary>
public abstract record NsqlStatement : NsqlNode
{
    /// <summary>
    /// The documentation comment preceding the statement, if any.
    /// </summary>
    public string? Doc { get; init; }
}
