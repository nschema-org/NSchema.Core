namespace NSchema.Project.Nsql.Syntax.Enums;

/// <summary>
/// <c>DROP ENUM schema.name;</c>
/// </summary>
/// <param name="Name">The dropped enum.</param>
public sealed record DropEnumStatement(QualifiedName Name) : NsqlStatement;
