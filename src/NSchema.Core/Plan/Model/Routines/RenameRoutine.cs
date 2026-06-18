using NSchema.Schema.Model.Routines;

namespace NSchema.Plan.Model.Routines;

/// <summary>
/// Represents renaming an existing routine.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the routine.</param>
/// <param name="OldName">The current name of the routine.</param>
/// <param name="NewName">The new name of the routine.</param>
/// <param name="Kind">Whether the routine is a function or a procedure.</param>
public sealed record RenameRoutine(string SchemaName, string OldName, string NewName, RoutineKind Kind) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
