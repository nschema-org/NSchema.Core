using NSchema.Model;
using NSchema.Model.Indexes;

namespace NSchema.Diff.Domain.Models.Indexes;

/// <summary>
/// Describes a change to a table index.
/// </summary>
/// <param name="Kind">The change to the index.</param>
/// <param name="Name">The index name.</param>
/// <param name="Definition">The full index definition for a created index; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the index's comment, if any.</param>
public sealed record IndexDiff(ChangeKind Kind, SqlIdentifier Name, TableIndex? Definition = null, ValueChange<string>? Comment = null) : INamedObjectDiff;
