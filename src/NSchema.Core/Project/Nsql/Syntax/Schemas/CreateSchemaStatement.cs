namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>CREATE [PARTIAL] SCHEMA name [RENAMED FROM old];</c>
/// </summary>
/// <param name="Name">The schema name.</param>
public sealed record CreateSchemaStatement(Identifier Name) : NsqlStatement;
