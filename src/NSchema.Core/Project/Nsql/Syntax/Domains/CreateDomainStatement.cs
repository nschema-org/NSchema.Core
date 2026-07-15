using NSchema.Model;
using NSchema.Project.Nsql.Syntax.Constraints;

namespace NSchema.Project.Nsql.Syntax.Domains;

/// <summary>
/// <c>CREATE DOMAIN schema.name [RENAMED FROM old] AS type [NOT NULL | NULL] [CONSTRAINT n CHECK (e)]… [DEFAULT expr];</c>
/// </summary>
/// <param name="Name">The domain name as written.</param>
/// <param name="Type">The base type as written.</param>
/// <param name="NotNull">Whether the domain is declared <c>NOT NULL</c>.</param>
/// <param name="Checks">The named check constraints (empty when absent).</param>
/// <param name="Default">The <c>DEFAULT</c> expression, or <see langword="null"/>.</param>
public sealed record CreateDomainStatement(
    QualifiedName Name,
    TypeName Type,
    bool NotNull = false,
    IReadOnlyList<CheckDefinition>? Checks = null,
    SqlText? Default = null
) : NsqlStatement
{
    /// <summary>
    /// The named check constraints (empty when absent).
    /// </summary>
    public IReadOnlyList<CheckDefinition> Checks { get; init; } = Checks ?? [];
}
