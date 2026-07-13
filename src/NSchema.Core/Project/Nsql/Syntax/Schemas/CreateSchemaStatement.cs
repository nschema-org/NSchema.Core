namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>CREATE [PARTIAL] SCHEMA name [RENAMED FROM old];</c>
/// </summary>
/// <param name="Name">The schema name.</param>
/// <param name="IsPartial">Whether the schema is declared <c>PARTIAL</c>.</param>
/// <param name="RenamedFrom">The previous name from a <c>RENAMED FROM</c> clause, or <see langword="null"/>.</param>
public sealed record CreateSchemaStatement(Identifier Name, bool IsPartial = false, Identifier? RenamedFrom = null) : NsqlStatement;
