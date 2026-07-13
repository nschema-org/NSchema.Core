namespace NSchema.Project.Nsql.Syntax.Sequences;

/// <summary>
/// <c>CREATE SEQUENCE schema.name [RENAMED FROM old] [(options)];</c>
/// </summary>
/// <param name="Name">The sequence name as written.</param>
/// <param name="Options">The options clause, or <see langword="null"/> when absent.</param>
/// <param name="RenamedFrom">The previous name from a <c>RENAMED FROM</c> clause, or <see langword="null"/>.</param>
public sealed record CreateSequenceStatement(
    QualifiedName Name,
    SequenceOptionsClause? Options = null,
    Identifier? RenamedFrom = null
) : NsqlStatement;
