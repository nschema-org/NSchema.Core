using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Model.Routines;

/// <summary>
/// Represents the removal of an existing routine.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the routine to be removed.</param>
/// <param name="RoutineName">The name of the routine to be removed.</param>
/// <param name="Kind">Whether the routine is a function or a procedure.</param>
public sealed record DropRoutine(SqlIdentifier SchemaName, SqlIdentifier RoutineName, RoutineKind Kind) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
