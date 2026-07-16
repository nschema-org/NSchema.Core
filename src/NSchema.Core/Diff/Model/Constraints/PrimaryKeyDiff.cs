using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;

namespace NSchema.Diff.Model.Constraints;

/// <summary>
/// Describes a change to a table's primary key.
/// </summary>
/// <param name="Kind">The change to the primary key.</param>
/// <param name="Name">The primary key constraint name.</param>
/// <param name="Definition">The primary key definition for an added primary key; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the constraint's comment, if any (carried on a comment-only <see cref="ChangeKind.Modify"/>).</param>
public sealed record PrimaryKeyDiff(ChangeKind Kind, SqlIdentifier Name, PrimaryKey? Definition = null, ValueChange<string>? Comment = null) : INamedObjectDiff
{
    /// <summary>
    /// The change-event script matched to this change, run at this point in the plan (<see langword="null"/> when none).
    /// </summary>
    public ChangeScript? MigrationScript { get; init; }
}
