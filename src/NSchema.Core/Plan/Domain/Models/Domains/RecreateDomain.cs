using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Domains;

namespace NSchema.Plan.Domain.Models.Domains;

/// <summary>
/// Represents dropping and recreating a domain whose base type changed.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="DomainType">The desired domain to recreate.</param>
public sealed record RecreateDomain(SqlIdentifier SchemaName, DomainType DomainType) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
