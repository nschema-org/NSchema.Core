using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a table's check constraint. A changed check constraint surfaces as a Remove followed by an Add.
/// </summary>
/// <param name="Kind">The change to the check constraint.</param>
/// <param name="Name">The check constraint name.</param>
/// <param name="Definition">The check constraint definition for an added constraint; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the constraint's comment, if any (carried on a comment-only <see cref="ChangeKind.Modify"/>).</param>
public sealed record CheckConstraintDiff(ChangeKind Kind, string Name, CheckConstraint? Definition = null, ValueChange<string>? Comment = null);
