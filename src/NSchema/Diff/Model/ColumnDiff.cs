using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes the changes affecting a single column. For an added or dropped column the full
/// <paramref name="Definition"/> is provided; for a modified column the individual field changes are populated.
/// </summary>
/// <param name="Name">The column name (the new name when renamed).</param>
/// <param name="Kind">The change to the column.</param>
/// <param name="Definition">The full column definition for added or dropped columns; otherwise <see langword="null"/>.</param>
/// <param name="RenamedFrom">The previous column name when renamed; otherwise <see langword="null"/>.</param>
/// <param name="Type">The change to the column's type, if any.</param>
/// <param name="Nullability">The change to the column's nullability, if any.</param>
/// <param name="Default">The change to the column's default value, if any.</param>
/// <param name="Identity">The change to the column's identity options, if any.</param>
/// <param name="Comment">The change to the column's comment, if any.</param>
public sealed record ColumnDiff(
    string Name,
    ChangeKind Kind,
    Column? Definition,
    string? RenamedFrom,
    ValueChange<SqlType>? Type,
    ValueChange<bool>? Nullability,
    ValueChange<string>? Default,
    ValueChange<IdentityOptions>? Identity,
    ValueChange<string>? Comment);
