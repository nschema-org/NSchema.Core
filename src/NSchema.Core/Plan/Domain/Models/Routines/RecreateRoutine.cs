using NSchema.Project.Domain.Models.Routines;

namespace NSchema.Plan.Domain.Models.Routines;

/// <summary>
/// Represents dropping and recreating a routine whose signature changed.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the routine.</param>
/// <param name="Routine">The desired routine to recreate.</param>
public sealed record RecreateRoutine(string SchemaName, Routine Routine) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
