using NSchema.Diff.Model;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Diff;

/// <summary>
/// The outcome of matching change-event scripts to a diff.
/// </summary>
/// <param name="Diff">The diff with each matched node annotated with its script's name.</param>
/// <param name="Unmatched">The scripts whose change is not in the diff, in declaration order.</param>
internal sealed record MigrationAnnotationResult(DatabaseDiff Diff, IReadOnlyList<Script> Unmatched);
