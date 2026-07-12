using NSchema.Project.Domain.Models.Columns;

namespace NSchema.Diff.Domain.Models.Columns;

/// <summary>
/// Describes the changes affecting a single column.
/// </summary>
/// <param name="Name">The column name (the new name when renamed).</param>
/// <param name="Kind">The change to the column.</param>
/// <param name="Definition">The full column definition.</param>
/// <param name="RenamedFrom">The previous column name when renamed; otherwise <see langword="null"/>.</param>
/// <param name="Type">The change to the column's type, if any.</param>
/// <param name="Nullability">The change to the column's nullability, if any.</param>
/// <param name="Default">The change to the column's default value, if any.</param>
/// <param name="Identity">The change to the column's identity options, if any.</param>
/// <param name="Comment">The change to the column's comment, if any.</param>
/// <param name="Generated">The change to the column's stored generation expression, if any.</param>
public sealed record ColumnDiff(
    string Name,
    ChangeKind Kind,
    Column? Definition = null,
    string? RenamedFrom = null,
    ValueChange<SqlType>? Type = null,
    ValueChange<bool>? Nullability = null,
    ValueChange<string>? Default = null,
    ValueChange<IdentityOptions>? Identity = null,
    ValueChange<string>? Comment = null,
    ValueChange<string>? Generated = null
) : INamedObjectDiff
{
    /// <summary>
    /// The name of the script matched to this change.
    /// </summary>
    public string? MigrationScript { get; init; }
}
