namespace NSchema.Project.Nsql.Syntax.Extensions;

/// <summary>
/// <c>DROP EXTENSION name;</c>
/// </summary>
/// <param name="Name">The dropped extension.</param>
public sealed record DropExtensionStatement(Identifier Name) : NsqlStatement;
