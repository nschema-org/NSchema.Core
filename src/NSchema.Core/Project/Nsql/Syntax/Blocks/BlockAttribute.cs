namespace NSchema.Project.Nsql.Syntax.Blocks;

/// <summary>
/// A single <c>key = value</c> attribute of a block; the key may be dotted (<c>pool.max</c>).
/// </summary>
/// <param name="Key">The attribute key as written.</param>
/// <param name="Value">The attribute value as written; the binder converts it to the target type.</param>
public sealed record BlockAttribute(string Key, string Value) : NsqlNode;
