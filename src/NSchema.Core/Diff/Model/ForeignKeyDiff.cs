using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a table's foreign key. A changed foreign key surfaces as a Remove followed by an Add.
/// </summary>
/// <param name="Kind">The change to the foreign key.</param>
/// <param name="Name">The foreign key constraint name.</param>
/// <param name="Definition">The foreign key definition for an added foreign key; otherwise <see langword="null"/>.</param>
public sealed record ForeignKeyDiff(ChangeKind Kind, string Name, ForeignKey? Definition);
