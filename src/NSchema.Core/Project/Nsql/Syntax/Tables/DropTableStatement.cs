namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>DROP TABLE schema.name;</c>
/// </summary>
/// <param name="Name">The dropped table.</param>
public sealed record DropTableStatement(QualifiedName Name) : NsqlStatement;