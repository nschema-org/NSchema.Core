using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Domains;

namespace NSchema.Plan.Domain.Models.Domains;

/// <summary>
/// Represents the creation of a domain, with its default, not-null requirement and check constraints inline.
/// </summary>
/// <param name="SchemaName">The name of the schema the domain belongs to.</param>
/// <param name="DomainDefinition">The definition of the domain to create.</param>
public sealed record CreateDomain(SqlIdentifier SchemaName, DomainDefinition DomainDefinition) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
