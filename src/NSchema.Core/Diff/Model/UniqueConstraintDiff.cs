using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a table's unique constraint. A changed unique constraint surfaces as a Remove followed by an Add.
/// </summary>
/// <param name="Kind">The change to the unique constraint.</param>
/// <param name="Name">The unique constraint name.</param>
/// <param name="Definition">The unique constraint definition for an added constraint; otherwise <see langword="null"/>.</param>
public sealed record UniqueConstraintDiff(ChangeKind Kind, string Name, UniqueConstraint? Definition);
