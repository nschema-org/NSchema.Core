namespace NSchema.Plan.Domain.Models.Sequences;

/// <summary>
/// Represents setting, changing, or clearing the comment on a sequence.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the sequence.</param>
/// <param name="SequenceName">The name of the sequence.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetSequenceComment(string SchemaName, string SequenceName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
