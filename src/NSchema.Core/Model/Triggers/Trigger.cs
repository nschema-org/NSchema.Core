using System.Diagnostics;
using NSchema.Model.Routines;

namespace NSchema.Model.Triggers;

/// <summary>
/// Represents a trigger on a table fired on a table operation.
/// </summary>
/// <remarks>
/// Creates a trigger.
/// </remarks>
/// <param name="name">The trigger name (unique within its table).</param>
/// <param name="timing">When the trigger fires relative to the operation.</param>
/// <param name="events">The operation(s) that fire the trigger.</param>
/// <param name="function">The function the trigger executes (optionally schema-qualified); null for an inline-body trigger.</param>
/// <param name="level">Whether the trigger fires per row or per statement.</param>
/// <param name="updateOfColumns">The columns an <c>UPDATE</c> trigger is narrowed to, if any; empty otherwise.</param>
/// <param name="when">An optional <c>WHEN</c> condition, stored verbatim (opaque SQL).</param>
/// <param name="functionArguments">Optional literal arguments passed to the function, stored verbatim; <see langword="null"/> when none.</param>
/// <param name="body">The trigger's inline statement body, stored verbatim (opaque SQL). Use <see langword="null"/> for a function-style trigger.</param>
[DebuggerDisplay("{Name,nq} (trigger)")]
public sealed class Trigger(
    SqlIdentifier name,
    TriggerTiming timing,
    TriggerEvent events,
    RoutineReference? function = null,
    TriggerLevel level = TriggerLevel.Statement,
    List<SqlIdentifier>? updateOfColumns = null,
    SqlText? when = null,
    SqlText? functionArguments = null,
    SqlText? body = null
) : DatabaseMember(name), IEquatable<Trigger>
{
    /// <summary>
    /// When the trigger fires relative to the operation.
    /// </summary>
    public TriggerTiming Timing { get; set; } = timing;

    /// <summary>
    /// The operation(s) that fire the trigger.
    /// </summary>
    public TriggerEvent Events { get; set; } = events;

    /// <summary>
    /// The function the trigger executes (optionally schema-qualified); null for an inline-body trigger.
    /// </summary>
    public RoutineReference? Function { get; set; } = function;

    /// <summary>
    /// Whether the trigger fires per row or per statement.
    /// </summary>
    public TriggerLevel Level { get; set; } = level;

    /// <summary>
    /// The columns an <c>UPDATE</c> trigger is narrowed to (<c>UPDATE OF (…)</c>), if any.
    /// </summary>
    public List<SqlIdentifier> UpdateOfColumns { get; } = updateOfColumns ?? [];

    /// <summary>
    /// An optional <c>WHEN</c> condition, stored verbatim (opaque SQL).
    /// </summary>
    public SqlText? When { get; set; } = when;

    /// <summary>
    /// Optional literal arguments passed to the function, stored verbatim; <see langword="null"/> when none.
    /// </summary>
    public SqlText? FunctionArguments { get; set; } = functionArguments;

    /// <summary>
    /// The trigger's inline statement body, stored verbatim (opaque SQL); <see langword="null"/> for a
    /// function-style trigger.
    /// </summary>
    public SqlText? Body { get; set; } = body;

    /// <inheritdoc/>
    public override Trigger Clone() =>
        new(Name, Timing, Events, Function, Level, [.. UpdateOfColumns], When, FunctionArguments, Body) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition;.
    /// </summary>
    public bool Equals(Trigger? other) =>
        other is not null
        && Name == other.Name
        && Timing == other.Timing
        && Events == other.Events
        && Equals(Function, other.Function)
        && Level == other.Level
        && UpdateOfColumns.SequenceEqual(other.UpdateOfColumns)
        && Equals(When, other.When)
        && Equals(FunctionArguments, other.FunctionArguments)
        && Equals(Body, other.Body);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Trigger other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Timing, Events, Function, Level, When, FunctionArguments, Body);
}
