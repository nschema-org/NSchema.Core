namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>GRANT USAGE ON SCHEMA name TO role;</c>
/// </summary>
/// <param name="Schema">The schema granted on.</param>
/// <param name="Role">The role granted to.</param>
public sealed record GrantSchemaUsageStatement(Identifier Schema, Identifier Role) : NsqlStatement;
