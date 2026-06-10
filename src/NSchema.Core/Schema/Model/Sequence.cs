using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents a standalone database sequence.
/// </summary>
/// <param name="Name">The name of the sequence.</param>
/// <param name="Options">The sequence's options; unset options use the provider's defaults.</param>
/// <param name="OldName">The previous name of the sequence, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the sequence.</param>
[DebuggerDisplay("{Name,nq} (sequence)")]
public sealed record Sequence(
    string Name,
    SequenceOptions? Options = null,
    string? OldName = null,
    string? Comment = null
) : IRenameableObject
{
    /// <summary>
    /// The sequence's options; unset options use the provider's defaults.
    /// </summary>
    public SequenceOptions Options { get; init; } = Options ?? new SequenceOptions();
}
