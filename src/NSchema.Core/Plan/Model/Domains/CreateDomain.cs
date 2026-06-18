using NSchema.Schema.Model.Domains;

namespace NSchema.Plan.Model.Domains;

/// <summary>
/// Represents the creation of a domain, with its default, not-null requirement and check constraints inline.
/// </summary>
/// <param name="SchemaName">The name of the schema the domain belongs to.</param>
/// <param name="Domain">The definition of the domain to create.</param>
public sealed record CreateDomain(string SchemaName, Domain Domain) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
