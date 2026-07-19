using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Model.Routines;

/// <summary>
/// Represents dropping and recreating a routine whose signature changed.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the routine.</param>
/// <param name="Routine">The desired routine to recreate.</param>
public sealed record RecreateRoutine(SqlIdentifier SchemaName, Routine Routine) : MigrationAction;
