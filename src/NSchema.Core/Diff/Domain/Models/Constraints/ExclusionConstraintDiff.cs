using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Constraints;

namespace NSchema.Diff.Domain.Models.Constraints;

/// <summary>
/// Describes a change to a table's exclusion constraint. A changed exclusion constraint surfaces as a Remove followed by an Add.
/// </summary>
/// <param name="Kind">The change to the exclusion constraint.</param>
/// <param name="Name">The exclusion constraint name.</param>
/// <param name="Definition">The exclusion constraint definition for an added constraint; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the constraint's comment, if any (carried on a comment-only <see cref="ChangeKind.Modify"/>).</param>
public sealed record ExclusionConstraintDiff(ChangeKind Kind, SqlIdentifier Name, ExclusionConstraint? Definition = null, ValueChange<string>? Comment = null) : INamedObjectDiff
{
    /// <summary>
    /// The name of the script matched to this change.
    /// </summary>
    public SqlIdentifier? MigrationScript { get; init; }
}
