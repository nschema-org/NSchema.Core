using System.Diagnostics;

namespace NSchema.Schema.Model.Triggers;

/// <summary>
/// Represents a trigger on a table: a function fired on a table operation.
/// </summary>
/// <param name="Name">The trigger name (unique within its table).</param>
/// <param name="Timing">When the trigger fires relative to the operation.</param>
/// <param name="Events">The operation(s) that fire the trigger.</param>
/// <param name="Function">The (optionally schema-qualified) function the trigger executes.</param>
/// <param name="Level">Whether the trigger fires per row or per statement.</param>
/// <param name="UpdateOfColumns">The columns an <c>UPDATE</c> trigger is narrowed to, if any; empty otherwise.</param>
/// <param name="When">An optional <c>WHEN</c> condition, stored verbatim (opaque SQL).</param>
/// <param name="FunctionArguments">Optional literal arguments passed to the function, stored verbatim; <see langword="null"/> when none.</param>
/// <param name="Comment">An optional comment or description for the trigger.</param>
[DebuggerDisplay("{Name,nq} (trigger)")]
public record Trigger(
    string Name,
    TriggerTiming Timing,
    TriggerEvent Events,
    string Function,
    TriggerLevel Level = TriggerLevel.Statement,
    IReadOnlyList<string>? UpdateOfColumns = null,
    string? When = null,
    string? FunctionArguments = null,
    string? Comment = null
) : INamedObject
{
    /// <summary>
    /// The columns an <c>UPDATE</c> trigger is narrowed to (<c>UPDATE OF (…)</c>), if any.
    /// </summary>
    public IReadOnlyList<string> UpdateOfColumns { get; init; } = UpdateOfColumns ?? [];

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
        && FunctionArguments == other.FunctionArguments;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Timing, Events, Function, Level, When, FunctionArguments);
}
