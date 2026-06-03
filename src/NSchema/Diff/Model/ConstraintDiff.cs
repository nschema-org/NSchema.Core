using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a primary or foreign key constraint.
/// </summary>
/// <param name="Kind">The change to the constraint.</param>
/// <param name="Type">Whether the constraint is a primary key or a foreign key.</param>
/// <param name="Name">The constraint name.</param>
/// <param name="PrimaryKey">The primary key definition for an added primary key; otherwise <see langword="null"/>.</param>
/// <param name="ForeignKey">The foreign key definition for an added foreign key; otherwise <see langword="null"/>.</param>
public sealed record ConstraintDiff(
    ChangeKind Kind,
    ConstraintType Type,
    string Name,
    PrimaryKey? PrimaryKey,
    ForeignKey? ForeignKey
);
