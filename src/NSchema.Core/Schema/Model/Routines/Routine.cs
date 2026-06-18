using System.Diagnostics;

namespace NSchema.Schema.Model.Routines;

/// <summary>
/// Represents a database routine. A function or a procedure (see <see cref="Kind"/>).
/// </summary>
/// <param name="Name">The name of the routine.</param>
/// <param name="Kind">Whether the routine is a function or a procedure.</param>
/// <param name="Arguments">The argument list, stored verbatim (the text inside the parentheses; may be empty).</param>
/// <param name="Definition">Everything after the argument list, stored verbatim.</param>
/// <param name="OldName">The previous name of the routine, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the routine.</param>
[DebuggerDisplay("{Name,nq} ({Kind})")]
public sealed record Routine(
    string Name,
    RoutineKind Kind,
    string Arguments,
    string Definition,
    string? OldName = null,
    string? Comment = null
) : IRenameableObject;
