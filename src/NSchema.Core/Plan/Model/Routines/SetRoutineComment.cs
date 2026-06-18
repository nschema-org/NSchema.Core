using NSchema.Schema.Model.Routines;

namespace NSchema.Plan.Model.Routines;

/// <summary>
/// Represents setting, changing, or clearing the comment on a routine.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the routine.</param>
/// <param name="RoutineName">The name of the routine.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
/// <param name="Kind">Whether the routine is a function or a procedure.</param>
public sealed record SetRoutineComment(string SchemaName, string RoutineName, string? OldComment, string? NewComment, RoutineKind Kind) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
