using NSchema.Project.Domain.Models;

namespace NSchema.Project.Nsql.Syntax.Routines;

/// <summary>
/// <c>CREATE FUNCTION|PROCEDURE schema.name [RENAMED FROM old] (arguments) definition;</c>
/// </summary>
/// <param name="Name">The routine name as written.</param>
/// <param name="Kind">Whether the statement declares a function or a procedure.</param>
/// <param name="Arguments">The argument list, verbatim (the text inside the parentheses; may be empty).</param>
/// <param name="Definition">Everything after the argument list, verbatim.</param>
public sealed record CreateRoutineStatement(
    QualifiedName Name,
    RoutineKind Kind,
    SqlText Arguments,
    SqlText Definition
) : NsqlStatement;
