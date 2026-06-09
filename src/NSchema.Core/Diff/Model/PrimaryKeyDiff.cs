using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a table's primary key.
/// </summary>
/// <param name="Kind">The change to the primary key.</param>
/// <param name="Name">The primary key constraint name.</param>
/// <param name="Definition">The primary key definition for an added primary key; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the constraint's comment, if any (carried on a comment-only <see cref="ChangeKind.Modify"/>).</param>
public sealed record PrimaryKeyDiff(ChangeKind Kind, string Name, PrimaryKey? Definition, ValueChange<string>? Comment = null);
