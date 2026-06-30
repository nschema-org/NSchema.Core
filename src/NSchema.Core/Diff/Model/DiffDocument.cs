namespace NSchema.Diff.Model;

/// <summary>
/// A renderer-neutral, structured rendering of a <see cref="DatabaseDiff"/>.
/// </summary>
/// <param name="Lines">The body lines, in render order. Empty when the diff has no changes.</param>
/// <param name="Summary">The aggregate add/modify/remove counts, for the footer.</param>
public sealed record DiffDocument(IReadOnlyList<DiffLine> Lines, DiffSummary Summary)
{
    /// <summary>
    /// Whether the diff produced no changes (no body lines).
    /// </summary>
    public bool IsEmpty => Lines.Count == 0;
}
