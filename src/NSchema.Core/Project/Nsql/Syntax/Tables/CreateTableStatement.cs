namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>CREATE TABLE schema.name [RENAMED FROM old] ( members… );</c>
/// </summary>
/// <param name="Name">The table name as written.</param>
/// <param name="Members">The body members in declaration order (columns, constraints, indexes, includes).</param>
/// <param name="RenamedFrom">The previous name from a <c>RENAMED FROM</c> clause, or <see langword="null"/>.</param>
public sealed record CreateTableStatement(
    QualifiedName Name,
    IReadOnlyList<TableMember> Members,
    Identifier? RenamedFrom = null
) : NsqlStatement;