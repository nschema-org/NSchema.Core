using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a table's primary key.
/// </summary>
/// <param name="Kind">The change to the primary key.</param>
/// <param name="Name">The primary key constraint name.</param>
/// <param name="Definition">The primary key definition for an added primary key; otherwise <see langword="null"/>.</param>
public sealed record PrimaryKeyDiff(ChangeKind Kind, string Name, PrimaryKey? Definition);
