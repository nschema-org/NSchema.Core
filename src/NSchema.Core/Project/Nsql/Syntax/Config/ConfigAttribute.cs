namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// A single <c>key = value</c> configuration attribute; the key may be dotted (<c>pool.max</c>).
/// </summary>
/// <param name="Key">The attribute key as written.</param>
/// <param name="Value">The attribute value.</param>
public sealed record ConfigAttribute(string Key, ConfigValueNode Value) : NsqlNode;