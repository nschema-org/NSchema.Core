using NSchema.Schema.Model;

namespace NSchema.Plan.Model;

/// <summary>
/// Represents the creation (or in-place body replacement) of a procedure. A provider emits
/// <c>CREATE OR REPLACE</c> so the same action serves an add and a definition-only modification.
/// </summary>
/// <param name="SchemaName">The name of the schema the procedure belongs to.</param>
/// <param name="Procedure">The definition of the procedure to create or replace.</param>
public sealed record CreateProcedure(string SchemaName, Procedure Procedure) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
