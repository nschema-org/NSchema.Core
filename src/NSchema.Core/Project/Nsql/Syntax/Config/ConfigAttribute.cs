namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// A single <c>key = value</c> configuration attribute; the key may be dotted (<c>pool.max</c>).
/// </summary>
/// <param name="Key">The attribute key as written.</param>
/// <param name="Value">The attribute value as written; the configuration binder converts it to the target type.</param>
public sealed record ConfigAttribute(string Key, string Value) : NsqlNode;
