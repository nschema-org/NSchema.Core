using System.Diagnostics;

namespace NSchema.Model.Routines;

/// <summary>
/// Represents a database routine. A function or a procedure (see <see cref="RoutineKind"/>).
/// </summary>
/// <param name="name">The name of the routine.</param>
/// <param name="routineKind">Whether the routine is a function or a procedure.</param>
/// <param name="arguments">The argument list, stored verbatim (the text inside the parentheses; may be empty).</param>
/// <param name="definition">Everything after the argument list, stored verbatim.</param>
[DebuggerDisplay("{Name,nq} ({RoutineKind})")]
public sealed class Routine(SqlIdentifier name, RoutineKind routineKind, SqlText arguments, SqlText definition) : DatabaseObject(name), IEquatable<Routine>
{
    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.Routine;

    /// <summary>
    /// Whether the routine is a function or a procedure.
    /// </summary>
    public RoutineKind RoutineKind { get; init; } = routineKind;

    /// <summary>
    /// The argument list, stored verbatim (the text inside the parentheses; may be empty).
    /// </summary>
    public SqlText Arguments { get; init; } = arguments;

    /// <summary>
    /// Everything after the argument list, stored verbatim.
    /// </summary>
    public SqlText Definition { get; init; } = definition;

    internal Routine Clone() => new(Name, RoutineKind, Arguments, Definition) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(Routine? other) =>
        other is not null
        && Name == other.Name
        && RoutineKind == other.RoutineKind
        && Arguments == other.Arguments
        && Definition == other.Definition;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Routine other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, RoutineKind, Arguments, Definition);
}
