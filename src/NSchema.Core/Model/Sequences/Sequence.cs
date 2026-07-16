using System.Diagnostics;

namespace NSchema.Model.Sequences;

/// <summary>
/// Represents a standalone database sequence.
/// </summary>
/// <param name="Name">The name of the sequence.</param>
/// <param name="Options">The sequence's options; unset options use the provider's defaults.</param>
/// <param name="Comment">An optional comment or description for the sequence.</param>
[DebuggerDisplay("{Name,nq} (sequence)")]
public sealed record Sequence(
    SqlIdentifier Name,
    SequenceOptions? Options = null,
    string? Comment = null
) : INamedObject
{
    /// <summary>
    /// The sequence's options; unset options use the provider's defaults.
    /// </summary>
    public SequenceOptions Options { get; init; } = Options ?? new SequenceOptions();
}
