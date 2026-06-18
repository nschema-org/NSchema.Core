namespace NSchema.Plan.Model.Domains;

/// <summary>
/// Represents setting, changing, or dropping a domain's default expression (<c>ALTER DOMAIN … SET/DROP DEFAULT</c>).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="DomainName">The name of the domain.</param>
/// <param name="OldDefault">The previous default expression, if any.</param>
/// <param name="NewDefault">The new default expression, or <see langword="null"/> to drop it.</param>
public sealed record AlterDomainDefault(string SchemaName, string DomainName, string? OldDefault, string? NewDefault) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
