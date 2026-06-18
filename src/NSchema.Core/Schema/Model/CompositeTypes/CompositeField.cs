using NSchema.Schema.Model.Columns;

namespace NSchema.Schema.Model.CompositeTypes;

/// <summary>
/// A single named, typed field (attribute) of a <see cref="CompositeType"/>.
/// </summary>
/// <param name="Name">The field name.</param>
/// <param name="DataType">The field's type.</param>
public sealed record CompositeField(string Name, SqlType DataType);
