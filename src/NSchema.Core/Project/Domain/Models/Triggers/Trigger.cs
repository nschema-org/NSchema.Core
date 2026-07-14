using System.Diagnostics;

namespace NSchema.Project.Domain.Models.Triggers;

/// <summary>
/// Represents a trigger on a table fired on a table operation.
/// </summary>
/// <param name="Name">The trigger name (unique within its table).</param>
/// <param name="Timing">When the trigger fires relative to the operation.</param>
/// <param name="Events">The operation(s) that fire the trigger.</param>
/// <param name="Function">The function the trigger executes (optionally schema-qualified); null for an inline-body trigger.</param>
/// <param name="Level">Whether the trigger fires per row or per statement.</param>
/// <param name="UpdateOfColumns">The columns an <c>UPDATE</c> trigger is narrowed to, if any; empty otherwise.</param>
/// <param name="When">An optional <c>WHEN</c> condition, stored verbatim (opaque SQL).</param>
/// <param name="FunctionArguments">Optional literal arguments passed to the function, stored verbatim; <see langword="null"/> when none.</param>
/// <param name="Comment">An optional comment or description for the trigger.</param>
/// <param name="Body">The trigger's inline statement body, stored verbatim (opaque SQL). Use <see langword="null"/> for a function-style trigger.</param>
[DebuggerDisplay("{Name,nq} (trigger)")]
public record Trigger(
    SqlIdentifier Name,
    TriggerTiming Timing,
    TriggerEvent Events,
    RoutineReference? Function = null,
    TriggerLevel Level = TriggerLevel.Statement,
    IReadOnlyList<SqlIdentifier>? UpdateOfColumns = null,
    SqlText? When = null,
    SqlText? FunctionArguments = null,
    string? Comment = null,
    SqlText? Body = null
) : INamedObject
{
    /// <summary>
    /// The columns an <c>UPDATE</c> trigger is narrowed to (<c>UPDATE OF (…)</c>), if any.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> UpdateOfColumns { get; init; } = UpdateOfColumns ?? [];

    /// <summary>
    /// Determines structural equality, <em>excluding</em> <see cref="Comment"/>.
    /// </summary>
    /// <param name="other">The trigger to compare with.</param>
    /// <returns><see langword="true"/> when the two triggers are structurally equal.</returns>
    public virtual bool Equals(Trigger? other) =>
        other != null
        && Name == other.Name
        && Timing == other.Timing
        && Events == other.Events
        && Function == other.Function
        && Level == other.Level
        && UpdateOfColumns.SequenceEqual(other.UpdateOfColumns)
        && When == other.When
        && FunctionArguments == other.FunctionArguments
        && Body == other.Body;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Timing, Events, Function, Level, When, FunctionArguments, Body);
}
