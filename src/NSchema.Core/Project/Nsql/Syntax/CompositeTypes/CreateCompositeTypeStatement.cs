namespace NSchema.Project.Nsql.Syntax.CompositeTypes;

/// <summary>
/// <c>CREATE TYPE schema.name [RENAMED FROM old] AS (field type, …);</c>
/// </summary>
/// <param name="Name">The composite type name as written.</param>
/// <param name="Fields">The fields in declaration order.</param>
/// <param name="RenamedFrom">The previous name from a <c>RENAMED FROM</c> clause, or <see langword="null"/>.</param>
public sealed record CreateCompositeTypeStatement(
    QualifiedName Name,
    IReadOnlyList<CompositeFieldDefinition> Fields,
    Identifier? RenamedFrom = null
) : NsqlStatement;
