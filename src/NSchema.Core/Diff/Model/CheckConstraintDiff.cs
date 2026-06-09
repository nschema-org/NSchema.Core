using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a table's check constraint. A changed check constraint surfaces as a Remove followed by an Add.
/// </summary>
/// <param name="Kind">The change to the check constraint.</param>
/// <param name="Name">The check constraint name.</param>
/// <param name="Definition">The check constraint definition for an added constraint; otherwise <see langword="null"/>.</param>
public sealed record CheckConstraintDiff(ChangeKind Kind, string Name, CheckConstraint? Definition);
