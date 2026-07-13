using NSchema.Project.Domain.Models.Columns;

namespace NSchema.Project.Domain.Models.CompositeTypes;

/// <summary>
/// A single named, typed field (attribute) of a <see cref="CompositeType"/>.
/// </summary>
/// <param name="Name">The field name.</param>
/// <param name="DataType">The field's type.</param>
public sealed record CompositeField(SqlIdentifier Name, SqlType DataType);
