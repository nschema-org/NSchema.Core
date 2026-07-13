using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Domains;

namespace NSchema.Plan.Domain.Models.Domains;

/// <summary>
/// Represents dropping and recreating a domain whose base type changed.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="DomainDefinition">The desired domain to recreate.</param>
public sealed record RecreateDomain(SqlIdentifier SchemaName, DomainDefinition DomainDefinition) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
