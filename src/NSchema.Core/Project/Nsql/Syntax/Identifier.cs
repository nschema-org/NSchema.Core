namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A name as written in the source, casing preserved.
/// </summary>
/// <param name="Text">The written text of the name.</param>
public sealed record Identifier(string Text) : NsqlNode;