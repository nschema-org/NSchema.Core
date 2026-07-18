using NSchema.Model;

namespace NSchema.Plan.Model.Domains;

/// <summary>
/// Represents dropping a check constraint from a domain (<c>ALTER DOMAIN … DROP CONSTRAINT …</c>).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="DomainName">The name of the domain.</param>
/// <param name="CheckName">The name of the check constraint to drop.</param>
public sealed record DropDomainCheck(SqlIdentifier SchemaName, SqlIdentifier DomainName, SqlIdentifier CheckName) : MigrationAction;
