using NSchema.Model;
using NSchema.Model.Triggers;

namespace NSchema.Diff.Domain.Models.Triggers;

/// <summary>
/// Describes a change to a table trigger.
/// </summary>
/// <param name="Kind">The change to the trigger.</param>
/// <param name="Name">The trigger name.</param>
/// <param name="Definition">The full trigger definition for a created trigger; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the trigger's comment, if any.</param>
public sealed record TriggerDiff(ChangeKind Kind, SqlIdentifier Name, Trigger? Definition = null, ValueChange<string>? Comment = null) : INamedObjectDiff;
