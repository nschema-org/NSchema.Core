using NSchema.Model;

namespace NSchema.Plan.Model.Domains;

/// <summary>
/// Represents the removal of an existing domain from the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain to be removed.</param>
/// <param name="DomainName">The name of the domain to be removed.</param>
public sealed record DropDomain(SqlIdentifier SchemaName, SqlIdentifier DomainName) : MigrationAction;
