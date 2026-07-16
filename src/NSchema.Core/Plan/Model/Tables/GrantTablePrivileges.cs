using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Domain.Models.Tables;

/// <summary>
/// Represents granting specific privileges on a table to a role in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table for which privileges are being granted.</param>
/// <param name="TableName">The name of the table for which privileges are being granted.</param>
/// <param name="Role">The name of the role to which the privileges will be granted.</param>
/// <param name="Privileges">The specific privileges being granted to the role for the table.</param>
public sealed record GrantTablePrivileges(SqlIdentifier SchemaName, SqlIdentifier TableName, SqlIdentifier Role, TablePrivilege Privileges) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
