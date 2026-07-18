using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Model.Tables;

/// <summary>
/// Represents revoking specific privileges on a table from a role in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table for which privileges are being revoked.</param>
/// <param name="TableName">The name of the table for which privileges are being revoked.</param>
/// <param name="Role">The name of the role from which the privileges will be revoked.</param>
/// <param name="Privileges">The specific privileges being revoked from the role for the table.</param>
public sealed record RevokeTablePrivileges(SqlIdentifier SchemaName, SqlIdentifier TableName, SqlIdentifier Role, TablePrivilege Privileges) : MigrationAction;
