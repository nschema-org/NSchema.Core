namespace NSchema.Project.Nsql.Syntax.CompositeTypes;

/// <summary>
/// <c>CREATE TYPE schema.name [RENAMED FROM old] AS (field type, …);</c>
/// </summary>
/// <param name="Name">The composite type name as written.</param>
/// <param name="Fields">The fields in declaration order.</param>
public sealed record CreateCompositeTypeStatement(
    QualifiedName Name,
    IReadOnlyList<CompositeFieldDefinition> Fields
) : NsqlStatement;
