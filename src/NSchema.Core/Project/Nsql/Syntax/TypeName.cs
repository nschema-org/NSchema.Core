namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A type reference as written: an optionally schema-qualified type name plus its optional
/// parenthesised arguments (e.g. <c>varchar(100)</c>, <c>numeric(10,2)</c>, <c>app.status</c>).
/// </summary>
/// <param name="Schema">The schema qualifier for a user-defined type, or <see langword="null"/>.</param>
/// <param name="Name">The type name.</param>
/// <param name="Arguments">The text inside the parentheses (e.g. <c>100</c> or <c>10,2</c>), or <see langword="null"/>.</param>
public sealed record TypeName(Identifier? Schema, Identifier Name, string? Arguments = null) : NsqlNode;
