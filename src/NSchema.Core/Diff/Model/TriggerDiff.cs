using NSchema.Schema.Model.Triggers;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a table trigger.
/// </summary>
/// <param name="Kind">The change to the trigger.</param>
/// <param name="Name">The trigger name.</param>
/// <param name="Definition">The full trigger definition for a created trigger; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the trigger's comment, if any.</param>
public sealed record TriggerDiff(ChangeKind Kind, string Name, Trigger? Definition = null, ValueChange<string>? Comment = null) : INamedObjectDiff;
