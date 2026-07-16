namespace NSchema.Model.Services;

/// <summary>
/// What sort of thing a <see cref="DependencyNode"/> is.
/// </summary>
internal enum DependencyKind
{
    /// <summary>
    /// A table.
    /// </summary>
    Table,

    /// <summary>
    /// A view.
    /// </summary>
    View,

    /// <summary>
    /// A foreign key constraint on a table.
    /// </summary>
    ForeignKey,
}
