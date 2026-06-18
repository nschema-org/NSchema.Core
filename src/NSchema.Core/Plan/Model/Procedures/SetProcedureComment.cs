namespace NSchema.Plan.Model.Procedures;

/// <summary>
/// Represents setting, changing, or clearing the comment on a procedure.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the procedure.</param>
/// <param name="ProcedureName">The name of the procedure.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetProcedureComment(string SchemaName, string ProcedureName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
