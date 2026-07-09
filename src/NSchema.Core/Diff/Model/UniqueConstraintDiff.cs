using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Migrations;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a table's unique constraint. A changed unique constraint surfaces as a Remove followed by an Add.
/// </summary>
/// <param name="Kind">The change to the unique constraint.</param>
/// <param name="Name">The unique constraint name.</param>
/// <param name="Definition">The unique constraint definition for an added constraint; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the constraint's comment, if any (carried on a comment-only <see cref="ChangeKind.Modify"/>).</param>
public sealed record UniqueConstraintDiff(ChangeKind Kind, string Name, UniqueConstraint? Definition = null, ValueChange<string>? Comment = null) : INamedObjectDiff
{
    /// <summary>
    /// The data migration matched to this change, when one is declared; otherwise <see langword="null"/>.
    /// </summary>
    public DataMigration? Migration { get; init; }
}
