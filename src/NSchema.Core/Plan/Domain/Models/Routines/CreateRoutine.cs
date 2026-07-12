using NSchema.Project.Domain.Models.Routines;

namespace NSchema.Plan.Domain.Models.Routines;

/// <summary>
/// Represents the creation (or in-place body replacement) of a routine.
/// </summary>
/// <param name="SchemaName">The name of the schema the routine belongs to.</param>
/// <param name="Routine">The definition of the routine to create or replace.</param>
public sealed record CreateRoutine(string SchemaName, Routine Routine) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
