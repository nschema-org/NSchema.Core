using NSchema.Schema.Model.Domains;

namespace NSchema.Plan.Model.Domains;

/// <summary>
/// Represents dropping and recreating a domain whose base type changed.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="Domain">The desired domain to recreate.</param>
public sealed record RecreateDomain(string SchemaName, Domain Domain) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
