namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// A referential action on a foreign key, as written.
/// </summary>
public enum ReferentialAction
{
    /// <summary>
    /// <c>NO ACTION</c> (the default).
    /// </summary>
    NoAction,

    /// <summary>
    /// <c>CASCADE</c>.
    /// </summary>
    Cascade,

    /// <summary>
    /// <c>SET NULL</c>.
    /// </summary>
    SetNull,

    /// <summary>
    /// <c>SET DEFAULT</c>.
    /// </summary>
    SetDefault
}
