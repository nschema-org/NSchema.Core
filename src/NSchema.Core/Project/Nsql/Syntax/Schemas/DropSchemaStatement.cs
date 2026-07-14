namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>DROP SCHEMA name;</c>
/// </summary>
/// <param name="Name">The dropped schema.</param>
public sealed record DropSchemaStatement(Identifier Name) : NsqlStatement;
