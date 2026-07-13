namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>GRANT privilege[, privilege]… ON schema.table TO role;</c>
/// </summary>
/// <param name="Privileges">The granted privileges.</param>
/// <param name="On">The table granted on.</param>
/// <param name="Role">The role granted to.</param>
public sealed record GrantTableStatement(TablePrivilege Privileges, QualifiedName On, Identifier Role) : NsqlStatement;