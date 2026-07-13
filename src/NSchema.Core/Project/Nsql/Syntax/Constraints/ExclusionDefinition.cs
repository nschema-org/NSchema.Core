using NSchema.Project.Domain.Models;
using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name EXCLUDE [USING method] (element WITH operator, …) [WHERE (predicate)]</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Elements">The exclusion elements.</param>
/// <param name="Method">The access method after <c>USING</c>, or <see langword="null"/>.</param>
/// <param name="Predicate">The partial-constraint predicate, or <see langword="null"/>.</param>
public sealed record ExclusionDefinition(
    Identifier Name,
    IReadOnlyList<ExclusionElement> Elements,
    Identifier? Method = null,
    SqlText? Predicate = null
) : TableMember;