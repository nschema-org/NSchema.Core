using NSchema.Project.Domain.Models;

namespace NSchema.Project.Nsql.Syntax.Views;

/// <summary>
/// <c>CREATE [MATERIALIZED] VIEW schema.name [RENAMED FROM old] AS body;</c>
/// </summary>
/// <param name="Name">The view name as written.</param>
/// <param name="Body">The defining query, verbatim (the text after <c>AS</c>).</param>
/// <param name="IsMaterialized">Whether the view is materialized.</param>
/// <param name="RenamedFrom">The previous name from a <c>RENAMED FROM</c> clause, or <see langword="null"/>.</param>
public sealed record CreateViewStatement(
    QualifiedName Name,
    SqlText Body,
    bool IsMaterialized = false,
    Identifier? RenamedFrom = null
) : NsqlStatement;
