namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// A member of a table body (or of a table template's body).
/// </summary>
public abstract record TableMember : NsqlNode
{
    /// <summary>
    /// The documentation comment preceding the member, if any.
    /// </summary>
    public string? Doc { get; init; }
}
