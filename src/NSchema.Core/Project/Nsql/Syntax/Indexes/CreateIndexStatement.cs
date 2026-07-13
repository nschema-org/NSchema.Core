using NSchema.Project.Domain.Models;

namespace NSchema.Project.Nsql.Syntax.Indexes;

/// <summary>
/// <c>CREATE [UNIQUE] INDEX name ON schema.relation [USING method] (keys) [INCLUDE (columns)] [WHERE (predicate)];</c>
/// </summary>
/// <param name="Name">The index name.</param>
/// <param name="IsUnique">Whether the index is declared <c>UNIQUE</c>.</param>
/// <param name="On">The table or materialized view the index attaches to.</param>
/// <param name="Columns">The index keys.</param>
/// <param name="Method">The access method after <c>USING</c>, or <see langword="null"/>.</param>
/// <param name="Include">The <c>INCLUDE</c> columns (empty when absent).</param>
/// <param name="Predicate">The partial-index predicate, or <see langword="null"/>.</param>
public sealed record CreateIndexStatement(
    Identifier Name,
    bool IsUnique,
    QualifiedName On,
    IReadOnlyList<IndexElement> Columns,
    Identifier? Method = null,
    IReadOnlyList<Identifier>? Include = null,
    SqlText? Predicate = null
) : NsqlStatement;