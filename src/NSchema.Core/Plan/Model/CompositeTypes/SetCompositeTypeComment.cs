using NSchema.Model;

namespace NSchema.Plan.Model.CompositeTypes;

/// <summary>
/// Represents setting, changing, or clearing the comment on a composite type.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the composite type.</param>
/// <param name="TypeName">The name of the composite type.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetCompositeTypeComment(SqlIdentifier SchemaName, SqlIdentifier TypeName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
