namespace NSchema.Diff.Model;

/// <summary>
/// Identifies the kind of table constraint a <see cref="ConstraintDiff"/> describes.
/// </summary>
public enum ConstraintType
{
    /// <summary>
    /// A primary key constraint.
    /// </summary>
    PrimaryKey,

    /// <summary>
    /// A foreign key constraint.
    /// </summary>
    ForeignKey,
}
