namespace NSchema.Plan.Domain.Models.Domains;

/// <summary>
/// Represents adding or dropping a domain's not-null requirement (<c>ALTER DOMAIN … SET/DROP NOT NULL</c>).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="DomainName">The name of the domain.</param>
/// <param name="NotNull">Whether the domain should forbid <c>NULL</c> after the change.</param>
public sealed record AlterDomainNotNull(string SchemaName, string DomainName, bool NotNull) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
