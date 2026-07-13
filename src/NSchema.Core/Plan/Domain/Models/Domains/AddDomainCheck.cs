using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Constraints;

namespace NSchema.Plan.Domain.Models.Domains;

/// <summary>
/// Represents adding a check constraint to a domain (<c>ALTER DOMAIN … ADD CONSTRAINT … CHECK (…)</c>).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="DomainName">The name of the domain.</param>
/// <param name="Check">The check constraint to add.</param>
public sealed record AddDomainCheck(SqlIdentifier SchemaName, SqlIdentifier DomainName, CheckConstraint Check) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
