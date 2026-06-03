using NSchema.Schema;

namespace NSchema.Migration.Diff;

/// <summary>
/// Describes a change to a table index.
/// </summary>
/// <param name="Kind">The change to the index.</param>
/// <param name="Name">The index name.</param>
/// <param name="Definition">The full index definition for a created index; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the index's comment, if any.</param>
public sealed record IndexDiff(ChangeKind Kind, string Name, TableIndex? Definition, ValueChange<string>? Comment);
