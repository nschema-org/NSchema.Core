namespace NSchema.Project.Nsql.Syntax.Enums;

/// <summary>
/// <c>CREATE ENUM schema.name [RENAMED FROM old] ('value', …);</c>
/// </summary>
/// <param name="Name">The enum name as written.</param>
/// <param name="Values">The values in declaration order.</param>
public sealed record CreateEnumStatement(
    QualifiedName Name,
    IReadOnlyList<string> Values
) : NsqlStatement;
