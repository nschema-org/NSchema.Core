using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;

namespace NSchema.Diff.Model.CompositeTypes;

/// <summary>
/// Describes a change to a single field of a composite type.
/// </summary>
/// <param name="Kind">The change to the field.</param>
/// <param name="Name">The field name.</param>
/// <param name="Definition">The field definition for an added field; otherwise <see langword="null"/>.</param>
/// <param name="Type">The change to the field's type, set on an in-place retype (<c>ALTER ATTRIBUTE … TYPE</c>).</param>
public sealed record CompositeFieldDiff(ChangeKind Kind, SqlIdentifier Name, CompositeField? Definition = null, ValueChange<SqlType>? Type = null);
